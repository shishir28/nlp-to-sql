from typing import Annotated, TypedDict
from langgraph.graph import StateGraph, END
from langgraph.graph.message import add_messages
from app.schema_introspection import get_introspector


class AgentState(TypedDict):
    """State passed between graph nodes"""
    question: str
    customer_id: str
    user_id: str
    role: str
    dialect: str
    tenant_column: str
    default_limit: int
    max_limit: int
    allowed_tables: list[str]
    allowed_functions: list[str]
    
    # Intermediate state
    detected_domain: str
    scoped_tables: list[str]
    plan: dict
    sql_candidate: str
    confidence: float
    reasoning: str
    needs_clarification: bool
    clarification_prompt: str


def route_domain(state: AgentState) -> AgentState:
    """Route question to domain using hybrid approach: JSON mapping + schema introspection"""
    question = state["question"]
    allowed_tables = state["allowed_tables"]
    
    # Get introspector singleton
    introspector = get_introspector()
    
    # Try manual mapping first, fallback to schema introspection
    domain_info = introspector.get_table_info_for_question(question, allowed_tables)
    
    state["detected_domain"] = domain_info["domain"]
    state["reasoning"] = f"Detected domain: {domain_info['domain']} (source: {domain_info['source']}, {domain_info['reason']})"
    
    return state


def schema_context(state: AgentState) -> AgentState:
    """Scope tables based on detected domain using hybrid approach"""
    question = state["question"]
    domain = state["detected_domain"]
    allowed_tables = state["allowed_tables"]
    
    # Get introspector singleton
    introspector = get_introspector()
    
    # Get table mapping (already computed in route_domain, but recalculating for clarity)
    domain_info = introspector.get_table_info_for_question(question, allowed_tables)
    
    # Use tables from domain mapping or introspection
    scoped_tables = domain_info["tables"]
    
    # Ensure all tables are in allowed list
    state["scoped_tables"] = [t for t in scoped_tables if t in allowed_tables]
    state["reasoning"] += f" | Scoped to tables: {', '.join(state['scoped_tables'])}"
    
    return state


def planner(state: AgentState) -> AgentState:
    """Build structured plan for SQL generation"""
    question = state["question"]
    domain = state["detected_domain"]
    
    # Simple plan structure
    plan = {
        "objective": question,
        "domain": domain,
        "tables": state["scoped_tables"],
        "filters": [],
        "aggregations": [],
        "requested_limit": state["default_limit"],
        "temporal_filter": None  # "past", "upcoming", or None
    }
    
    # Extract specific limit from query
    question_lower = question.lower()
    words = question_lower.split()
    
    # Map text numbers to integers
    text_numbers = {
        "one": 1, "two": 2, "three": 3, "four": 4, "five": 5,
        "six": 6, "seven": 7, "eight": 8, "nine": 9, "ten": 10,
        "fifteen": 15, "twenty": 20, "thirty": 30, "fifty": 50
    }
    
    for i, word in enumerate(words):
        limit = None
        # Check for patterns like "top 5", "5 top", "first 10", "list 5", "show 3", "show me 3"
        if word.isdigit() and int(word) > 0:
            limit = int(word)
        elif word in text_numbers:
            limit = text_numbers[word]
        
        if limit:
            # Check context before the number
            if i > 0:
                prev_word = words[i-1]
                # Also check for "show me 5" pattern (i > 1)
                if prev_word in ["top", "first", "list", "show", "most", "recent"]:
                    plan["requested_limit"] = limit
                    break
                elif i > 1 and prev_word == "me" and words[i-2] == "show":
                    plan["requested_limit"] = limit
                    break
            # Check context after the number
            if i < len(words) - 1 and words[i+1] in ["top", "first", "most", "recent"]:
                plan["requested_limit"] = limit
                break
    
    # Temporal filter extraction for inspections
    if "past" in question_lower or "previous" in question_lower or "completed" in question_lower or "historical" in question_lower:
        plan["temporal_filter"] = "past"
    elif "upcoming" in question_lower or "future" in question_lower or "scheduled" in question_lower:
        plan["temporal_filter"] = "upcoming"
    
    # Basic filter extraction
    if "active" in question_lower:
        plan["filters"].append("status = 'Active'")
    if "next" in question_lower and "days" in question_lower:
        # Extract number of days
        for i, word in enumerate(words):
            if word in ["next", "within"] and i + 1 < len(words):
                try:
                    days = int(''.join(filter(str.isdigit, words[i + 1])))
                    plan["filters"].append(f"date_range_days={days}")
                except ValueError:
                    pass
    
    # Extract numeric filters (e.g., "rent amount less than 400", "rent > 500")
    import re
    # Pattern: field name + operator + value
    numeric_patterns = [
        (r'rent\s*(?:amount)?\s*(?:less\s+than|below|under|<)\s*(\d+)', 'rent_lt'),
        (r'rent\s*(?:amount)?\s*(?:greater\s+than|above|over|>)\s*(\d+)', 'rent_gt'),
        (r'rent\s*(?:amount)?\s*(?:equals?|=|is)\s*(\d+)', 'rent_eq'),
        (r'arrears?\s*(?:greater\s+than|above|over|>)\s*(\d+)', 'arrears_gt'),
        (r'arrears?\s*(?:less\s+than|below|under|<)\s*(\d+)', 'arrears_lt'),
    ]
    
    for pattern, filter_type in numeric_patterns:
        match = re.search(pattern, question_lower)
        if match:
            value = match.group(1)
            plan["filters"].append(f"{filter_type}={value}")
            break
    
    if plan["requested_limit"] > state["max_limit"]:
        plan["requested_limit"] = state["max_limit"]

    state["plan"] = plan
    state["confidence"] = 0.7  # Medium confidence for template-based generation
    state["reasoning"] += f" | Plan: {plan['objective']}"
    return state


