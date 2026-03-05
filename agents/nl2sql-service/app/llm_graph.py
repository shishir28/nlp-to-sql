"""
Multi-agent LLM-powered SQL generation using LangGraph
Phase 2: Includes caching, retry logic, filter extraction, and metrics
Phase 3: Pydantic models for type safety and validation
"""
from typing import Annotated, Optional, Dict, Any, List, TypedDict
from langgraph.graph import StateGraph, END
from langchain_core.messages import HumanMessage, SystemMessage
from app.llm import get_llm_instance
from app.schema_introspection import get_introspector
from app.state_models import LLMAgentState, Domain, QueryComplexity
from app.prompt_library import PromptLibrary
import json
import logging
import re
import hashlib
import time
from dataclasses import dataclass, field
from functools import wraps
from tenacity import (
    retry,
    stop_after_attempt,
    wait_exponential,
    retry_if_exception_type,
    before_sleep_log
)

logger = logging.getLogger(__name__)

# Initialize prompt library for Phase 3
prompt_library = PromptLibrary()

# ============================================================================
# PHASE 2: CACHING, RETRY LOGIC, FILTER EXTRACTION & METRICS
# ============================================================================

class QueryCache:
    """
    Multi-tier caching for SQL query results.
    Uses Redis if available, falls back to in-memory cache.
    """
    def __init__(self, ttl: int = 3600):
        self.ttl = ttl
        self.local_cache = {}  # In-memory fallback
        self.redis = None
        
        # Try to connect to Redis
        try:
            import redis
            redis_url = "redis://redis:6379"  # Docker service name
            self.redis = redis.from_url(redis_url, decode_responses=True)
            self.redis.ping()
            logger.info(f"QueryCache: Connected to Redis at {redis_url}")
        except Exception as e:
            logger.warning(f"QueryCache: Redis not available, using in-memory cache only: {e}")
    
    def cache_key(self, question: str, customer_id: int, schema_version: str = "v1") -> str:
        """Generate cache key from query parameters"""
        key_data = f"{question.lower().strip()}:{customer_id}:{schema_version}"
        return f"nl2sql:{hashlib.sha256(key_data.encode()).hexdigest()}"
    
    def get(self, key: str) -> Optional[Dict]:
        """Get cached result from Redis or local cache"""
        # Try Redis first
        if self.redis:
            try:
                data = self.redis.get(key)
                if data:
                    logger.info(f"Cache HIT (Redis): {key[:16]}...")
                    return json.loads(data)
            except Exception as e:
                logger.warning(f"Redis get error: {e}, falling back to local cache")
        
        # Fall back to local cache
        if key in self.local_cache:
            entry = self.local_cache[key]
            if time.time() - entry['timestamp'] < self.ttl:
                logger.info(f"Cache HIT (local): {key[:16]}...")
                return entry['data']
            else:
                del self.local_cache[key]
        
        logger.info(f"Cache MISS: {key[:16]}...")
        return None
    
    def set(self, key: str, value: Dict):
        """Store result in Redis and local cache"""
        # Store in Redis
        if self.redis:
            try:
                self.redis.setex(key, self.ttl, json.dumps(value))
                logger.debug(f"Cached to Redis: {key[:16]}...")
            except Exception as e:
                logger.warning(f"Redis set error: {e}")
        
        # Always store in local cache as backup
        self.local_cache[key] = {
            'data': value,
            'timestamp': time.time()
        }
        logger.debug(f"Cached locally: {key[:16]}...")


@dataclass
class AgentMetrics:
    """Performance metrics for a single agent execution"""
    agent_name: str
    start_time: float = field(default_factory=time.time)
    end_time: float = 0
    duration_ms: float = 0
    success: bool = True
    error_message: str = ""
    retry_count: int = 0
    
    def complete(self, success: bool = True, error: str = "", retry_count: int = 0):
        self.end_time = time.time()
        self.duration_ms = round((self.end_time - self.start_time) * 1000, 2)
        self.success = success
        self.error_message = error
        self.retry_count = retry_count


