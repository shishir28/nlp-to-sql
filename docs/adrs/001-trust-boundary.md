# ADR-001: Trust Boundary at .NET API Layer

**Status**: Accepted

**Date**: 2024-01-15

## Context

We are building a multi-tenant NL→SQL system where:
- Users ask natural language questions
- A Python service generates SQL candidates
- SQL is executed against a shared MySQL database
- Multiple customers share the same database, isolated by `CustomerId`

**The security challenge**: How do we enforce tenant isolation when SQL generation happens in a separate Python service?

## Decision

**The .NET API is the trust boundary. The Python agent is untrusted.**

### Principles

1. **Python agent NEVER gets database credentials**
   - Agent returns SQL strings only
   - Cannot execute queries directly
   - Treated as "suggestion generator"

2. **.NET API owns all security enforcement**
   - Extracts `customerId` from JWT claims (authenticated context)
   - Runs SQL firewall to validate agent-generated SQL
   - Injects `WHERE CustomerId = @customerId` predicate
   - Executes queries with parameterized values
   - Audits all query attempts

3. **Frontend is also untrusted**
   - Angular UI cannot bypass API
   - No direct database access
   - Cannot provide CustomerId (always from API auth)

### Architecture Implications

```
┌─────────────┐
│   Angular   │  ❌ Untrusted - no DB credentials
└──────┬──────┘
       │ HTTP
       ▼
┌─────────────────────────┐
│    .NET API             │  ✅ Trust Boundary
│  ┌────────────────────┐ │     - Has DB credentials
│  │ Extract customerId │ │     - Enforces security
│  │ from JWT claims    │ │     - Executes queries
│  ├────────────────────┤ │
│  │ Call Python agent  │◀┼─┐
│  ├────────────────────┤ │ │
│  │ SQL Firewall       │ │ │
│  ├────────────────────┤ │ │
│  │ Execute SQL        │ │ │
│  └────────────────────┘ │ │
└─────────────────────────┘ │
                            │ HTTP (no DB creds)
┌─────────────────────────┐ │
│   Python Agent          │◀┘
│  (LangGraph)            │  ❌ Untrusted - returns SQL strings only
└─────────────────────────┘
```

## Consequences

### Positive

1. **Defense in depth**: Even if agent is compromised, attacker cannot access database
2. **Single enforcement point**: All security rules in one place (.NET API)
3. **Agent simplicity**: Python service is stateless, no secrets to manage
4. **Auditability**: Every query execution logged in .NET API
5. **Language flexibility**: Can swap Python agent for any other service

### Negative

1. **Extra network hop**: API → Agent → API (adds latency)
2. **Duplication**: Schema policy must be passed to agent as constraints
3. **Firewall complexity**: .NET must parse/validate SQL returned by agent

### Mitigations

- **Latency**: HTTP calls are fast (< 50ms for local dev, < 200ms prod)
- **Schema sync**: Policy loaded from JSON file shared between services
- **Firewall**: Use regex + allowlists (no need for full SQL parser)

## Alternatives Considered

### Alternative 1: Python Agent Executes SQL Directly

```
Frontend → Agent → MySQL
```

Rejected because:
- Violates zero-trust principle
- Hard to enforce customerId injection
- Audit logging split across services
- Python agent becomes attack target with DB creds

### Alternative 2: .NET API Generates SQL (No Agent)

```
Frontend → .NET API (NL→SQL + Execute)
```

Rejected because:
- .NET ecosystem weaker for NLP/LLM tasks
- Harder to iterate on prompt engineering
- Coupling NL processing with security enforcement
- Python/LangChain better tooling for AI workflows

### Alternative 3: Agent Returns Abstract Syntax Tree (AST)

```
Agent → AST → .NET converts to SQL
```

Rejected because:
- Over-engineering for MVP
- Still need SQL firewall for validation
- AST serialization adds complexity
- No advantage over validating SQL strings

## Implementation

See [ARCHITECTURE.md](../ARCHITECTURE.md) for data flow details.

Key files:
- `backend/src/Application/Orchestration/NlSqlOrchestrator.cs` - Coordinates agent call + firewall
- `backend/src/Infrastructure/Security/SqlFirewall.cs` - SQL validation + tenant injection
- `agents/nl2sql-service/app/main.py` - Untrusted agent endpoint

## References

- [OWASP: Defense in Depth](https://owasp.org/www-community/Defense_in_Depth)
- [Google Zero Trust Architecture](https://cloud.google.com/security/zero-trust)
- [ADR-002: Tenant Enforcement Strategy](./002-tenant-enforcement.md)
