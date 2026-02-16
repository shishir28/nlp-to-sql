"""
Multi-agent LLM-powered SQL generation using LangGraph
"""
from typing import TypedDict, Annotated, Optional, Dict, Any
from langgraph.graph import StateGraph, END
from langchain_core.messages import HumanMessage, SystemMessage
from app.llm import get_llm_instance
from app.schema_introspection import get_introspector
import json
import logging
import re

logger = logging.getLogger(__name__)

# Database schema definition for accurate SQL generation
SCHEMA_DEFINITION = """
## MySQL Database Schema (ALL tables include CustomerId for multi-tenancy)

### Properties
PropertyId BIGINT (PK), **CustomerId BIGINT (required)**, AddressLine1 VARCHAR, AddressLine2 VARCHAR, 
Suburb VARCHAR, StateCode CHAR(3), Postcode VARCHAR, PropertyType VARCHAR (House/Unit/Townhouse), 
Bedrooms TINYINT, Bathrooms TINYINT, Parking TINYINT, IsActive BIT, CreatedAtUtc DATETIME

### Tenants  
TenantId BIGINT (PK), **CustomerId BIGINT (required)**, FullName VARCHAR, Email VARCHAR, 
Phone VARCHAR, EmergencyContact VARCHAR, CreatedAtUtc DATETIME

### Tenancies
TenancyId BIGINT (PK), **CustomerId BIGINT (required)**, TenantId BIGINT (FK→Tenants), 
PropertyId BIGINT (FK→Properties), StatusCode VARCHAR ('ACTIVE'|'ENDED'|'CANCELLED'), 
StartDate DATE, EndDate DATE (nullable, actual termination date), 
**LeaseEndDate DATE (fixed-term lease expiry - use for "ending" queries)**, 
RentAmount DECIMAL, RentFrequency VARCHAR ('Weekly'|'Fortnightly'|'Monthly'), 
BondAmount DECIMAL, CreatedAtUtc DATETIME

**CRITICAL for Tenancy Queries**:
- EndDate: NULL for active tenancies, populated when tenancy actually ends
- LeaseEndDate: Fixed-term lease expiry date - **USE THIS** for queries about "ending", "expiring", "ending in next N days"
- Active tenancies: StatusCode='ACTIVE' AND EndDate IS NULL

### RentLedgerEntries
EntryId BIGINT (PK), **CustomerId BIGINT (required)**, TenancyId BIGINT (FK→Tenancies), 
EntryType VARCHAR (Charge/Receipt/Adjustment), TransactionDate DATE, DueDate DATE, 
PaidDate DATE (nullable), Amount DECIMAL, BalanceDelta DECIMAL, Description VARCHAR, Reference VARCHAR

### MaintenanceJobs
MaintenanceJobId BIGINT (PK), **CustomerId BIGINT (required)**, PropertyId BIGINT (FK→Properties), 
TenancyId BIGINT (FK→Tenancies, nullable), VendorId BIGINT (FK→Vendors, nullable), 
StatusCode VARCHAR ('OPEN'|'IN_PROGRESS'|'COMPLETED'|'CANCELLED'), 
Priority VARCHAR ('LOW'|'MEDIUM'|'HIGH'|'URGENT'), Category VARCHAR, Summary VARCHAR, 
Description TEXT, OpenedAtUtc DATETIME, ClosedAtUtc DATETIME, EstimatedCost DECIMAL, ActualCost DECIMAL

### Inspections
InspectionId BIGINT (PK), **CustomerId BIGINT (required)**, PropertyId BIGINT (FK→Properties), 
TenancyId BIGINT (FK→Tenancies, nullable), InspectionType VARCHAR (Routine/Entry/Exit/Compliance), 
ScheduledDate DATE, CompletedDate DATE (nullable), ComplianceStatus VARCHAR (PENDING/PASS/FAIL), 
Notes TEXT, InspectorName VARCHAR, CreatedAtUtc DATETIME

### Owners
OwnerId BIGINT (PK), **CustomerId BIGINT (required)**, FullName VARCHAR, Email VARCHAR, 
Phone VARCHAR, Abn VARCHAR, CreatedAtUtc DATETIME

### OwnerStatements
OwnerStatementId BIGINT (PK), **CustomerId BIGINT (required)**, OwnerId BIGINT (FK→Owners), 
PeriodStart DATE, PeriodEnd DATE, GrossIncome DECIMAL, Expenses DECIMAL, ManagementFees DECIMAL, 
NetPayout DECIMAL, CreatedAtUtc DATETIME

### Vendors
VendorId BIGINT (PK), **CustomerId BIGINT (required)**, VendorName VARCHAR, 
Category VARCHAR (Plumbing/Electrical/Landscaping), Abn VARCHAR, Phone VARCHAR, Email VARCHAR, 
IsActive BIT, CreatedAtUtc DATETIME

### PropertyOwners (Junction table)
PropertyOwnerId BIGINT (PK), **CustomerId BIGINT (required)**, PropertyId BIGINT, OwnerId BIGINT, 
OwnershipPct DECIMAL, StartDate DATE, EndDate DATE

### Common JOIN Patterns
- Tenancies → Tenants: t.TenantId = tn.TenantId AND t.CustomerId = tn.CustomerId
- Tenancies → Properties: t.PropertyId = p.PropertyId AND t.CustomerId = p.CustomerId
- MaintenanceJobs → Properties: m.PropertyId = p.PropertyId AND m.CustomerId = p.CustomerId
- Inspections → Properties: i.PropertyId = p.PropertyId AND i.CustomerId = p.CustomerId
- OwnerStatements → Owners: os.OwnerId = o.OwnerId AND os.CustomerId = o.CustomerId

### Address Formatting
- Standard: CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress
- Full: CONCAT(p.AddressLine1, ', ', p.Suburb, ' ', p.StateCode, ' ', p.Postcode)

### Date Functions
- Current date: CURDATE()
- Future date: DATE_ADD(CURDATE(), INTERVAL N DAY)
- Past date: DATE_SUB(CURDATE(), INTERVAL N DAY)
- Days between: DATEDIFF(date1, date2)

### CRITICAL Rules
1. Column name is Phone (NOT PhoneNumber)
2. Every table has CustomerId - security filtering is added automatically
3. Use table aliases: t (Tenancies), tn (Tenants), p (Properties), m (MaintenanceJobs), i (Inspections)
4. Always include LIMIT clause
"""