class MetricsCollector:
    """Collect and aggregate metrics across all agents"""
    def __init__(self):
        self.metrics: List[AgentMetrics] = []
        self.start_time = time.time()
        self.cache_hit = False
    
    def record(self, agent_name: str) -> AgentMetrics:
        metric = AgentMetrics(agent_name=agent_name)
        self.metrics.append(metric)
        return metric
    
    def summary(self) -> Dict:
        total_duration = round((time.time() - self.start_time) * 1000, 2)
        return {
            "total_duration_ms": total_duration,
            "cache_hit": self.cache_hit,
            "agents": [
                {
                    "name": m.agent_name,
                    "duration_ms": m.duration_ms,
                    "success": m.success,
                    "error": m.error_message,
                    "retry_count": m.retry_count
                }
                for m in self.metrics
            ],
            "success_rate": round(sum(1 for m in self.metrics if m.success) / len(self.metrics), 2) if self.metrics else 1.0
        }


def extract_limit_from_question(question: str, default: int = 10) -> int:
    """
    Extract LIMIT value from natural language question.
    
    Examples:
        "show top 5" → 5
        "give me three inspections" → 3
        "list first 10" → 10
        "all tenancies" → 100
    """
    question_lower = question.lower()
    
    # Pattern 1: "top N", "first N", "last N"
    match = re.search(r'\b(?:top|first|last)\s+(\d+)\b', question_lower)
    if match:
        limit = int(match.group(1))
        logger.info(f"Extracted LIMIT {limit} from pattern 'top/first/last N'")
        return limit
    
    # Pattern 2: Explicit numbers with context
    match = re.search(r'\b(\d+)\s+(?:most|recent|latest|oldest|properties|tenancies|inspections|jobs)\b', question_lower)
    if match:
        limit = int(match.group(1))
        logger.info(f"Extracted LIMIT {limit} from pattern 'N most/recent/latest'")
        return limit
    
    # Pattern 3: Text numbers
    text_numbers = {
        "one": 1, "two": 2, "three": 3, "four": 4, "five": 5,
        "six": 6, "seven": 7, "eight": 8, "nine": 9, "ten": 10,
        "dozen": 12, "twenty": 20, "fifty": 50
    }
    
    for word, num in text_numbers.items():
        pattern = f"\\b{word}\\s+(?:most|recent|latest|oldest|properties|tenancies|inspections|jobs)\\b"
        if re.search(pattern, question_lower):
            logger.info(f"Extracted LIMIT {num} from text number '{word}'")
            return num
    
    # Pattern 4: "all" or "everything" → use higher limit
    if re.search(r'\b(all|every|entire)\b', question_lower):
        logger.info("Extracted LIMIT 100 from 'all/every/entire'")
        return 100
    
    logger.info(f"No LIMIT found, using default: {default}")
    return default


def extract_numeric_filters(question: str) -> Dict[str, Any]:
    """
    Extract numeric comparison filters from question.
    
    Returns dict of filters: {"RentAmount": {"lt": 400}, "Bedrooms": {"gte": 3}}
    """
    filters = {}
    question_lower = question.lower()
    
    # Map common field aliases to actual column names
    field_mapping = {
        'rent': 'RentAmount',
        'price': 'RentAmount',
        'cost': 'EstimatedCost',
        'bedrooms': 'Bedrooms',
        'beds': 'Bedrooms',
        'bathrooms': 'Bathrooms',
        'baths': 'Bathrooms',
        'parking': 'Parking',
        'spaces': 'Parking',
        'arrears': 'BalanceDelta',
        'balance': 'BalanceDelta',
    }
    
    # Patterns for different operators (order matters - check specific before general)
    patterns = {
        'between': [
            r'(\w+)\s+between\s+(\d+(?:\.\d+)?)\s+and\s+(\d+(?:\.\d+)?)',
            r'(\d+(?:\.\d+)?)\s+to\s+(\d+(?:\.\d+)?)\s+(\w+)',
        ],
        'lte': [
            r'(\w+)\s+(?:at most|up to|max|maximum)\s+(\d+(?:\.\d+)?)',
            r'(?:at most|up to|max|maximum)\s+(\d+(?:\.\d+)?)\s+(\w+)',
        ],
        'gte': [
            r'(\w+)\s+(?:at least|minimum|min)\s+(\d+(?:\.\d+)?)',
            r'(?:at least|minimum|min)\s+(\d+(?:\.\d+)?)\s+(\w+)',
        ],
        'lt': [
            r'(\w+)\s+(?:less than|under|below|<)\s+(\d+(?:\.\d+)?)',
            r'(?:less than|under|below)\s+(\d+(?:\.\d+)?)\s+(\w+)',
        ],
        'gt': [
            r'(\w+)\s+(?:greater than|over|above|more than|>)\s+(\d+(?:\.\d+)?)',
            r'(?:greater than|over|above|more than)\s+(\d+(?:\.\d+)?)\s+(\w+)',
        ],
        'eq': [
            r'(\w+)\s+(?:equals|is|=)\s+(\d+(?:\.\d+)?)',
            r'exactly\s+(\d+(?:\.\d+)?)\s+(\w+)',
        ],
    }
    
    for operator, pattern_list in patterns.items():
        for pattern in pattern_list:
            matches = re.finditer(pattern, question_lower)
            for match in matches:
                groups = match.groups()
                
                if operator == 'between':
                    if len(groups) == 3:
                        if groups[0].replace('.', '').isdigit():
                            # Pattern: "500 to 1000 rent"
                            val1, val2, field = groups
                        else:
                            # Pattern: "rent between 500 and 1000"
                            field, val1, val2 = groups
                        field = field_mapping.get(field, field)
                        filters[field] = {'between': [float(val1), float(val2)]}
                        logger.info(f"Extracted filter: {field} BETWEEN {val1} AND {val2}")
                else:
                    # Determine if first group is value or field
                    if groups[0].replace('.', '').replace('-', '').isdigit():
                        value, field = groups
                    else:
                        field, value = groups
                    
                    field = field_mapping.get(field, field)
                    if field not in filters:
                        filters[field] = {}
                    filters[field][operator] = float(value)
                    logger.info(f"Extracted filter: {field} {operator} {value}")
    
    return filters


