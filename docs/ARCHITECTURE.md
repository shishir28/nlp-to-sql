# 🏗️ Architecture Overview

For a developer-focused message/control-flow view, see [MESSAGE_FLOW.md](MESSAGE_FLOW.md).

## System Design

This is a **multi-tenant Australian Property Management NL→SQL analytics application** with a three-service architecture enforcing strict security boundaries.

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER BROWSER                             │
│                    http://localhost:4200                         │
└───────────────────────────────┬─────────────────────────────────┘
                                │
                                │ HTTPS (JWT in future)
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      ANGULAR 19 FRONTEND                         │
│  • Standalone components                                         │
│  • HttpClient for API calls                                      │
│  • No direct database access                                     │
│  • Displays rows + plain-English explanation (never raw SQL)     │
│  • Responsive gradient UI                                        │
└───────────────────────────────┬─────────────────────────────────┘
                                │
                                │ HTTP POST /api/query
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│              .NET 8 ASP.NET CORE API (Trust Boundary)           │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ 1. Extract customerId from JWT (dev: hardcoded "1")        │ │
│  │ 2. Load schema policy from JSON                             │ │
│  │ 3. Call Python agent for SQL candidate                      │ │
│  │ 4. Run SQL Firewall (validate + inject tenant predicate)    │ │
│  │ 5. Execute approved SQL with Dapper                          │ │
│  │ 6. Call Python summarizer for NL result summary (optional)  │ │
│  │ 7. Audit log to console                                      │ │
│  │ 8. Return QueryResponse (rows + explanation + summary)      │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                   │
│  Security Enforced Here:                                         │
│  ✓ CustomerId ALWAYS from auth, never from user input           │
│  ✓ SQL firewall blocks mutations (INSERT/UPDATE/DELETE)         │
│  ✓ Tenant predicate injected: WHERE CustomerId = @customerId    │
│  ✓ LIMIT enforcement + max-limit capping                         │
│  ✓ JOIN depth enforcement (maxJoinDepth from policy)             │
│  ✓ Table/function allowlists from policy                         │
│  ✓ MySQL-only dialect enforcement                                 │
│  ✓ Python service NEVER gets DB credentials                      │
└────────────┬──────────────────────────────────┬─────────────────┘
             │                                  │
             │ HTTP POST                        │ SQL Queries
             │ /v1/nl2sql/generate              │ (parameterized)
             ▼                                  ▼
┌─────────────────────────────┐  ┌────────────────────────────────┐
│  PYTHON LANGGRAPH SERVICE   │  │     MYSQL 8.4 IN DOCKER       │
│  http://localhost:8000      │  │     localhost:3306            │
│                             │  │                                │
│  LangGraph State Machine:   │  │  Multi-tenant schema:          │
│  ┌──────────────────────┐   │  │  • Customers (tenant root)     │
│  │ domain_classifier ──┐│   │  │  • Properties                  │
│  │ schema_prefetch   ──┘│   │  │  • Tenancies                   │
│  │   (parallel)         │   │  │  • RentLedgerEntries           │
│  ├──────────────────────┤   │  │  • MaintenanceJobs             │
│  │ schema_analyzer      │   │  │  • Inspections                 │
│  ├──────────────────────┤   │  │  • OwnerStatements             │
│  │ sql_generator        │   │  │  • Vendors                     │
│  ├──────────────────────┤   │  │  • Owners / OwnerStatements    │
│  │ sql_validator        │   │  │                                │
│  └──────────────────────┘   │  │  All tenant tables have:       │
│                             │  │  • CustomerId column           │
│  + /v1/nl2sql/summarize     │  │  • Composite indexes           │
│  + /v1/nl2sql/stream (SSE)  │  │                                │
│                             │  │                                │
│  Returns:                   │  │  Migration: Flyway             │
│  • sql_candidate (string)   │  │  Seeding: Bogus (10K+ rows)    │
│  • confidence (0.0-1.0)     │  │                                │
│  • needs_clarification      │  │                                │
│  • clarification_prompt      │  │                                │
│  • reasoning (internal)      │  │                                │
└─────────────────────────────┘  └────────────────────────────────┘
```

## Data Flow

### Happy Path: User Query → SQL Execution

```
1. User types: "Show active tenancies ending in next 60 days"
   └─▶ Angular: POST /api/query { question: "..." }