def safe_parse_json(content: str, default: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """
    Safely parse JSON from LLM response, handling markdown code blocks and malformed JSON.
    
    Args:
        content: Raw LLM response content
        default: Default dict to return on failure
        
    Returns:
        Parsed JSON dict or default
    """
    if default is None:
        default = {}
    
    try:
        # Try direct parsing first
        return json.loads(content)
    except json.JSONDecodeError:
        # Try extracting JSON from markdown code blocks
        json_match = re.search(r'```(?:json)?\s*({.*?})\s*```', content, re.DOTALL)
        if json_match:
            try:
                return json.loads(json_match.group(1))
            except json.JSONDecodeError:
                pass
        
        # Try finding first { to last }
        try:
            start = content.index('{')
            end = content.rindex('}') + 1
            return json.loads(content[start:end])
        except (ValueError, json.JSONDecodeError):
            logger.warning(f"Failed to parse JSON from content: {content[:200]}...")
            return default


class LLMAgentState(TypedDict):
    """State passed between LLM-powered agents"""
    question: str
    customer_id: str
    user_id: str
    role: str
    tenant_column: str
    default_limit: int
    allowed_tables: list[str]
    
    # Agent outputs
    detected_domain: str
    domain_confidence: float
    scoped_tables: list[str]
    schema_description: str
    query_plan: str
    sql_candidate: str
    validation_notes: str
    confidence: float
    reasoning: str
    needs_clarification: bool
    clarification_prompt: str


def domain_classifier_agent(state: LLMAgentState) -> LLMAgentState:
    """
    Agent 1: Domain Classification
    Uses LLM to classify the question into a domain and identify relevant tables.
    """
    logger.info(f"[Domain Classifier] Processing: {state['question']}")
    llm = get_llm_instance()
    
    # Fallback to template-based if no LLM
    if llm is None:
        logger.info("[Domain Classifier] Using template-based fallback (no LLM)")
        introspector = get_introspector()
        domain_info = introspector.get_table_info_for_question(
            state["question"], 
            state["allowed_tables"]
        )
        state["detected_domain"] = domain_info["domain"]
        state["domain_confidence"] = domain_info.get("confidence", 0.7)
        state["reasoning"] = f"Template-based: {domain_info['reason']}"
        logger.info(f"[Domain Classifier] Result: domain={domain_info['domain']}, confidence={state['domain_confidence']}")
        return state
    
    # LLM-based domain classification
    system_prompt = """You are a domain classification expert for a property management system.
Given a natural language question, identify:
1. The primary business domain (tenancy, arrears, maintenance, inspection, owner_statement, or general)
2. Relevant database tables needed to answer the question
3. Your confidence level (0.0 to 1.0)

Available tables: {tables}

Respond in JSON format:
{{
    "domain": "domain_name",
    "confidence": 0.95,
    "tables": ["Table1", "Table2"],
    "reasoning": "Brief explanation"
}}"""
    
    user_prompt = f"""Question: {state['question']}

Available tables: {', '.join(state['allowed_tables'])}

Classify the domain and identify relevant tables."""
    
    try:
        logger.debug(f"[Domain Classifier] Calling LLM with {len(state['allowed_tables'])} tables")
        response = llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ])
        
        result = safe_parse_json(response.content, default={
            "domain": "general",
            "confidence": 0.5,
            "tables": [],
            "reasoning": "JSON parse failed"
        })
        
        state["detected_domain"] = result.get("domain", "general")
        state["domain_confidence"] = result.get("confidence", 0.5)
        state["scoped_tables"] = [t for t in result.get("tables", []) if t in state["allowed_tables"]]
        state["reasoning"] = f"LLM Classification: {result.get('reasoning', 'No reasoning provided')}"
        
        logger.info(f"[Domain Classifier] LLM result: domain={state['detected_domain']}, "
                   f"confidence={state['domain_confidence']:.2f}, tables={state['scoped_tables']}")
        
    except Exception as e:
        # Fallback on error
        logger.error(f"[Domain Classifier] LLM invocation failed: {str(e)}", exc_info=True)
        state["detected_domain"] = "general"
        state["domain_confidence"] = 0.5
        state["reasoning"] = f"LLM error, using fallback: {str(e)}"
    
    return state


