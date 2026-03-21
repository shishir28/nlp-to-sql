import asyncio
import threading
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

            # Stream graph execution node-by-node using LangGraph stream().
            # graph.stream() is a synchronous generator so we run it in a thread
            # and shuttle events back via an asyncio.Queue to keep the SSE
            # response truly asynchronous.
            loop = asyncio.get_event_loop()
            queue: asyncio.Queue = asyncio.Queue()
            final_state: dict = {}

            def _run_stream():
                try:
                    state = initial_state.copy()
                    for chunk in active_graph.stream(state):
                        node = list(chunk.keys())[0]
                        node_data = chunk[node] or {}
                        final_state.update(node_data)
                        loop.call_soon_threadsafe(
                            queue.put_nowait, ("node", node, node_data)
                        )
                    loop.call_soon_threadsafe(queue.put_nowait, ("done", None, None))
                except Exception as exc:
                    loop.call_soon_threadsafe(
                        queue.put_nowait, ("error", str(exc), None)
                    )

            t = threading.Thread(target=_run_stream, daemon=True)
            t.start()

            sql_emitted = False
            while True:
                kind, node, node_data = await queue.get()
                if kind == "error":
                    yield _sse_event("error", {"message": node})
                    return
                if kind == "done":
                    break

                # Emit agent_start then agent_complete for each node
                yield _sse_event("agent_start", {"agent": node})
                yield _sse_event("agent_complete", {
                    "agent": node,
                    "detected_domain": node_data.get("detected_domain"),
                })

                # Emit sql_generated as soon as we have a candidate
                if not sql_emitted and node_data.get("sql_candidate"):
                    sql_emitted = True
                    yield _sse_event("sql_generated", {
                        "sql_candidate": node_data["sql_candidate"],
                        "detected_domain": node_data.get("detected_domain"),
                    })

            result = final_state

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


@app.post("/v1/nl2sql/suggest")
async def suggest_queries(request: dict) -> dict:
    """
    Return domain-aware query suggestions based on partial input or empty prompt.
    Used by the Angular dashboard input to show autocomplete chips.
    """
    partial: str = (request.get("partial") or "").lower().strip()
    domain: str = (request.get("domain") or "").lower()

    domain_suggestions: dict[str, list[str]] = {
        "arrears": [
            "Which tenancies have arrears?",
            "Show rent arrears by property",
            "List tenancies overdue by more than 14 days",
        ],
        "maintenance": [
            "Show open maintenance jobs older than 30 days",
            "Show maintenance jobs by category",
            "List urgent maintenance requests",
        ],
        "tenancy": [
            "Show active tenancies ending in next 60 days",
            "Which leases are expiring in 90 days?",
            "List tenancies with rent below $500 per week",
        ],
        "inspection": [
            "List upcoming inspections for next month",
            "Show non-compliant inspection results",
            "How many inspections are overdue?",
        ],
        "owner_statement": [
            "Show total income summary by owner",
            "Which owners have outstanding balances?",
        ],
        "property_portfolio": [
            "Show vacant properties in portfolio",
            "List properties with no active tenancy",
            "How many properties are in each suburb?",
        ],
    }

    generic: list[str] = [
        "Which tenancies have arrears?",
        "Show open maintenance jobs",
        "Show active tenancies ending in next 60 days",
        "List upcoming inspections",
        "Show vacant properties in portfolio",
        "Show total income summary by owner",
        "Which leases are expiring in 90 days?",
        "Show non-compliant inspection results",
        "List all active contractors",
        "Show rent arrears by property",
    ]

    pool: list[str] = domain_suggestions.get(domain, generic)

    if partial and len(partial) >= 2:
        filtered = [s for s in pool if partial in s.lower()]
        # supplement with generic if too few
        if len(filtered) < 4:
            filtered += [s for s in generic if partial in s.lower() and s not in filtered]
    else:
        filtered = pool

    return {"suggestions": filtered[:8]}


@app.post("/v1/nl2sql/anomalies")
async def detect_anomalies(request: dict) -> dict:
    """
    Lightweight anomaly detection on a set of numeric rows.
    Uses Z-score to flag outliers (|z| > 2).
    Called by the Angular AnomalyWidget with the rows from a previous query.
    """
    try:
        rows: list[dict] = request.get("rows") or []
        value_key: str = request.get("valueKey") or ""
        label_key: str = request.get("labelKey") or ""

        if not rows or not value_key:
            return {"anomalies": [], "mean": None, "stddev": None}

        values = [float(r[value_key]) for r in rows if value_key in r and r[value_key] is not None]
        if len(values) < 3:
            return {"anomalies": [], "mean": None, "stddev": None}

        mean = sum(values) / len(values)
        variance = sum((v - mean) ** 2 for v in values) / len(values)
        stddev = variance ** 0.5

        if stddev == 0:
            return {"anomalies": [], "mean": round(mean, 4), "stddev": 0}

        anomalies = []
        for row in rows:
            val = row.get(value_key)
            if val is None:
                continue
            z = (float(val) - mean) / stddev
            if abs(z) > 2.0:
                anomalies.append({
                    "label": str(row.get(label_key, "")),
                    "value": float(val),
                    "z_score": round(z, 3),
                    "direction": "high" if z > 0 else "low",
                })

        return {
            "anomalies": anomalies,
            "mean": round(mean, 4),
            "stddev": round(stddev, 4),
        }

    except Exception as e:
        logger.error(f"[Anomaly] Error: {e}", exc_info=True)
        return {"anomalies": [], "error": str(e)}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
