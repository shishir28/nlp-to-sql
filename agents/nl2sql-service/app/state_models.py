"""
Phase 3: Pydantic models for type-safe state management
Replaces TypedDict with validated Pydantic BaseModel
"""
from pydantic import BaseModel, Field, field_validator
from typing import Optional, List, Dict, Any
from enum import Enum


class Domain(str, Enum):
    """Business domain categories for queries"""
    TENANCY = "tenancy"
    ARREARS = "arrears"
    ARREARS_ESCALATION = "arrears_escalation"
    MAINTENANCE = "maintenance"
    MAINTENANCE_WORKFLOW = "maintenance_workflow"
    INSPECTION = "inspection"
    OWNER_STATEMENT = "owner_statement"
    VENDOR = "vendor"
    PROPERTY_PORTFOLIO = "property_portfolio"
    LEASE_RENEWAL = "lease_renewal"
    COMPLIANCE = "compliance"
    COMPLIANCE_CALENDAR = "compliance_calendar"
    FINANCIAL_SUMMARY = "financial_summary"
    VACANCY = "vacancy"
    LETTING = "letting"
    TRUST_LEDGER = "trust_ledger"
    PM_TASKS = "pm_tasks"
    GENERAL = "general"


class QueryComplexity(str, Enum):
    """Query complexity levels for routing"""
    SIMPLE = "simple"      # Single table, basic filter
    MEDIUM = "medium"      # 1-2 joins, simple aggregation
    COMPLEX = "complex"    # Multiple joins, complex aggregations, subqueries


class LLMAgentState(BaseModel):
    """
    Strongly-typed state for LLM agent pipeline.
    Phase 3: Replaces TypedDict with Pydantic for runtime validation.
    """
    
    # ===== Input Fields (Required) =====
    question: str = Field(..., min_length=1, max_length=500, description="Natural language question")
    customer_id: str = Field(..., description="Customer ID for multi-tenancy")
    user_id: str = Field(..., description="User making the request")
    role: str = Field(..., description="User role (PropertyManager, Tenant, Owner)")
    dialect: str = Field(default="mysql8", description="SQL dialect expected by execution layer")
    tenant_column: str = Field(default="CustomerId", description="Column used for tenant filtering")
    default_limit: int = Field(default=50, ge=1, le=1000, description="Default row limit")
    max_limit: int = Field(default=200, ge=1, le=1000, description="Maximum row limit")
    allowed_tables: List[str] = Field(default_factory=list, description="Tables accessible to this user")
    allowed_functions: List[str] = Field(default_factory=list, description="Functions permitted by SQL firewall")
    
    # ===== Agent Outputs (Optional with defaults) =====
    detected_domain: Optional[str] = Field(None, description="Classified business domain")
    domain_confidence: float = Field(default=0.0, ge=0.0, le=1.0, description="Domain classification confidence")
    scoped_tables: List[str] = Field(default_factory=list, description="Tables relevant to this query")
    schema_description: str = Field(default="", description="Schema analysis output")
    query_plan: str = Field(default="", description="Query execution plan")
    sql_candidate: str = Field(default="", description="Generated SQL query")
    validation_notes: str = Field(default="", description="SQL validation results")
    confidence: float = Field(default=0.0, ge=0.0, le=1.0, description="Overall query confidence")
    reasoning: str = Field(default="", description="Agent reasoning chain")
    needs_clarification: bool = Field(default=False, description="Whether query needs user clarification")
    clarification_prompt: Optional[str] = Field(None, description="Clarification message for user")
    
    # ===== Phase 2: Performance & Filters =====
    metrics_collector: Optional[Any] = Field(None, description="Metrics collection object")
    extracted_limit: Optional[int] = Field(None, ge=1, le=1000, description="Limit extracted from question")
    extracted_filters: Dict[str, Any] = Field(default_factory=dict, description="Numeric filters extracted from question")
    
    # ===== Phase 3: Routing & Optimization =====
    query_complexity: Optional[QueryComplexity] = Field(None, description="Query complexity for routing")
    agent_route: Optional[str] = Field(None, description="Selected agent execution path")
    prompt_version: str = Field(default="v1", description="Prompt template version used")
    schema_version: str = Field(default="1.0", description="Database schema version")

    # ===== Parallel Prefetch =====
    prefetched_fk_map: Dict[str, Any] = Field(default_factory=dict, description="FK map prefetched in parallel")

    # ===== Conversation Memory =====
    conversation_history: List[Dict[str, Any]] = Field(default_factory=list, description="Prior turns in this conversation")
    
    @field_validator('question')
    @classmethod
    def clean_question(cls, v: str) -> str:
        """Normalize question text"""
        return v.strip()

    @field_validator('default_limit', 'extracted_limit')
    @classmethod
    def validate_limit(cls, v: Optional[int]) -> Optional[int]:
        """Ensure limit is within acceptable range"""
        if v is not None and v > 1000:
            return 1000
        return v

    @field_validator('confidence', 'domain_confidence')
    @classmethod
    def validate_confidence(cls, v: float) -> float:
        """Ensure confidence is between 0 and 1"""
        return max(0.0, min(1.0, v))
    
    class Config:
        use_enum_values = True
        validate_assignment = True  # Validate on field updates
        arbitrary_types_allowed = True  # Allow MetricsCollector and other custom types



class PromptConfig(BaseModel):
    """Configuration for prompt templates"""
    version: str
    author: str
    created: str
    description: str
    temperature: Optional[float] = Field(None, ge=0.0, le=2.0)
    max_tokens: Optional[int] = Field(None, ge=1, le=4096)
    template: str
    variables: List[str] = Field(default_factory=list)
    examples: List[Dict[str, str]] = Field(default_factory=list)
    
    class Config:
        validate_assignment = True
