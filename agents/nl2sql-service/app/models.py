from pydantic import BaseModel, Field
from typing import Optional


class UserContext(BaseModel):
    """User context from auth claims"""
    customer_id: str
    user_id: str
    role: str


class Nl2SqlConstraints(BaseModel):
    """Constraints and policies for SQL generation"""
    tenant_column: str = "CustomerId"
    default_limit: int = 50
    max_limit: int = 200
    select_only: bool = True
    allowed_tables: list[str] = Field(default_factory=list)
    allowed_functions: list[str] = Field(default_factory=list)


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
    scoped_tables: list[str] = Field(default_factory=list)