def schema_analyzer_agent(state: LLMAgentState) -> LLMAgentState:
    """
    Agent 2: Schema Analysis & Query Planning
    Uses LLM to understand the schema and create a detailed query plan.
    """
    logger.info(f"[Schema Analyzer] Planning for domain={state['detected_domain']}, tables={state['scoped_tables']}")
    llm = get_llm_instance()
    
    if llm is None:
        logger.info("[Schema Analyzer] Using simple fallback (no LLM)")
        state["query_plan"] = f"Simple query on {state['scoped_tables']}"
        state["schema_description"] = "Schema info not available"
        return state
    
    system_prompt = """You are a database schema analyst. 
Analyze the schema and create a detailed query plan.

Guidelines:
- Identify required JOINs between tables
- Determine necessary WHERE clauses
- Identify any GROUP BY or aggregate functions needed
- Consider ORDER BY and LIMIT clauses
- Think about temporal filters (past/future dates)

Respond in JSON format:
{{
    "query_plan": "Detailed step-by-step plan",
    "joins_needed": ["Table1 JOIN Table2 ON ..."],
    "filters": ["condition1", "condition2"],
    "aggregations": ["SUM(Amount)", "COUNT(*)"],
    "ordering": "column ASC/DESC",
    "temporal_filter": "past/upcoming/null"
}}"""
    
    user_prompt = f"""Question: {state['question']}
Domain: {state['detected_domain']}
Relevant tables: {', '.join(state['scoped_tables'])}
Tenant column for security: {state['tenant_column']}

Create a query plan."""
    
    try:
        logger.debug("[Schema Analyzer] Calling LLM for query planning")
        response = llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ])
        
        result = safe_parse_json(response.content, default={
            "query_plan": "Unable to create plan",
            "joins_needed": [],
            "filters": [],
            "aggregations": [],
            "ordering": "created DESC",
            "temporal_filter": None
        })
        
        state["query_plan"] = result.get("query_plan", "No plan generated")
        state["schema_description"] = json.dumps(result, indent=2)
        plan_preview = result.get("query_plan", "")[:100]
        state["reasoning"] += f" | LLM Plan: {plan_preview}..."
        
        logger.info(f"[Schema Analyzer] Query plan created: {plan_preview}")
        logger.debug(f"[Schema Analyzer] Full plan: {state['schema_description']}")
        
    except Exception as e:
        logger.error(f"[Schema Analyzer] LLM invocation failed: {str(e)}", exc_info=True)
        state["query_plan"] = "Error creating plan"
        state["reasoning"] += f" | Planning error: {str(e)}"
    
    return state


