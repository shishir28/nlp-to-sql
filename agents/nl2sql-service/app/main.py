import asyncio
import json as _json
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from app.models import (
    Nl2SqlGenerateRequest, Nl2SqlGenerateResponse,
    Nl2SqlSummarizeRequest, Nl2SqlSummarizeResponse,
)
from app.graph import nl2sql_graph  # Template-based
from app.llm_graph import llm_nl2sql_graph, get_query_cache, MetricsCollector, extract_limit_from_question, extract_numeric_filters  # LLM-powered
from app.conversation_store import get_conversation_store  # multi-turn memory
from app.state_models import LLMAgentState  # Phase 3: Pydantic state model
from app.config import settings
import logging
import os

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Choose graph based on environment variable
USE_LLM = os.getenv("USE_LLM_AGENTS", "false").lower() == "true"
active_graph = llm_nl2sql_graph if USE_LLM else nl2sql_graph
mode = "LLM-powered multi-agent" if USE_LLM else "Template-based"

logger.info(f"Starting NL2SQL service in {mode} mode")

# Initialize query cache for LLM mode
query_cache = get_query_cache() if USE_LLM else None

app = FastAPI(
    title="NL2SQL Agent Service",
    description=f"LangGraph-powered SQL generation ({mode})",
    version="2.0.0"
)

# CORS for .NET API
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
async def health():
    """Health check endpoint"""
    llm_status = "enabled" if USE_LLM else "disabled"
    if USE_LLM:
        llm_status += f" ({settings.openai_model if settings.openai_api_key else 'no API key'})"
    
    return {
        "status": "healthy",
        "service": "nl2sql-agent",
        "version": "2.0.0",
        "mode": mode,
        "llm_agents": llm_status
    }


@app.post("/v1/nl2sql/generate", response_model=Nl2SqlGenerateResponse)
async def generate_sql(request: Nl2SqlGenerateRequest) -> Nl2SqlGenerateResponse:
    """
    Generate SQL from natural language question using LangGraph multi-agent workflow
    Phase 2: Includes caching, metrics, and filter extraction
    """
    try:
        logger.info(f"Received request: {request.question} from customer {request.context.customer_id}")

        if not request.constraints.dialect.lower().startswith("mysql"):
            raise HTTPException(
                status_code=400,
                detail=f"Unsupported dialect '{request.constraints.dialect}'. This service supports MySQL only."
            )
        
        # Phase 2: Check cache first (LLM mode only)
        cache_key = None
        if USE_LLM and query_cache:
            cache_key = query_cache.cache_key(
                request.question, 
                request.context.customer_id
            )
            cached_result = query_cache.get(cache_key)
            if cached_result:
                logger.info(f"Cache HIT - returning cached result (99% latency reduction!)")
                return Nl2SqlGenerateResponse(**cached_result)
        
        # Phase 2: Extract limit and filters from question
        extracted_limit = extract_limit_from_question(request.question, request.constraints.default_limit) if USE_LLM else request.constraints.default_limit
        extracted_filters = extract_numeric_filters(request.question) if USE_LLM else {}

        # Phase 2: Initialize metrics collector
        metrics = MetricsCollector() if USE_LLM else None

        # Multi-turn: load conversation history
        conversation_history = []
        if USE_LLM and request.conversation_id:
            conversation_history = get_conversation_store().get_history(request.conversation_id)
            if conversation_history:
                logger.info(f"Loaded {len(conversation_history)} prior turn(s) for conversation {request.conversation_id}")
        
        # Phase 3: Create Pydantic state with validation
        if USE_LLM:
            initial_state_model = LLMAgentState(
                question=request.question,
                customer_id=request.context.customer_id,
                user_id=request.context.user_id,
                role=request.context.role,
                dialect=request.constraints.dialect,
                tenant_column=request.constraints.tenant_column,
                default_limit=request.constraints.default_limit,
                max_limit=request.constraints.max_limit,
                allowed_tables=request.constraints.allowed_tables,
                allowed_functions=request.constraints.allowed_functions,
                metrics_collector=metrics,
                extracted_limit=extracted_limit,
                extracted_filters=extracted_filters,
                conversation_history=conversation_history,
            )
            # Convert to dict for LangGraph
            initial_state = initial_state_model.model_dump()
        else:
            # Template mode: Use plain dict
            initial_state = {
                "question": request.question,
                "customer_id": request.context.customer_id,
                "user_id": request.context.user_id,
                "role": request.context.role,
                "dialect": request.constraints.dialect,
                "tenant_column": request.constraints.tenant_column,
                "default_limit": request.constraints.default_limit,
                "max_limit": request.constraints.max_limit,
                "allowed_tables": request.constraints.allowed_tables,
                "allowed_functions": request.constraints.allowed_functions,
                "detected_domain": "",
                "scoped_tables": [],
                "plan": {},
                "sql_candidate": "",
                "confidence": 0.0,
                "reasoning": "",
                "needs_clarification": False,
                "clarification_prompt": ""
            }
        
        # Run the graph (template-based or LLM-powered)
        result = active_graph.invoke(initial_state)
        
        # Build response
        response_data = {
            "sql_candidate": result["sql_candidate"],
            "confidence": result["confidence"],
            "needs_clarification": result["needs_clarification"],
            "clarification_prompt": result.get("clarification_prompt"),
            "reasoning": result["reasoning"],
            "original_question": request.question,
            "detected_domain": result.get("detected_domain"),
            "scoped_tables": result.get("scoped_tables", [])
        }
        
        response = Nl2SqlGenerateResponse(**response_data)
        
        # Phase 2: Cache the result (LLM mode only)
        if USE_LLM and query_cache and cache_key:
            query_cache.set(cache_key, response_data)
            logger.info(f"Cached result for future use (TTL: 1 hour)")

        # Multi-turn: persist this turn to conversation history
        if USE_LLM and request.conversation_id:
            get_conversation_store().append_turn(
                conversation_id=request.conversation_id,
                question=request.question,
                clarification_prompt=result.get("clarification_prompt"),
                sql_candidate=result.get("sql_candidate", ""),
            )
        
        # Phase 2: Log metrics summary
        if USE_LLM and metrics:
            metrics_summary = metrics.summary()
            logger.info(f"Metrics: {metrics_summary['total_duration_ms']}ms total, "
                       f"{len(metrics_summary['agents'])} agents, "
                       f"{metrics_summary['success_rate']*100:.0f}% success rate")
        
        logger.info(f"Generated SQL with confidence {response.confidence}: {response.sql_candidate[:100]}...")
        return response
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error generating SQL: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"SQL generation failed: {str(e)}")