2. .NET API Controller:
   └─▶ Extract customerId="1" from User.FindFirst("customer_id")
   └─▶ Call orchestrator.HandleAsync()

3. NlSqlOrchestrator:
   ├─▶ Step 1: Call Python agent
   │   └─▶ HTTP POST http://localhost:8000/v1/nl2sql/generate
   │       {
   │         "question": "Show active tenancies ending in next 60 days",
   │         "context": { "customer_id": "1", "role": "PropertyManager" },
   │         "constraints": {
   │           "dialect": "mysql8",
   │           "tenant_column": "CustomerId",
   │           "default_limit": 50,
   │           "max_limit": 200,
   │           "allowed_tables": [...],
   │           "allowed_functions": [...]
   │         }
   │       }
   │
   ├─▶ Step 2: Agent returns
   │   {
   │     "sql_candidate": "SELECT t.TenancyId, ... FROM Tenancies t ...",
   │     "confidence": 0.7,
   │     "needs_clarification": false
   │   }
   │
   ├─▶ Step 3: SQL Firewall validates + rewrites
   │   └─▶ Check SELECT-only ✓
   │   └─▶ Validate tables (Tenancies, Tenants, Properties) ✓
   │   └─▶ Inject tenant predicate: "WHERE CustomerId = @customerId AND ..."
   │   └─▶ Inject LIMIT: "... LIMIT 50"
   │   └─▶ Returns: { approved: true, rewritten_sql: "..." }
   │
   ├─▶ Step 4: QueryExecutor runs SQL
   │   └─▶ MySqlConnection with timeout
   │   └─▶ Dapper QueryAsync with parameters: { customerId: "1" }
   │   └─▶ Returns: (rows, columns, executionMs)
   │
   ├─▶ Step 5: Audit log
   │   └─▶ ILogger: "AUDIT {...}"
   │
   └─▶ Step 6: Summarization (best-effort)
       └─▶ HTTP POST http://localhost:8000/v1/nl2sql/summarize
           { question, detected_domain, row_count, rows[0..9] }
       └─▶ Returns: { nl_summary: "3 tenancies are expiring..." }

4. API returns QueryResponse:
   {
     "rows": [...],
     "columns": [...],
     "rowCount": 12,
     "executionMs": 245,
     "status": "ok",
     "explanation": "Ran a tenancy query ...",
     "domain": "tenancy",
     "nlSummary": "3 tenancies are expiring in the next 30 days..."
   }

5. Angular displays results table + NL summary
```

## Security Model

### Trust Boundary

**The .NET API is the only trusted component.**

- **✅ Trusted**: .NET API
  - Has database credentials
  - Enforces all security rules
  - Only component that can execute SQL
  - Injects CustomerId from authenticated context

- **❌ Untrusted**: Python Agent Service
  - No database credentials
  - Returns plain SQL strings
  - Treated as "suggestion generator"
  - Cannot execute queries directly

- **❌ Untrusted**: Angular Frontend
  - No database credentials
  - Can only call API via HTTP
  - Never receives SQL candidate/approved SQL
  - Cannot bypass firewall

### Multi-Tenancy Enforcement

#### Rule: CustomerId ALWAYS from Auth, NEVER from User

```csharp
// ✅ CORRECT - from auth claim
var customerId = User.FindFirst("customer_id")?.Value ?? "1";

