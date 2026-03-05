from pydantic import BaseModel, Field
from typing import Any, Dict, List, Optional


class UserContext(BaseModel):
    """User context from auth claims"""
    customer_id: str
    user_id: str
    role: str


class Nl2SqlConstraints(BaseModel):
    """Constraints and policies for SQL generation"""
    dialect: str = "mysql8"
    tenant_column: str = "CustomerId"
    default_limit: int = 50
    max_limit: int = 200
    select_only: bool = True
    allowed_tables: List[str] = Field(default_factory=list)
    allowed_functions: List[str] = Field(default_factory=list)


class Nl2SqlGenerateRequest(BaseModel):
    """Request from .NET API to generate SQL"""
    question: str
    context: UserContext
    constraints: Nl2SqlConstraints
    conversation_id: Optional[str] = None


class Nl2SqlGenerateResponse(BaseModel):
    """Response from agent service to .NET API"""
    sql_candidate: str
    confidence: float  # 0.0 to 1.0
    needs_clarification: bool
    clarification_prompt: Optional[str] = None
    reasoning: str
    original_question: str
    detected_domain: Optional[str] = None
    scoped_tables: List[str] = Field(default_factory=list)


class Nl2SqlSummarizeRequest(BaseModel):
    """Request from .NET API to summarize executed query results in natural language"""
    question: str
    detected_domain: Optional[str] = None
    row_count: int = 0
    rows: List[Dict[str, Any]] = Field(default_factory=list)


class Nl2SqlSummarizeResponse(BaseModel):
    """Natural language summary of query results"""
    nl_summary: str