def sql_generator_agent(state: LLMAgentState) -> LLMAgentState:
    """
    Agent 3: SQL Generation
    Uses LLM to generate SQL based on the query plan and schema.
    """
    logger.info("[SQL Generator] Generating SQL from query plan")
    llm = get_llm_instance()
    
    if llm is None:
        logger.info("[SQL Generator] Using template-based fallback (no LLM)")
        # Fallback to templates
        from app.graph import sql_generator as template_generator
        return template_generator(state)
    
    system_prompt = f"""You are an expert SQL generator for MySQL 8.4.
Generate clean, efficient SQL queries following these rules:

1. **CRITICAL**: DO NOT add tenant filtering (WHERE {{tenant_column}} = X) - this is injected separately
2. Use EXACT column names from the schema below (case-sensitive!)
3. Use proper JOINs with table aliases  
4. Use CONCAT for address formatting
5. Use DATEDIFF, CURDATE(), DATE_ADD for date calculations
6. Always include LIMIT clause
7. Format SQL with proper indentation

{SCHEMA_DEFINITION}

Return ONLY the SQL query, no explanation or markdown."""
    
    user_prompt = f"""Generate MySQL SQL for this question:
Question: {state['question']}

Query Plan: {state['query_plan']}

Relevant Tables: {', '.join(state['scoped_tables'])}
Default LIMIT: {state['default_limit']}

Generate the SQL now using EXACT column names from the schema."""
    
    try:
        logger.debug("[SQL Generator] Calling LLM with temperature=0 for deterministic output")
        
        # Override temperature to 0 for SQL generation (deterministic)
        from langchain_openai import ChatOpenAI
        if isinstance(llm, ChatOpenAI):
            sql_llm = llm.bind(temperature=0.0)
        else:
            sql_llm = llm
        
        response = sql_llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ])
        
        sql = response.content.strip()
        logger.debug(f"[SQL Generator] Raw LLM response length: {len(sql)} chars")
        
        # Clean up markdown code blocks if present (robust regex approach)
        sql = re.sub(r'^```(?:sql)?\s*', '', sql, flags=re.IGNORECASE)
        sql = re.sub(r'```\s*$', '', sql)
        sql = sql.strip()
        
        state["sql_candidate"] = sql
        state["reasoning"] += " | LLM-generated SQL"
        
        logger.info(f"[SQL Generator] Generated SQL ({len(sql)} chars): {sql[:100]}...")
        
    except Exception as e:
        logger.error(f"[SQL Generator] LLM invocation failed: {str(e)}", exc_info=True)
        state["sql_candidate"] = f"-- Error generating SQL: {str(e)}"
        state["reasoning"] += f" | SQL generation error: {str(e)}"
    
    return state