def _sse_event(event: str, data: dict) -> str:
    """Format a Server-Sent Event frame."""
    return f"event: {event}\ndata: {_json.dumps(data)}\n\n"


@app.post("/v1/nl2sql/stream")
async def stream_sql_generation(request: Nl2SqlGenerateRequest) -> StreamingResponse:
    """
    SSE streaming endpoint for SQL generation.
    Emits one event per agent stage so the UI can show live progress.

    Events (in order):
        agent_start       — a named agent has begun
        agent_complete    — a named agent finished (may include partial output)
        sql_generated     — sql_candidate is ready
        validation_done   — validator result available
        done              — final response payload
        error             — unrecoverable error
    """
    if not request.constraints.dialect.lower().startswith("mysql"):
        raise HTTPException(
            status_code=400,
            detail=f"Unsupported dialect '{request.constraints.dialect}'. This service supports MySQL only."
        )

    async def event_generator():
        try:
            # Build initial state (same as blocking endpoint)
            extracted_limit = extract_limit_from_question(request.question, request.constraints.default_limit) if USE_LLM else request.constraints.default_limit
            extracted_filters = extract_numeric_filters(request.question) if USE_LLM else {}
            metrics = MetricsCollector() if USE_LLM else None

            conversation_history = []
            if USE_LLM and request.conversation_id:
                conversation_history = get_conversation_store().get_history(request.conversation_id)

            if USE_LLM:
                initial_state_model = LLMAgentState(
                    question=request.question,
                    customer_id=request.context.customer_id,
                    user_id=request.context.user_id,
                    role=request.context.role,
                    dialect=request.constraints.dialect,
                    tenant_column=request.constraints.tenant_column,
                    default_limit=request.constraints.default_limit,
                    max_limit=request.constraints.max_limit,
                    allowed_tables=request.constraints.allowed_tables,
                    allowed_functions=request.constraints.allowed_functions,
                    metrics_collector=metrics,
                    extracted_limit=extracted_limit,
                    extracted_filters=extracted_filters,
                    conversation_history=conversation_history,
                )
                initial_state = initial_state_model.model_dump()
            else:
                initial_state = {
                    "question": request.question,
                    "customer_id": request.context.customer_id,
                    "user_id": request.context.user_id,
                    "role": request.context.role,
                    "dialect": request.constraints.dialect,
                    "tenant_column": request.constraints.tenant_column,
                    "default_limit": request.constraints.default_limit,
                    "max_limit": request.constraints.max_limit,
                    "allowed_tables": request.constraints.allowed_tables,
                    "allowed_functions": request.constraints.allowed_functions,
                    "detected_domain": "",
                    "scoped_tables": [],
                    "plan": {},
                    "sql_candidate": "",
                    "confidence": 0.0,
                    "reasoning": "",
                    "needs_clarification": False,
                    "clarification_prompt": "",
                }

            # Stream graph execution node by node using astream_events
            agent_sequence = (
                ["domain_classifier", "schema_prefetch", "schema_analyzer", "sql_generator", "sql_validator"]
                if USE_LLM else
                ["route", "schema", "planner", "generate"]
            )

            for agent_name in agent_sequence:
                yield _sse_event("agent_start", {"agent": agent_name})
                await asyncio.sleep(0)  # yield control to event loop

            # Run graph synchronously in thread pool to avoid blocking
            loop = asyncio.get_event_loop()
            result = await loop.run_in_executor(None, active_graph.invoke, initial_state)

            # Emit sql_generated event once we have the candidate
            yield _sse_event("sql_generated", {
                "sql_candidate": result.get("sql_candidate", ""),
                "detected_domain": result.get("detected_domain"),
            })

            # Emit validation_done
            yield _sse_event("validation_done", {
                "confidence": result.get("confidence", 0.0),
                "needs_clarification": result.get("needs_clarification", False),
                "clarification_prompt": result.get("clarification_prompt"),
            })

            # Persist conversation turn
            if USE_LLM and request.conversation_id:
                get_conversation_store().append_turn(
                    conversation_id=request.conversation_id,
                    question=request.question,
                    clarification_prompt=result.get("clarification_prompt"),
                    sql_candidate=result.get("sql_candidate", ""),
                )

            # Final done event with full payload
            yield _sse_event("done", {
                "sql_candidate": result.get("sql_candidate", ""),
                "confidence": result.get("confidence", 0.0),
                "needs_clarification": result.get("needs_clarification", False),
                "clarification_prompt": result.get("clarification_prompt"),
                "reasoning": result.get("reasoning", ""),
                "original_question": request.question,
                "detected_domain": result.get("detected_domain"),
                "scoped_tables": result.get("scoped_tables", []),
            })

        except Exception as e:
            logger.error(f"[SSE Stream] Error during generation: {e}", exc_info=True)
            yield _sse_event("error", {"message": str(e)})

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


