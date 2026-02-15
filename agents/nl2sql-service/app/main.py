from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from app.models import Nl2SqlGenerateRequest, Nl2SqlGenerateResponse
from app.graph import nl2sql_graph  # Template-based
from app.llm_graph import llm_nl2sql_graph  # LLM-powered
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
    """
    try:
        logger.info(f"Received request: {request.question} from customer {request.context.customer_id}")
        
        # Prepare initial state
        initial_state = {
            "question": request.question,
            "customer_id": request.context.customer_id,
            "user_id": request.context.user_id,
            "role": request.context.role,
            "tenant_column": request.constraints.tenant_column,
            "default_limit": request.constraints.default_limit,
            "allowed_tables": request.constraints.allowed_tables,
            
            # Initialize output fields
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
        response = Nl2SqlGenerateResponse(
            sql_candidate=result["sql_candidate"],
            confidence=result["confidence"],
            needs_clarification=result["needs_clarification"],
            clarification_prompt=result.get("clarification_prompt"),
            reasoning=result["reasoning"],
            original_question=request.question,
            detected_domain=result.get("detected_domain"),
            scoped_tables=result.get("scoped_tables", [])
        )
        
        logger.info(f"Generated SQL with confidence {response.confidence}: {response.sql_candidate[:100]}...")
        return response
        
    except Exception as e:
        logger.error(f"Error generating SQL: {str(e)}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"SQL generation failed: {str(e)}")


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