def sql_validator_agent(state: LLMAgentState) -> LLMAgentState:
    """
    Agent 4: SQL Validation & Refinement
    Uses LLM to review and validate the generated SQL.
    """
    logger.info("[SQL Validator] Validating generated SQL")
    llm = get_llm_instance()
    
    if llm is None:
        logger.info("[SQL Validator] Skipping validation (no LLM)")
        state["validation_notes"] = "Validation skipped (no LLM)"
        state["confidence"] = 0.7
        state["needs_clarification"] = False
        return state
    
    system_prompt = """You are a SQL validation expert.
Review the generated SQL for:
1. Syntax correctness
2. Logical correctness (does it answer the question?)
3. Security issues (but remember tenant filtering is added separately)
4. Performance concerns
5. Edge cases

Respond in JSON format:
{{
    "is_valid": true/false,
    "confidence": 0.95,
    "issues": ["issue1", "issue2"],
    "suggestions": ["suggestion1"],
    "revised_sql": "SQL if changes needed, or null"
}}"""
    
    user_prompt = f"""Validate this SQL:

Question: {state['question']}
Generated SQL:
{state['sql_candidate']}

Is this SQL correct and safe?"""
    
    try:
        logger.debug("[SQL Validator] Calling LLM for validation")
        response = llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ])
        
        result = safe_parse_json(response.content, default={
            "is_valid": True,
            "confidence": 0.7,
            "issues": [],
            "suggestions": [],
            "revised_sql": None
        })
        
        state["validation_notes"] = json.dumps(result, indent=2)
        state["confidence"] = result.get("confidence", 0.7)
        
        logger.info(f"[SQL Validator] Validation result: is_valid={result.get('is_valid')}, "
                   f"confidence={state['confidence']:.2f}")
        
        # Use revised SQL if provided
        if result.get("revised_sql"):
            logger.info("[SQL Validator] Using revised SQL from validator")
            state["sql_candidate"] = result["revised_sql"]
            state["reasoning"] += " | SQL revised by validator"
        
        # Check if clarification needed
        state["needs_clarification"] = not result.get("is_valid", True) or state["confidence"] < 0.6
        
        if state["needs_clarification"]:
            issues = result.get("issues", [])
            state["clarification_prompt"] = f"Issues with query: {', '.join(issues)}"
            logger.warning(f"[SQL Validator] Clarification needed: {state['clarification_prompt']}")
        else:
            logger.info("[SQL Validator] SQL validation passed")
        
    except Exception as e:
        logger.error(f"[SQL Validator] LLM invocation failed: {str(e)}", exc_info=True)
        state["validation_notes"] = f"Validation error: {str(e)}"
        state["confidence"] = 0.5
        state["needs_clarification"] = False
    
    return state


def should_clarify(state: LLMAgentState) -> str:
    """Route to clarification if validation failed or confidence too low"""
    return "clarify" if state.get("needs_clarification", False) else "end"


def clarification_agent(state: LLMAgentState) -> LLMAgentState:
    """Handle cases needing user clarification"""
    logger.warning(f"[Clarification] Query needs user clarification: {state.get('clarification_prompt', 'Unknown issue')}")
    state["sql_candidate"] = ""
    state["reasoning"] += " | Needs clarification"
    return state


# Build the multi-agent graph
def create_llm_agent_graph():
    logger.info("[Graph] Initializing LLM multi-agent graph")
    workflow = StateGraph(LLMAgentState)
    
    # Add agent nodes
    workflow.add_node("domain_classifier", domain_classifier_agent)
    workflow.add_node("schema_analyzer", schema_analyzer_agent)
    workflow.add_node("sql_generator", sql_generator_agent)
    workflow.add_node("sql_validator", sql_validator_agent)
    workflow.add_node("clarify", clarification_agent)
    
    # Define sequential flow with parallel potential
    workflow.set_entry_point("domain_classifier")
    workflow.add_edge("domain_classifier", "schema_analyzer")
    workflow.add_edge("schema_analyzer", "sql_generator")
    workflow.add_edge("sql_generator", "sql_validator")
    
    # Conditional routing after validation
    workflow.add_conditional_edges(
        "sql_validator",
        should_clarify,
        {
            "clarify": "clarify",
            "end": END
        }
    )
    workflow.add_edge("clarify", END)
    
    logger.info("[Graph] Multi-agent graph compiled successfully")
    return workflow.compile()


# Global graph instance
logger.info("[System] Creating global LLM agent graph instance")
llm_nl2sql_graph = create_llm_agent_graph()
