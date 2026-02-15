"""
Multi-agent LLM-powered SQL generation using LangGraph
"""
from typing import TypedDict, Annotated
from langgraph.graph import StateGraph, END
from langchain_core.messages import HumanMessage, SystemMessage
from app.llm import get_llm_instance
from app.schema_introspection import get_introspector
import json


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
    llm = get_llm_instance()
    
    # Fallback to template-based if no LLM
    if llm is None:
        introspector = get_introspector()
        domain_info = introspector.get_table_info_for_question(
            state["question"], 
            state["allowed_tables"]
        )
        state["detected_domain"] = domain_info["domain"]
        state["domain_confidence"] = domain_info.get("confidence", 0.7)
        state["reasoning"] = f"Template-based: {domain_info['reason']}"
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
        response = llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ])
        
        result = json.loads(response.content)
        state["detected_domain"] = result["domain"]
        state["domain_confidence"] = result["confidence"]
        state["scoped_tables"] = [t for t in result["tables"] if t in state["allowed_tables"]]
        state["reasoning"] = f"LLM Classification: {result['reasoning']}"
        
    except Exception as e:
        # Fallback on error
        state["detected_domain"] = "general"
        state["domain_confidence"] = 0.5
        state["reasoning"] = f"LLM error, using fallback: {str(e)}"
    
    return state


def schema_analyzer_agent(state: LLMAgentState) -> LLMAgentState:
    """
    Agent 2: Schema Analysis & Query Planning
    Uses LLM to understand the schema and create a detailed query plan.
    """
    llm = get_llm_instance()
    
    if llm is None:
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
        response = llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ])
        
        result = json.loads(response.content)
        state["query_plan"] = result["query_plan"]
        state["schema_description"] = json.dumps(result, indent=2)
        state["reasoning"] += f" | LLM Plan: {result['query_plan'][:100]}..."
        
    except Exception as e:
        state["query_plan"] = "Error creating plan"
        state["reasoning"] += f" | Planning error: {str(e)}"
    
    return state


def sql_generator_agent(state: LLMAgentState) -> LLMAgentState:
    """
    Agent 3: SQL Generation
    Uses LLM to generate SQL based on the query plan and schema.
    """
    llm = get_llm_instance()
    
    if llm is None:
        # Fallback to templates
        from app.graph import sql_generator as template_generator
        return template_generator(state)
    
    system_prompt = """You are an expert SQL generator for MySQL 8.4.
Generate clean, efficient SQL queries following these rules:

1. SECURITY: DO NOT add tenant filtering (WHERE {tenant_column} = X) - this will be injected separately
2. Use proper JOINs with table aliases
3. Use CONCAT for address formatting
4. Use DATEDIFF, CURDATE(), DATE_ADD for date calculations
5. Always include LIMIT clause
6. Use proper column names (case-sensitive)
7. Format SQL nicely with indentation

Return ONLY the SQL query, no explanation."""
    
    user_prompt = f"""Generate SQL for this question:
Question: {state['question']}

Query Plan: {state['query_plan']}

Tables: {', '.join(state['scoped_tables'])}
Default LIMIT: {state['default_limit']}

Generate the SQL query now."""
    
    try:
        response = llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ])
        
        sql = response.content.strip()
        # Clean up markdown code blocks if present
        if sql.startswith("```sql"):
            sql = sql.split("```sql")[1].split("```")[0].strip()
        elif sql.startswith("```"):
            sql = sql.split("```")[1].split("```")[0].strip()
        
        state["sql_candidate"] = sql
        state["reasoning"] += " | LLM-generated SQL"
        
    except Exception as e:
        state["sql_candidate"] = f"-- Error generating SQL: {str(e)}"
        state["reasoning"] += f" | SQL generation error: {str(e)}"
    
    return state


def sql_validator_agent(state: LLMAgentState) -> LLMAgentState:
    """
    Agent 4: SQL Validation & Refinement
    Uses LLM to review and validate the generated SQL.
    """
    llm = get_llm_instance()
    
    if llm is None:
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
        response = llm.invoke([
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ])
        
        result = json.loads(response.content)
        state["validation_notes"] = json.dumps(result, indent=2)
        state["confidence"] = result["confidence"]
        
        # Use revised SQL if provided
        if result.get("revised_sql"):
            state["sql_candidate"] = result["revised_sql"]
            state["reasoning"] += " | SQL revised by validator"
        
        # Check if clarification needed
        state["needs_clarification"] = not result["is_valid"] or result["confidence"] < 0.6
        
        if state["needs_clarification"]:
            state["clarification_prompt"] = f"Issues with query: {', '.join(result['issues'])}"
        
    except Exception as e:
        state["validation_notes"] = f"Validation error: {str(e)}"
        state["confidence"] = 0.5
        state["needs_clarification"] = False
    
    return state


def should_clarify(state: LLMAgentState) -> str:
    """Route to clarification if validation failed or confidence too low"""
    return "clarify" if state.get("needs_clarification", False) else "end"


def clarification_agent(state: LLMAgentState) -> LLMAgentState:
    """Handle cases needing user clarification"""
    state["sql_candidate"] = ""
    state["reasoning"] += " | Needs clarification"
    return state


# Build the multi-agent graph
def create_llm_agent_graph():
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
    
    return workflow.compile()


# Global graph instance
llm_nl2sql_graph = create_llm_agent_graph()