def sql_generator(state: AgentState) -> AgentState:
    """Generate SQL from plan using templates"""
    domain = state["detected_domain"]
    plan = state["plan"]
    tenant_col = state["tenant_column"]
    limit = plan["requested_limit"]
    
    # Extract filter values
    rent_lt = rent_gt = rent_eq = None
    arrears_lt = arrears_gt = None
    date_range_days = 60  # default
    
    for filter_str in plan["filters"]:
        if filter_str.startswith("rent_lt="):
            rent_lt = int(filter_str.split("=")[1])
        elif filter_str.startswith("rent_gt="):
            rent_gt = int(filter_str.split("=")[1])
        elif filter_str.startswith("rent_eq="):
            rent_eq = int(filter_str.split("=")[1])
        elif filter_str.startswith("arrears_gt="):
            arrears_gt = int(filter_str.split("=")[1])
        elif filter_str.startswith("arrears_lt="):
            arrears_lt = int(filter_str.split("=")[1])
        elif filter_str.startswith("date_range_days="):
            date_range_days = int(filter_str.split("=")[1])
    
    # Build dynamic WHERE clauses
    arrears_having = f"HAVING TotalArrears > {arrears_gt}" if arrears_gt else "HAVING TotalArrears > 0"
    if arrears_lt:
        arrears_having += f" AND TotalArrears < {arrears_lt}"
    
    tenancy_rent_filter = ""
    if rent_lt:
        tenancy_rent_filter = f"  AND t.RentAmount < {rent_lt}\n"
    elif rent_gt:
        tenancy_rent_filter = f"  AND t.RentAmount > {rent_gt}\n"
    elif rent_eq:
        tenancy_rent_filter = f"  AND t.RentAmount = {rent_eq}\n"
    
    # Template-based SQL generation (deterministic MVP)
    sql_templates = {
        "arrears": f"""SELECT 
    t.TenancyId,
    tn.FullName AS TenantName,
    CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
    SUM(CASE WHEN rle.PaidDate IS NULL THEN rle.Amount ELSE 0 END) AS TotalArrears,
    MAX(rle.DueDate) AS LatestDueDate
FROM Tenancies t
INNER JOIN Tenants tn ON t.TenantId = tn.TenantId
INNER JOIN Properties p ON t.PropertyId = p.PropertyId
INNER JOIN RentLedgerEntries rle ON t.TenancyId = rle.TenancyId
WHERE t.StatusCode = 'ACTIVE' 
  AND rle.PaidDate IS NULL
  AND rle.DueDate < CURDATE()
GROUP BY t.TenancyId, tn.FullName, p.AddressLine1, p.Suburb
{arrears_having}
ORDER BY TotalArrears DESC
LIMIT {limit}""",
        
        "tenancy": f"""SELECT 
    t.TenancyId,
    tn.FullName AS TenantName,
    CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
    t.StartDate,
    t.LeaseEndDate,
    t.RentAmount,
    t.RentFrequency,
    DATEDIFF(t.LeaseEndDate, CURDATE()) AS DaysUntilExpiry
FROM Tenancies t
INNER JOIN Tenants tn ON t.TenantId = tn.TenantId
INNER JOIN Properties p ON t.PropertyId = p.PropertyId
WHERE t.StatusCode = 'ACTIVE'
  AND t.LeaseEndDate IS NOT NULL
  AND t.LeaseEndDate BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL {date_range_days} DAY)
{tenancy_rent_filter}ORDER BY t.LeaseEndDate ASC
LIMIT {limit}""",
        
        "maintenance": f"""SELECT 
    m.MaintenanceJobId,
    m.Summary,
    CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
    m.StatusCode,
    m.OpenedAtUtc,
    DATEDIFF(NOW(), m.OpenedAtUtc) AS DaysOpen,
    m.EstimatedCost
FROM MaintenanceJobs m
INNER JOIN Properties p ON m.PropertyId = p.PropertyId
WHERE m.StatusCode IN ('OPEN', 'IN_PROGRESS')
ORDER BY m.OpenedAtUtc ASC
LIMIT {limit}""",
        
        "inspection": f"""SELECT 
    i.InspectionId,
    CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
    i.InspectionType,
    i.ScheduledDate,
    i.ComplianceStatus
FROM Inspections i
INNER JOIN Properties p ON i.PropertyId = p.PropertyId
WHERE {"i.ScheduledDate < CURDATE()" if plan.get("temporal_filter") == "past" else "i.ScheduledDate >= CURDATE()"}
ORDER BY i.ScheduledDate {"DESC" if plan.get("temporal_filter") == "past" else "ASC"}
LIMIT {limit}""",
        
        "owner_statement": f"""SELECT 
    os.OwnerStatementId,
    o.FullName AS OwnerName,
    os.PeriodStart,
    os.PeriodEnd,
    os.GrossIncome,
    os.Expenses,
    os.ManagementFees,
    os.NetPayout
FROM OwnerStatements os
INNER JOIN Owners o ON os.OwnerId = o.OwnerId
ORDER BY os.PeriodEnd DESC
LIMIT {limit}"""
    }
    
    sql = sql_templates.get(domain, 
        f"SELECT * FROM {state['scoped_tables'][0] if state['scoped_tables'] else 'Properties'} LIMIT {limit}")
    
    # Note: Tenant predicate will be injected by .NET firewall
    # LIMIT is added here based on query analysis
    
    state["sql_candidate"] = sql.strip()
    state["needs_clarification"] = False
    state["reasoning"] += f" | Generated SQL template for {domain}"
    return state