def create_retry_decorator():
    """Create retry decorator for LLM calls with exponential backoff"""
    return retry(
        stop=stop_after_attempt(3),
        wait=wait_exponential(multiplier=1, min=2, max=10),
        retry=retry_if_exception_type((Exception,)),  # Retry on any exception
        before_sleep=before_sleep_log(logger, logging.WARNING),
        reraise=True
    )


# Create retry decorator instance
_retry_llm_call = create_retry_decorator()


def call_llm_with_retry(llm, messages: list, metric: Optional[AgentMetrics] = None):
    """
    Call LLM with automatic retry on failures.
    Tracks retry count in metrics if provided.
    """
    retry_count = 0
    
    @_retry_llm_call
    def _invoke():
        nonlocal retry_count
        response = llm.invoke(messages)
        
        # Validate response
        if not response.content or len(response.content.strip()) < 10:
            retry_count += 1
            raise ValueError("Empty or invalid LLM response")
        
        return response
    
    try:
        response = _invoke()
        if metric:
            metric.retry_count = retry_count
        return response
    except Exception as e:
        if metric:
            metric.retry_count = retry_count
        raise


# Global cache instance (initialized once)
_query_cache = None

def get_query_cache() -> QueryCache:
    """Get or create global query cache instance"""
    global _query_cache
    if _query_cache is None:
        _query_cache = QueryCache(ttl=3600)  # 1 hour TTL
    return _query_cache

# ============================================================================
# END OF PHASE 2 ADDITIONS
# ============================================================================

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


# ============================================================================
# STATE DEFINITION (Phase 3: TypedDict for LangGraph, Pydantic for validation)
# ============================================================================
# LangGraph requires TypedDict or similar for state schema
# We use Pydantic LLMAgentState only for initial validation in main.py
class LLMAgentStateDict(TypedDict, total=False):
    """State dict passed between agents in LangGraph"""
    # Required fields
    question: str
    customer_id: str
    user_id: str
    role: str
    dialect: str
    tenant_column: str
    default_limit: int
    max_limit: int
    allowed_tables: List[str]
    allowed_functions: List[str]
    
    # Agent outputs
    detected_domain: str
    domain_confidence: float
    scoped_tables: List[str]
    schema_description: str
    query_plan: str
    sql_candidate: str
    validation_notes: str
    confidence: float
    reasoning: str
    needs_clarification: bool
    clarification_prompt: str
    
    # Phase 2: Performance tracking
    metrics_collector: Any
    extracted_limit: int
    extracted_filters: Dict[str, Any]
    
    # Phase 3: Routing & optimization
    query_complexity: str
    agent_route: str
    prompt_version: str

    # Parallel prefetch
    prefetched_fk_map: Dict[str, Any]

    # Conversation memory
    conversation_history: List[Dict[str, Any]]