// ❌ WRONG - from user input (allows tenant hopping!)
var customerId = request.CustomerId; // NEVER DO THIS
```

#### Rule: WHERE CustomerId = @customerId Injected by Firewall

User's SQL candidate (from agent):
```sql
SELECT * FROM Properties WHERE StateCode = 'NSW'
```

After firewall rewrite:
```sql
SELECT * FROM Properties WHERE CustomerId = @customerId AND StateCode = 'NSW' LIMIT 50
```

Execution parameters:
```csharp
{ "customerId": "1" } // From JWT, not user input
```

### SQL Firewall Rules

Implemented in `Infrastructure/Security/SqlFirewall.cs`:

| Rule | Check | Violation Action |
|------|-------|------------------|
| R000 | Non-empty SQL | Reject |
| R001 | SELECT-only (regex) | Reject if INSERT/UPDATE/DELETE/DROP/etc found |
| R002 | Single statement | Reject if semicolon-separated statements |
| R003 | Table allowlist | Reject if table not in schema-policy.json |
| R004 | Max join depth | Reject if JOIN count > `maxJoinDepth` |
| R006 | Tenant predicate injection | Always inject `WHERE CustomerId = @customerId` |
| R007 | LIMIT enforcement | Inject default LIMIT or cap to policy max |
| R009 | Forbidden patterns | Reject if `--`, `/**/`, `UNION`, `INFORMATION_SCHEMA` found |

### Schema Policy

Defined in `db/policy/schema-policy.json`:

```json
{
  "version": "1.0.0",
  "dialect": "mysql8",
  "selectOnly": true,
  "tenantColumn": "CustomerId",
  "allowedTables": [
    "Customers", "Properties", "Tenancies", "RentLedgerEntries",
    "MaintenanceJobs", "Inspections", "OwnerStatements", ...
  ],
  "allowedFunctions": [
    "COUNT", "SUM", "AVG", "MAX", "MIN", "CONCAT", "DATE_ADD", ...
  ],
  "forbiddenPatterns": [
    "--", "/*", "*/", "UNION", "INFORMATION_SCHEMA", "mysql.", "sys."
  ],
  "limitPolicy": {
    "default": 50,
    "max": 200
  },
  "maxJoinDepth": 4
}
```

## Component Responsibilities

### Angular Frontend
- **Purpose**: User interface
- **Responsibilities**:
  - Render NL query input with SSE streaming status display (live agent progress)
  - Display results in table with column sorting, export CSV, and row limit (25 shown, drill-down to full)
  - Display clarification prompts and plain-English NL summary (never raw SQL)
  - Handle loading/error states with per-widget feedback
  - Provide example queries and multi-turn conversation history
  - **Dashboard tab**: Pinned widget grid (2-column, drag-to-reorder) — each widget runs its own query and persists view type to DB
  - **Chart visualizations**: Bar, line, pie, donut, scatter, heatmap via Chart.js (`ChartWidgetComponent`)
  - **Saved queries (pins)**: Pin any query result as a named tile for quick re-use
  - **Analytics tab**: Query count trends, domain distribution, top queries (read from `QueryAuditLog`)
  - **Scheduled reports tab**: Create/manage email reports with cron-like schedule (`ScheduledReports` table)
- **Technology**: Angular 19 standalone components, Chart.js 4.x, `ChartWidgetComponent`
- **Security**: No secrets, no direct DB access, no SQL display

### .NET API (Trust Boundary)
- **Purpose**: Security enforcement and query execution
- **Responsibilities**:
  - Extract `customerId` from JWT claims (dev: hardcoded fallback)
  - Load schema policy from JSON file
  - Enforce MySQL-only dialect for execution pipeline
  - Orchestrate 6-step pipeline:
    1. Call Python agent for SQL candidate with policy constraints (`allowedTables`, `allowedFunctions`, limits)
    2. Check confidence/clarification
    3. Run SQL firewall (validate + rewrite)
    4. Execute approved SQL with Dapper + MySqlConnector
    5. Audit log structured event
    6. Call Python summarizer (best-effort) and return `nlSummary` with rows
  - Proxy SSE stream from Python agent to browser (`POST /api/query/stream`)
  - Return QueryResponse to frontend without SQL text
- **Technology**: ASP.NET Core 8, Dapper, MySqlConnector
- **Security**: Owns all secrets, enforces all rules

### Python Agent Service
- **Purpose**: Natural language → SQL translation
- **Responsibilities**:
  - Parse user question with multi-turn conversation history
  - Detect domain (arrears, tenancy, maintenance, inspection, owner_statement, vendor, property_portfolio, lease_renewal, compliance, financial_summary)
  - Scope relevant tables using real FK relationships from INFORMATION_SCHEMA
  - Run parallel agents: `domain_classifier` + `schema_prefetch` concurrently
  - Generate SQL using templates (template mode) or LLM (LLM mode)
  - Validate and auto-correct SQL candidate
  - Summarize result rows in natural language (`/v1/nl2sql/summarize`)
  - Stream agent progress via SSE (`/v1/nl2sql/stream`)
  - Persist conversation turns in Redis (or in-memory fallback)
- **Technology**: FastAPI, LangGraph, Pydantic, Redis
- **Security**: Stateless, no DB credentials, treated as untrusted

### MySQL Database
- **Purpose**: Multi-tenant data storage
- **Schema**: 17 tables, all tenant-scoped via `CustomerId`
  - Core: `Customers`, `Properties`, `Owners`, `Tenants`, `Tenancies`, `RentLedgerEntries`, `MaintenanceJobs`, `Inspections`, `OwnerStatements`, `Vendors`, `PropertyOwners`
  - Dashboard/Analytics: `DashboardWidgets`, `SavedQueries`, `ScheduledReports`, `QueryAuditLog`
  - Reference: `PropertyOwners` (join table)
- **Technology**: MySQL 8.4 in Docker (external port 3307)
- **Security**: Row-level tenant isolation via `CustomerId` column

## Technology Stack

| Layer | Technology | Version | Purpose |
|-------|------------|---------|---------|
| Frontend | Angular | 19.0 | Standalone components UI |
| API | ASP.NET Core | 8.0 | Trust boundary / security |
| Agent | Python FastAPI | 0.115 | NL→SQL generation |
| Agent Framework | LangGraph | 0.2.58 | Multi-agent state machine |
| Database | MySQL | 8.4 | Multi-tenant data store |
| ORM | Dapper | 2.1.35 | Micro-ORM for queries |
| Migrations | Flyway | 10.x | SQL-first schema versioning |
| Data Seeding | Bogus | 35.6 | Realistic fake AU data |

## Australian Domain Model

### Core Entities
- **Customers**: Property management companies (tenant root)
- **Properties**: Australian addresses with StateCode (NSW/VIC/etc), Postcode
- **Owners**: Property owners with ABN, BSB, bank account
- **Tenants**: Rental tenants with contact details
- **Tenancies**: Leases with weekly/fortnightly rent, bond = 4 weeks rent
- **RentLedgerEntries**: Charges, payments, arrears tracking
- **MaintenanceJobs**: Work orders with vendors, costs, status
- **Inspections**: Routine/entry/exit/compliance checks
- **OwnerStatements**: Monthly financial statements

### Australian-Specific Fields
- `Abn`: Australian Business Number (format: "## ### ### ###")
- `StateCode`: NSW, VIC, QLD, WA, SA, TAS, ACT, NT
- `RentFrequency`: Weekly, Fortnightly, Monthly (common in AU)
- `BondAmountCents`: Typically 4 weeks rent (AU regulation)
- `BankBsb`: 6-digit bank identifier (AU banking)

## Deployment Architecture

### Development (Current)
All services run via Docker Compose:
```bash
docker-compose up -d
```
| Service | Container | External Port |
|---------|-----------|---------------|
| Angular (nginx) | nlp2sql-frontend | 4200 |
| .NET API | nlp2sql-api | 5000 |
| Python Agent | nlp2sql-agent | 8000 |
| MySQL 8.4 | nlp2sql-mysql | 3307 |
| Redis | nlp2sql-redis | 6379 |
| Adminer | nlp2sql-adminer | 8080 |

DB migrations (Flyway) and seeding run automatically via the `nlp2sql-seeder` container on first start.

### Production (Recommended)
```
Frontend: Vercel / Netlify / Azure Static Web Apps
API: Azure App Service / Cloud Run / ECS
Agent: Azure Container Instances / Cloud Run / ECS
Database: Azure Database for MySQL / RDS / Cloud SQL
```

## Extension Points

### Adding New Domains
1. Add keywords and table list to `agents/nl2sql-service/domain-mapping.json`
2. Add SQL template to `agents/nl2sql-service/app/graph.py` `sql_templates` dict
3. Add new value to `Domain` enum in `agents/nl2sql-service/app/state_models.py`
4. LLM mode picks up new domains automatically via the domain-mapping.json keywords

### Adding New Tables
1. Create Flyway migration: `db/migrations/V3__add_new_table.sql`
2. Add table to `db/policy/schema-policy.json` allowedTables
3. Add join edges if needed
4. Run `flyway migrate`
5. Update seeder if needed

### Switching to Real LLM
Replace template-based sql_generator() with:
```python
from langchain_openai import ChatOpenAI
from langchain.chains import create_sql_query_chain