def clarification_node(state: AgentState) -> AgentState:
    """Handle cases needing clarification"""
    state["needs_clarification"] = True
    state["clarification_prompt"] = (
        "I need one more detail before I can answer this. "
        "Please specify a date range, property scope, or status."
    )
    state["sql_candidate"] = ""
    state["confidence"] = 0.3
    return state


def should_clarify(state: AgentState) -> str:
    """Route to clarification if confidence too low"""
    return "clarify" if state.get("confidence", 0) < 0.55 else "generate"


# Build the graph
def create_nl2sql_graph():
    workflow = StateGraph(AgentState)
    
    # Add nodes
    workflow.add_node("route", route_domain)
    workflow.add_node("schema", schema_context)
    workflow.add_node("planner", planner)
    workflow.add_node("generate", sql_generator)
    workflow.add_node("clarify", clarification_node)
    
    # Define edges
    workflow.set_entry_point("route")
    workflow.add_edge("route", "schema")
    workflow.add_edge("schema", "planner")
    workflow.add_conditional_edges(
        "planner",
        should_clarify,
        {
            "generate": "generate",
            "clarify": "clarify"
        }
    )
    workflow.add_edge("generate", END)
    workflow.add_edge("clarify", END)
    
    return workflow.compile()


# Global graph instance
nl2sql_graph = create_nl2sql_graph()