def schema_prefetch_agent(state: Dict[str, Any]) -> Dict[str, Any]:
    """
    Parallel Agent: Schema Prefetch
    Runs concurrently with domain_classifier to pre-load FK relationships from
    INFORMATION_SCHEMA so schema_analyzer has them ready without waiting for an LLM call.
    """
    metric = state.get('metrics_collector').record('schema_prefetch') if state.get('metrics_collector') else None
    logger.info("[Schema Prefetch] Pre-loading schema FK relationships")

    try:
        introspector = get_introspector()
        allowed_tables = state.get("allowed_tables", [])

        # Eagerly warm the FK cache for all allowed tables
        if introspector._info_schema is not None:
            fk_map = introspector._info_schema._load_fk_map()
            prefetched = {t: fk_map.get(t, []) for t in allowed_tables}
            state["prefetched_fk_map"] = prefetched
            logger.info(f"[Schema Prefetch] FK map warmed for {len(prefetched)} tables")
        else:
            state["prefetched_fk_map"] = {}
            logger.info("[Schema Prefetch] DB unavailable, skipping FK prefetch")

        if metric:
            metric.complete(success=True)

    except Exception as e:
        logger.warning(f"[Schema Prefetch] Failed: {e}")
        state["prefetched_fk_map"] = {}
        if metric:
            metric.complete(success=False, error=str(e))

    return state


def domain_classifier_agent(state: Dict[str, Any]) -> Dict[str, Any]:
    """
    Agent 1: Domain Classification
    Uses LLM to classify the question into a domain and identify relevant tables.
    Phase 2: Includes metrics tracking and retry logic
    Phase 3: State validated by Pydantic in main.py, received as dict from LangGraph
    """
    # Phase 2: Start metrics tracking
    metric = state.get('metrics_collector').record('domain_classifier') if state.get('metrics_collector') else None
    
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

Respond in JSON format:
{{
    "domain": "domain_name",
    "confidence": 0.95,
    "tables": ["Table1", "Table2"],
    "reasoning": "Brief explanation"
}}"""
    
    history_block = ""
    if state.get("conversation_history"):
        from app.conversation_store import get_conversation_store
        history_text = get_conversation_store().format_history_for_prompt(state["conversation_history"])
        history_block = f"\n\nConversation history (for context):\n{history_text}\n"

    user_prompt = f"""Question: {state['question']}{history_block}
Available tables: {', '.join(state['allowed_tables'])}

Classify the domain and identify relevant tables. Only use the listed available tables."""
    
    try:
        logger.debug(f"[Domain Classifier] Calling LLM with {len(state['allowed_tables'])} tables")
        # Phase 2: Use retry wrapper for LLM call
        response = call_llm_with_retry(llm, [
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ], metric)
        
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
        
        # Phase 2: Complete metrics on success
        if metric:
            metric.complete(success=True)
        
    except Exception as e:
        # Fallback on error
        logger.error(f"[Domain Classifier] LLM invocation failed: {str(e)}", exc_info=True)
        state["detected_domain"] = "general"
        state["domain_confidence"] = 0.5
        state["reasoning"] = f"LLM error, using fallback: {str(e)}"
        
        # Phase 2: Complete metrics on failure
        if metric:
            metric.complete(success=False, error=str(e))
    
    return state


def schema_analyzer_agent(state: Dict[str, Any]) -> Dict[str, Any]:
    """
    Agent 2: Schema Analysis & Query Planning
    Uses LLM to understand the schema and create a detailed query plan.
    Phase 2: Includes metrics tracking and retry logic
    Phase 3: State validated by Pydantic in main.py, received as dict from LangGraph
    """
    # Phase 2: Start metrics tracking
    metric = state.get('metrics_collector').record('schema_analyzer') if state.get('metrics_collector') else None
    
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
        # Phase 2: Use retry wrapper for LLM call
        response = call_llm_with_retry(llm, [
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ], metric)
        
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
        
        # Phase 2: Complete metrics on success
        if metric:
            metric.complete(success=True)
        
    except Exception as e:
        logger.error(f"[Schema Analyzer] LLM invocation failed: {str(e)}", exc_info=True)
        state["query_plan"] = "Error creating plan"
        state["reasoning"] += f" | Planning error: {str(e)}"
        
        # Phase 2: Complete metrics on failure
        if metric:
            metric.complete(success=False, error=str(e))
    
    return state


def sql_generator_agent(state: Dict[str, Any]) -> Dict[str, Any]:
    """
    Agent 3: SQL Generation
    Uses LLM to generate SQL based on the query plan and schema.
    Phase 2: Includes metrics tracking, retry logic, and filter extraction
    Phase 3: State validated by Pydantic in main.py, received as dict from LangGraph
    """
    # Phase 2: Start metrics tracking
    metric = state.get('metrics_collector').record('sql_generator') if state.get('metrics_collector') else None
    
    logger.info("[SQL Generator] Generating SQL from query plan")
    llm = get_llm_instance()
    
    if llm is None:
        logger.info("[SQL Generator] Using template-based fallback (no LLM)")
        # Fallback to templates
        from app.graph import sql_generator as template_generator
        return template_generator(state)
    
    # Phase 2: Build filter guidance from extracted filters
    filter_guidance = ""
    extracted_filters = state.get('extracted_filters', {})
    if extracted_filters:
        filter_guidance = "\n\n**EXTRACTED FILTERS (MUST include in WHERE clause)**:\n"
        for field, conditions in extracted_filters.items():
            for op, val in conditions.items():
                if op == 'between':
                    filter_guidance += f"- {field} BETWEEN {val[0]} AND {val[1]}\n"
                else:
                    op_sql = {'lt': '<', 'lte': '<=', 'gt': '>', 'gte': '>=', 'eq': '='}[op]
                    filter_guidance += f"- {field} {op_sql} {val}\n"
        logger.info(f"[SQL Generator] Including extracted filters: {list(extracted_filters.keys())}")
    
    # Phase 2: Use extracted limit
    limit_value = state.get('extracted_limit', state['default_limit'])
    max_limit = state.get('max_limit', state['default_limit'])
    if limit_value > max_limit:
        logger.info(f"[SQL Generator] Capping extracted limit {limit_value} to max_limit {max_limit}")
        limit_value = max_limit
    
    allowed_functions = state.get('allowed_functions', [])
    functions_hint = ", ".join(allowed_functions) if allowed_functions else "COUNT, SUM, AVG, MIN, MAX, CONCAT, DATEDIFF, DATE_ADD"

    system_prompt = f"""You are an expert SQL generator for MySQL 8.4.