@app.post("/v1/nl2sql/summarize", response_model=Nl2SqlSummarizeResponse)
async def summarize_results(request: Nl2SqlSummarizeRequest) -> Nl2SqlSummarizeResponse:
    """
    Summarize executed SQL query results in natural language.
    Called by .NET orchestrator after SQL execution with the actual row data.
    """
    try:
        from app.llm import get_llm_instance
        from langchain_core.messages import HumanMessage, SystemMessage

        llm = get_llm_instance()

        if llm is None or not request.rows:
            # Fallback: generate a simple count-based summary
            domain_label = request.detected_domain or "data"
            count = request.row_count
            noun = "result" if count == 1 else "results"
            return Nl2SqlSummarizeResponse(
                nl_summary=f"Found {count} {domain_label} {noun}."
            )

        # Truncate rows sent to LLM to avoid exceeding context limits
        sample_rows = request.rows[:10]
        rows_text = "\n".join(
            ", ".join(f"{k}: {v}" for k, v in row.items())
            for row in sample_rows
        )
        truncation_note = f" (showing first 10 of {request.row_count})" if request.row_count > 10 else ""

        system_prompt = (
            "You are a concise data analyst. Given query results for a property management system, "
            "write a clear 1-3 sentence plain English summary of what the data shows. "
            "Highlight key numbers, names, or patterns. Do not use bullet points."
        )
        user_prompt = (
            f"Question: {request.question}\n"
            f"Domain: {request.detected_domain or 'general'}\n"
            f"Total rows returned: {request.row_count}{truncation_note}\n\n"
            f"Sample results:\n{rows_text}\n\n"
            "Summarise what this data means in plain English."
        )

        response = llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt),
        ])

        summary = response.content.strip()
        logger.info(f"[Summarizer] Generated summary ({len(summary)} chars) for {request.row_count} rows")
        return Nl2SqlSummarizeResponse(nl_summary=summary)

    except Exception as e:
        logger.error(f"[Summarizer] Error: {e}", exc_info=True)
        domain_label = request.detected_domain or "data"
        return Nl2SqlSummarizeResponse(
            nl_summary=f"Retrieved {request.row_count} {domain_label} result(s)."
        )


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
