from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from app.models import Nl2SqlGenerateRequest, Nl2SqlGenerateResponse
from app.graph import nl2sql_graph  # Template-based
from app.llm_graph import llm_nl2sql_graph, get_query_cache, MetricsCollector, extract_limit_from_question, extract_numeric_filters  # LLM-powered
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
        
        # Phase 3: Create Pydantic state with validation
        if USE_LLM:
            initial_state_model = LLMAgentState(
                question=request.question,
                customer_id=request.context.customer_id,
                user_id=request.context.user_id,
                role=request.context.role,
                tenant_column=request.constraints.tenant_column,
                default_limit=request.constraints.default_limit,
                allowed_tables=request.constraints.allowed_tables,
                metrics_collector=metrics,
                extracted_limit=extracted_limit,
                extracted_filters=extracted_filters
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
                "tenant_column": request.constraints.tenant_column,
                "default_limit": request.constraints.default_limit,
                "allowed_tables": request.constraints.allowed_tables,
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
        
        # Phase 2: Log metrics summary
        if USE_LLM and metrics:
            metrics_summary = metrics.summary()
            logger.info(f"Metrics: {metrics_summary['total_duration_ms']}ms total, "
                       f"{len(metrics_summary['agents'])} agents, "
                       f"{metrics_summary['success_rate']*100:.0f}% success rate")
        
        logger.info(f"Generated SQL with confidence {response.confidence}: {response.sql_candidate[:100]}...")
        return response
        
    except Exception as e:
        logger.error(f"Error generating SQL: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"SQL generation failed: {str(e)}")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