Generate clean, efficient SQL queries following these rules:

1. **CRITICAL**: DO NOT add tenant filtering (WHERE {{tenant_column}} = X) - this is injected separately
2. Use EXACT column names from the schema below (case-sensitive!)
3. Use proper JOINs with table aliases  
4. Use CONCAT for address formatting
5. Use DATEDIFF, CURDATE(), DATE_ADD for date calculations
6. Always include LIMIT clause
7. Format SQL with proper indentation
8. Only use these SQL functions: {functions_hint}

{SCHEMA_DEFINITION}

Return ONLY the SQL query, no explanation or markdown."""
    
    history_block = ""
    if state.get("conversation_history"):
        from app.conversation_store import get_conversation_store
        history_text = get_conversation_store().format_history_for_prompt(state["conversation_history"])
        history_block = f"\n\nConversation history (prior turns for context):\n{history_text}\n"

    user_prompt = f"""Generate MySQL SQL for this question:
Question: {state['question']}{history_block}
Query Plan: {state['query_plan']}

Relevant Tables: {', '.join(state['scoped_tables'])}
LIMIT to use: {limit_value}{filter_guidance}

Generate the SQL now using EXACT column names from the schema."""
    
    try:
        logger.debug(f"[SQL Generator] Calling LLM with temperature=0, LIMIT={limit_value}, filters={list(extracted_filters.keys())}")
        
        # Override temperature to 0 for SQL generation (deterministic)
        from langchain_openai import ChatOpenAI
        if isinstance(llm, ChatOpenAI):
            sql_llm = llm.bind(temperature=0.0)
        else:
            sql_llm = llm
        
        # Phase 2: Use retry wrapper for LLM call
        response = call_llm_with_retry(sql_llm, [
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ], metric)
        
        sql = response.content.strip()
        logger.debug(f"[SQL Generator] Raw LLM response length: {len(sql)} chars")
        
        # Clean up markdown code blocks if present (robust regex approach)
        sql = re.sub(r'^```(?:sql)?\s*', '', sql, flags=re.IGNORECASE)
        sql = re.sub(r'```\s*$', '', sql)
        sql = sql.strip()
        
        state["sql_candidate"] = sql
        state["reasoning"] += " | LLM-generated SQL"
        
        logger.info(f"[SQL Generator] Generated SQL ({len(sql)} chars): {sql[:100]}...")
        
        # Phase 2: Complete metrics on success
        if metric:
            metric.complete(success=True)
        
    except Exception as e:
        logger.error(f"[SQL Generator] LLM invocation failed: {str(e)}", exc_info=True)
        state["sql_candidate"] = f"-- Error generating SQL: {str(e)}"
        state["reasoning"] += f" | SQL generation error: {str(e)}"
        
        # Phase 2: Complete metrics on failure
        if metric:
            metric.complete(success=False, error=str(e))
    
    return state


def sql_validator_agent(state: Dict[str, Any]) -> Dict[str, Any]:
    """
    Agent 4: SQL Validation & Refinement
    Uses LLM to review and validate the generated SQL.
    Phase 2: Includes metrics tracking and retry logic
    Phase 3: State validated by Pydantic in main.py, received as dict from LangGraph
    """
    # Phase 2: Start metrics tracking
    metric = state.get('metrics_collector').record('sql_validator') if state.get('metrics_collector') else None
    
    logger.info("[SQL Validator] Validating generated SQL")
    llm = get_llm_instance()
    
    if llm is None:
        logger.info("[SQL Validator] Skipping validation (no LLM)")
        state["validation_notes"] = "Validation skipped (no LLM)"
        state["confidence"] = 0.7
        state["needs_clarification"] = False
        return state
    
    # Phase 3: Load prompts from YAML templates
    try:
        system_prompt = prompt_library.render_prompt(
            'sql_validator',
            'system',
            tenant_column=state.get('tenant_column', 'CustomerId'),
            mysql_version='8.4'
        )
        user_prompt = prompt_library.render_prompt(
            'sql_validator',
            'user',
            question=state['question'],
            sql_candidate=state['sql_candidate'],
            schema_description="All tables have CustomerId. Common JOINs use: table1.ForeignKeyId = table2.PrimaryKeyId AND table1.CustomerId = table2.CustomerId",
            tenant_column=state.get('tenant_column', 'CustomerId'),
            scoped_tables=state.get('scoped_tables', []),
            detected_domain=state.get('detected_domain', 'unknown'),
            role=state.get('role', 'user'),
            default_limit=state.get('default_limit', 50)
        )
    except Exception as e:
        logger.warning(f"[SQL Validator] Failed to load YAML prompts, using fallback: {e}")
        # Fallback to hardcoded prompts if YAML loading fails
        system_prompt = """You are a SQL validation and auto-correction expert for MySQL 8.4.
