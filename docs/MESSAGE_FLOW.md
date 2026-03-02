# 🔄 Message & Control Flow

This document shows how a natural-language request moves through the system and how control branches for success, clarification, and blocked/error outcomes.

## End-to-End Sequence

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant F as Angular Frontend
    participant C as QueryController (.NET API)
    participant O as NlSqlOrchestrator
    participant A as Python Agent Service
    participant W as SQL Firewall
    participant D as MySQL
    participant L as Audit Logger

    U->>F: Enter natural-language question
    F->>C: POST /api/query { question, conversationId? }
    C->>O: HandleAsync(request, customerId, userId, role)
    O->>A: POST /v1/nl2sql/generate + policy constraints
    A-->>O: { sql_candidate, confidence, needs_clarification, domain }

    alt clarification needed OR low confidence
        O->>L: Log status=clarification_needed
        O-->>C: QueryResponse(status=clarification_needed, message, explanation)
        C-->>F: 200 response
        F-->>U: Show clarification prompt (no SQL shown)
    else candidate returned with enough confidence
        O->>W: ValidateAndRewrite(sql_candidate, customerId, policy)
        alt firewall rejected
            O->>L: Log status=blocked + violations
            O-->>C: QueryResponse(status=blocked, message, explanation)
            C-->>F: 200 response
            F-->>U: Show blocked message (no SQL shown)
        else firewall approved
            O->>D: Execute rewritten SQL with @customerId
            D-->>O: rows + columns + executionMs
            O->>L: Log status=ok
            O-->>C: QueryResponse(status=ok, rows, columns, domain, explanation)
            C-->>F: 200 response
            F-->>U: Show results table + explanation
        end
    end
```

## API Control Flow

```mermaid
flowchart TD
    A[POST /api/query] --> B{Question empty?}
    B -->|Yes| B1[400 INVALID_REQUEST]
    B -->|No| C[Build user context from claims]
    C --> D[Orchestrator: load schema policy]
    D --> E{Policy dialect starts with mysql?}
    E -->|No| E1[Return blocked: UNSUPPORTED_DIALECT]
    E -->|Yes| F[Call Python agent with constraints]
    F --> G{Needs clarification or confidence < threshold?}
    G -->|Yes| G1[Return clarification_needed + NL prompt]
    G -->|No| H[Run SQL firewall]
    H --> I{Firewall approved?}
    I -->|No| I1[Return blocked + reason]
    I -->|Yes| J[Execute rewritten SQL in MySQL]
    J --> K[Return ok + rows + explanation]
```

## Agent Internal Flow (High-Level)

```mermaid
flowchart LR
    Q[Question + Constraints] --> DC[Domain Classifier]
    DC --> SA[Schema Analyzer]
    SA --> SG[SQL Generator]
    SG --> SV[SQL Validator]
    SV --> R{needs_clarification?}
    R -->|Yes| CL[Clarification Prompt]
    R -->|No| OUT[SQL Candidate + Confidence]
```

## Notes For New Developers

- Trust boundary is the .NET API: only it can execute SQL.
- Python agent is untrusted and only suggests SQL.
- Frontend never receives SQL text; it receives only safe response metadata and rows.
- Tenant isolation is always injected by firewall via `CustomerId = @customerId`.
- Policy constraints (`allowedTables`, `allowedFunctions`, limits, dialect) are sent to agent and enforced again by firewall.
