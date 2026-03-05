# 🏘️ NL2SQL Property Analytics

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=.net)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-19-DD0031?logo=angular)](https://angular.io/)
[![Python](https://img.shields.io/badge/Python-3.11+-3776AB?logo=python&logoColor=white)](https://www.python.org/)
[![MySQL](https://img.shields.io/badge/MySQL-8.4-4479A1?logo=mysql&logoColor=white)](https://www.mysql.com/)
[![LangGraph](https://img.shields.io/badge/LangGraph-0.2-8A2BE2)](https://github.com/langchain-ai/langgraph)

> **Production-ready multi-tenant Australian Property Management analytics system with natural language to SQL conversion.**

Ask questions like *"Show active tenancies ending in next 60 days"* and get instant SQL-powered insights from your property data.

---

## 🎯 What This Does

This system lets property managers ask questions in plain English and receive data-driven answers from a multi-tenant MySQL database—**without writing SQL**.

**Example Questions:**
- *"Which tenancies have arrears?"*
- *"Show open maintenance jobs older than 30 days"*
- *"List upcoming inspections for next month"*
- *"Which leases are expiring in 90 days?"*
- *"Show vacant properties in portfolio"*
- *"List all active contractors"*
- *"Show non-compliant inspection results"*
- *"Show total income summary by owner"*

**Under the Hood:**
1. User types a natural language question in Angular UI; live agent status is streamed via SSE
2. .NET API routes question to Python LangGraph multi-agent service (with `conversationId` for multi-turn memory)
3. Agent runs `domain_classifier` + `schema_prefetch` in parallel, then `schema_analyzer` → `sql_generator` → `sql_validator`
4. .NET SQL Firewall validates SQL and injects tenant isolation: `WHERE CustomerId = @customerId`
5. Query executes against MySQL with strict timeout
6. Python summarizer generates a plain-English NL summary of the returned rows
7. API returns result rows + explanation + NL summary (no raw SQL in UI)
8. Results displayed in responsive table with execution time and summary

---

## 🏗️ Architecture

```
┌─────────────┐
│   Angular   │  Gradient UI with example queries
│  (Port 4200)│
└──────┬──────┘
       │ HTTP POST /api/query
       ▼
┌─────────────────────────┐
│    .NET 8 API           │  🛡️ Trust Boundary
│  (Port 5000)            │  • Extract customerId from JWT
│                         │  • SQL Firewall (validate + inject tenant predicate)
│  Responsibilities:      │  • Execute with Dapper + MySqlConnector
│  • Auth & Security      │  • Audit log structured events
│  • SQL Firewall         │
│  • Query Execution      │
│  • Audit Logging        │
└────┬────────────────┬───┘
     │                │
     │ HTTP           │ SQL Queries
     ▼                ▼
┌──────────────┐  ┌────────────────────┐
│  Python      │  │   MySQL 8.4        │
│  LangGraph   │  │   (Docker)         │
│  (Port 8000) │  │                    │
│              │  │  Multi-tenant DB:  │
│  State Graph:│  │  • Customers       │
│  route →     │  │  • Properties      │
│  schema →    │  │  • Tenancies       │
│  plan →      │  │  • RentLedger      │
│  generate    │  │  • MaintenanceJobs │
│              │  │  • Inspections     │
│  Returns SQL │  │  • OwnerStatements │
│  candidate   │  │                    │
└──────────────┘  └────────────────────┘
```

**Security Model:**
- **.NET API = Trusted** (has DB credentials, enforces all security)
- **Python Agent = Untrusted** (returns SQL strings, no DB access)
- **Angular UI = Untrusted** (HTTP client only, never receives SQL text)

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed design.

---

## 🚀 Quick Start

### Prerequisites

- **Node.js** v20.19+ ([Download](https://nodejs.org/))
- **.NET 8 SDK** ([Download](https://dotnet.microsoft.com/download))
- **Python 3.11+** ([Download](https://www.python.org/downloads/))
- **Docker Desktop** ([Download](https://www.docker.com/products/docker-desktop))
- **Flyway CLI** ([Download](https://flywaydb.org/download))

### Step-by-Step Setup

```bash
# 1. Start Database
cd db
docker compose up -d
# Wait for healthy status (~20 seconds)

# 2. Run Migrations
flyway -configFiles=flyway.conf migrate

# 3. Seed Test Data (10,000+ records)
cd ../backend/src/SeedRunner
dotnet run

# 4. Start Python Agent
cd ../../../agents/nl2sql-service
python -m venv .venv
source .venv/bin/activate  # Windows: .venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000

# 5. Start .NET API (new terminal)
cd backend/src/Api
dotnet run

# 6. Start Angular Frontend (new terminal)
cd frontend
npm install  # First time only
npm start
```

### Test It!

1. Open browser: **http://localhost:4200**
2. Click example: **"Expiring Leases"**
3. Click **"Run Query"**
4. See results in < 3 seconds ✨

---

## 📚 Documentation

| Document | Purpose |
|----------|---------|
| [RUNBOOK.md](docs/RUNBOOK.md) | Complete setup guide, troubleshooting, service URLs |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | System design, data flow, security model, tech stack |
| [MESSAGE_FLOW.md](docs/MESSAGE_FLOW.md) | End-to-end request/control flow diagrams (success, clarification, blocked, error) |
| [ADR-001: Trust Boundary](docs/adrs/001-trust-boundary.md) | Why .NET API is the security enforcement layer |
| [ADR-002: Tenant Enforcement](docs/adrs/002-tenant-enforcement.md) | How `CustomerId` injection prevents tenant data leaks |

---

## 🎯 Key Features

### Security-First Design
- ✅ **Zero-trust architecture**: Python agent cannot access database
- ✅ **Multi-tenancy enforcement**: `WHERE CustomerId = @customerId` injected on every query
- ✅ **SQL Firewall**: Blocks mutations, enforces table/function allowlists, join-depth limits, tenant injection, and LIMIT capping
- ✅ **MySQL-only execution**: Orchestrator and agent reject unsupported SQL dialects
- ✅ **Audit logging**: Every query execution logged with structured JSON

### Australian Property Domain
- 🏘️ **Realistic schema**: 13 tables covering properties, tenancies, rent, maintenance, inspections
- 🇦🇺 **AU-specific fields**: ABN, BSB, state codes (NSW/VIC/QLD/etc), weekly/fortnightly rent
- 📊 **10,000+ seed records**: Generated with Bogus library (reproducible with fixed seed)

### Developer Experience
- ⚡ **Fast feedback**: Hot reload on all services (Angular, .NET, Python)
- 📦 **Docker everything**: MySQL + Adminer UI with single command
- 🔄 **SQL migrations**: Flyway for version-controlled schema evolution
- 📝 **Comprehensive docs**: Runbook, architecture diagrams, ADRs

---

## 🛠️ Technology Stack

| Layer | Technology | Version | Purpose |
|-------|------------|---------|---------|
| **Frontend** | Angular | 19.0 | Standalone components, reactive forms |
| **API** | ASP.NET Core | 8.0 | Trust boundary, security enforcement |
| **Agent** | Python FastAPI | 0.115 | NL→SQL generation service |
| **AI Framework** | LangGraph | 0.2.58 | Multi-agent state machine |
| **Database** | MySQL | 8.4 | Multi-tenant data store |
| **ORM** | Dapper | 2.1.35 | Micro-ORM for high-performance queries |
| **Migrations** | Flyway | 10.x | SQL-first schema versioning |
| **Seeding** | Bogus | 35.6 | Realistic fake data generator |

---

## 🔍 Example Queries

### Arrears Detection
**Question**: *"Which tenancies have arrears?"*

**Execution behavior**: Tenant isolation is enforced by firewall and arrears aggregations are returned as result rows.

### Lease Expiry Monitoring
**Question**: *"Show active tenancies ending in next 60 days"*

**Result**: Table with TenancyId, TenantName, PropertyAddress, LeaseEndDate, DaysUntilExpiry

### Open Maintenance Jobs
**Question**: *"Show open maintenance jobs older than 30 days"*

**Filters**: Status IN ('Open', 'InProgress'), DATEDIFF calculation from OpenedAtUtc

---

## 🧪 Testing

### Manual Smoke Test
```bash
# Health checks
curl http://localhost:8000/health  # Agent
curl http://localhost:5000/api/query/health  # API

# Execute query via API
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"question":"Show active tenancies","conversationId":"test"}'
```

---

## 🚧 Production Checklist

Before deploying to production:

- [ ] Replace dev `customerId="1"` fallback with real JWT extraction
- [ ] Add proper authentication middleware
- [ ] Configure HTTPS with TLS certificates
- [ ] Store secrets in Key Vault
- [ ] Add rate limiting to API endpoints
- [ ] Enable SQL result caching with Redis
- [ ] Switch Python agent from templates to real LLM
- [ ] Add comprehensive monitoring
- [ ] Configure CORS for production domain
- [ ] Add DDoS protection

See [docs/ARCHITECTURE.md#security-checklist-production](docs/ARCHITECTURE.md) for full list.

---

## 📖 Project Structure

```
nlp-to-sql/
├── backend/
│   └── src/
│       ├── Api/              # ASP.NET Core entry point + SSE proxy endpoint
│       ├── Application/      # Business logic (orchestrator + summarization)
│       ├── Contracts/        # DTOs (request/response + summarize contracts)
│       ├── Infrastructure/   # SQL firewall, executor, agent client, summarization client
│       └── SeedRunner/       # Database seeding tool
├── agents/
│   └── nl2sql-service/
│       ├── app/
│       │   ├── graph.py              # Template pipeline (10 domains)
│       │   ├── llm_graph.py          # LLM multi-agent graph (parallel fan-out)
│       │   ├── conversation_store.py # Multi-turn memory (Redis + in-memory)
│       │   ├── schema_introspection.py # Real FK lookup via INFORMATION_SCHEMA
│       │   ├── state_models.py       # Pydantic state + Domain enum
│       │   ├── models.py             # API request/response models
│       │   ├── main.py               # FastAPI: /generate, /stream, /summarize
│       │   ├── config.py             # Settings (LLM + DB + agent config)
│       │   └── prompt_library.py     # YAML prompt templates (Jinja2)
│       ├── domain-mapping.json       # Keyword→domain mappings
│       └── prompts/                  # YAML prompt templates per agent
├── frontend/                 # Angular 19 UI (SSE streaming + NL summary display)
├── db/                       # Migrations, Docker Compose, policies
└── docs/                     # Architecture docs & ADRs
```

---

**Built with ❤️ for property managers who deserve better analytics tools.**
- **Audit Trail**: Full request/response logging

## Project Structure

```
/backend          - .NET API and infrastructure
/agents           - Python LangGraph multi-agent service
/frontend         - Angular UI
/db               - Migrations, Docker, policy JSON
/docs             - Architecture decisions and diagrams
/tests            - Integration and security tests
```