Your goal is to FIX SQL issues automatically, NOT just flag them.

Review the generated SQL for:
1. **Syntax correctness** - Fix any syntax errors immediately
2. **Missing JOINs** - If tables are referenced but not joined, add the proper JOINs
3. **Logical correctness** - Does it answer the question?
4. **Security** - Tenant filtering is added separately, don't worry about it
5. **MySQL compatibility** - MySQL supports HAVING with column aliases, aggregate functions in HAVING, etc.

**IMPORTANT RULES**:
- If you find fixable SQL issues (missing JOINs, syntax errors), provide revised_sql with corrections
- Only set is_valid=false if the QUESTION is ambiguous and cannot be answered
- MySQL 8.4 supports: HAVING with aliases, CTEs, window functions, JSON functions
- Confidence below 0.8 should trigger auto-correction, not clarification requests

Respond in JSON format:
{{
    "is_valid": true/false,
    "confidence": 0.95,
    "issues": ["issue1", "issue2"],
    "fixes_applied": ["fix1", "fix2"],
    "revised_sql": "CORRECTED SQL if any issues found, or null if perfect"
}}

If you provide revised_sql, the issues should be FIXED in that SQL."""
        
        user_prompt = f"""Validate and auto-correct this SQL if needed:

Question: {state['question']}
Generated SQL:
{state['sql_candidate']}

Schema context: All tables have CustomerId. Common JOINs use: table1.ForeignKeyId = table2.PrimaryKeyId AND table1.CustomerId = table2.CustomerId