llm = ChatOpenAI(model="gpt-4")
chain = create_sql_query_chain(llm, db_connection)
sql = chain.invoke({"question": question, "table_info": schema_context})
```

## Monitoring & Observability

### Logs
- **Frontend**: Browser console
- **API**: Console (structured JSON for audit events)
- **Agent**: Uvicorn console
- **Database**: Docker logs

### Health Endpoints
- API: `GET /api/query/health`
- Agent: `GET /health`
- MySQL: Docker healthcheck

### Audit Events
Every query execution logs:
```json
{
  "event_type": "query_executed",
  "timestamp": "2024-01-15T10:30:00Z",
  "customer_id": "1",
  "user_id": "local-dev-user",
  "question": "Show active tenancies",
  "sql_executed": "SELECT ... WHERE CustomerId = @customerId ...",
  "rows_returned": 12,
  "execution_ms": 245,
  "firewall_approved": true,
  "agent_confidence": 0.7
}
```

## Performance Considerations

### Query Performance
- All tenant tables have composite indexes starting with `CustomerId`
- Specialized indexes for common queries (arrears, lease expiry, open jobs)
- Foreign key constraints ensure referential integrity

### Scaling
- **API**: Stateless, can scale horizontally behind load balancer
- **Agent**: Stateless, can scale horizontally (containerize)
- **Database**: Read replicas for reporting queries, master for writes

### Caching Opportunities
- Schema policy (loaded once at startup)
- Agent SQL templates (in-memory)
- Frequent queries (Redis cache with short TTL)

## Security Checklist (Production)

- [ ] Replace dev CustomerId fallback with real JWT extraction
- [ ] Add proper authentication middleware (JWT validation)
- [ ] Use HTTPS for all services (TLS certificates)
- [ ] Rotate database credentials regularly
- [ ] Store secrets in Azure Key Vault / AWS Secrets Manager
- [ ] Add rate limiting to API endpoints
- [ ] Enable SQL query result size limits (prevent memory exhaustion)
- [ ] Add request validation (max question length, input sanitization)
- [ ] Implement comprehensive audit logging (Azure Monitor / CloudWatch)
- [ ] Add alerting for firewall rule violations
- [ ] Configure CORS to allow only production domains
- [ ] Add API input validation with FluentValidation
- [ ] Enable OWASP security headers
- [ ] Add DDoS protection (Azure DDoS / Cloudflare)