AUTO-FIX any issues and provide revised_sql."""
    
    try:
        logger.debug("[SQL Validator] Calling LLM for validation")
        # Phase 2: Use retry wrapper for LLM call
        response = call_llm_with_retry(llm, [
            SystemMessage(content=system_prompt),
            HumanMessage(content=user_prompt)
        ], metric)
        
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
        fixes_applied = result.get("fixes_applied", [])
        if result.get("revised_sql"):
            logger.info(f"[SQL Validator] Auto-corrected SQL. Fixes: {', '.join(fixes_applied) if fixes_applied else 'general improvements'}")
            state["sql_candidate"] = result["revised_sql"]
            state["reasoning"] += " | SQL auto-corrected by validator"
            # Boost confidence if auto-correction was successful
            if state["confidence"] < 0.8:
                state["confidence"] = 0.85
                logger.info("[SQL Validator] Confidence boosted to 0.85 after auto-correction")
        
        # Only request clarification if question is truly ambiguous AND no fix was possible
        # If revised_sql was provided, trust the auto-correction
        has_auto_fix = result.get("revised_sql") is not None
        is_question_ambiguous = not result.get("is_valid", True)
        
        # Request clarification only when question is ambiguous and we couldn't auto-fix
        state["needs_clarification"] = is_question_ambiguous and not has_auto_fix and state["confidence"] < 0.6
        
        if state["needs_clarification"]:
            issues = result.get("issues", [])
            details = ", ".join(issues) if issues else "the timeframe, status, or property scope"
            state["clarification_prompt"] = (
                "I need one more detail before I can run this safely. "
                f"Please clarify {details}."
            )
            logger.warning(f"[SQL Validator] Clarification needed: {state['clarification_prompt']}")
        else:
            status = "auto-corrected and validated" if has_auto_fix else "validated"
            logger.info(f"[SQL Validator] SQL {status} - confidence {state['confidence']:.2f}")
        
        # Phase 2: Complete metrics on success
        if metric:
            metric.complete(success=True)
        
    except Exception as e:
        logger.error(f"[SQL Validator] LLM invocation failed: {str(e)}", exc_info=True)
        state["validation_notes"] = f"Validation error: {str(e)}"
        state["confidence"] = 0.5
        state["needs_clarification"] = False
        
        # Phase 2: Complete metrics on failure
        if metric:
            metric.complete(success=False, error=str(e))
    
    return state


def should_clarify(state: Dict[str, Any]) -> str:
    """Route to clarification if validation failed or confidence too low"""
    return "clarify" if state.get("needs_clarification", False) else "end"


def clarification_agent(state: Dict[str, Any]) -> Dict[str, Any]:
    """Handle cases needing user clarification"""
    logger.warning(f"[Clarification] Query needs user clarification: {state.get('clarification_prompt', 'Unknown issue')}")
    state["sql_candidate"] = ""
    state["reasoning"] += " | Needs clarification"
    return state


def parallel_fan_in(state: Dict[str, Any]) -> Dict[str, Any]:
    """
    Merge node: waits for both domain_classifier and schema_prefetch to complete
    before forwarding state to schema_analyzer. No transformation needed —
    LangGraph merges parallel branch outputs into a single state dict automatically.
    """
    logger.info("[Fan-in] Parallel branches merged — proceeding to schema_analyzer")
    return state


# Build the multi-agent graph
def create_llm_agent_graph():
    logger.info("[Graph] Initializing LLM multi-agent graph")
    # Phase 3: Use TypedDict for LangGraph, Pydantic LLMAgentState used only for validation in main.py
    workflow = StateGraph(LLMAgentStateDict)

    # Add agent nodes
    workflow.add_node("domain_classifier", domain_classifier_agent)
    workflow.add_node("schema_prefetch", schema_prefetch_agent)
    workflow.add_node("fan_in", parallel_fan_in)
    workflow.add_node("schema_analyzer", schema_analyzer_agent)
    workflow.add_node("sql_generator", sql_generator_agent)
    workflow.add_node("sql_validator", sql_validator_agent)
    workflow.add_node("clarify", clarification_agent)

    # Parallel fan-out: domain_classifier and schema_prefetch run concurrently
    workflow.set_entry_point("domain_classifier")
    workflow.add_edge("domain_classifier", "fan_in")

    # schema_prefetch starts at the same time — wire it from START via a second entry
    from langgraph.graph import START
    workflow.add_edge(START, "schema_prefetch")
    workflow.add_edge("schema_prefetch", "fan_in")

    # Fan-in → sequential pipeline
    workflow.add_edge("fan_in", "schema_analyzer")
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

    logger.info("[Graph] Multi-agent graph compiled successfully with parallel fan-out")
    return workflow.compile()


# Global graph instance
logger.info("[System] Creating global LLM agent graph instance")
llm_nl2sql_graph = create_llm_agent_graph()
