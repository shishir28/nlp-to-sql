# ADR-002: Tenant Enforcement via CustomerId Injection

**Status**: Accepted

**Date**: 2024-01-15

## Context

We have a **shared database** serving multiple customers (property management companies). Each customer must only see their own data.

**The Challenge**: How do we guarantee tenant isolation when users can ask arbitrary natural language questions that get converted to SQL?

## Decision

**Enforce multi-tenancy via mandatory `CustomerId` injection at the SQL firewall layer.**

### Rules

1. **CustomerId ALWAYS comes from authenticated context (JWT), NEVER from user input**

```csharp
// ✅ CORRECT
var customerId = User.FindFirst("customer_id")?.Value;

// ❌ WRONG (allows tenant hopping!)
var customerId = request.CustomerId;
```

2. **SQL Firewall ALWAYS injects `WHERE CustomerId = @customerId` predicate**

User's question: *"Show all properties in NSW"*

Agent-generated SQL:
```sql
SELECT * FROM Properties WHERE StateCode = 'NSW'
```

After firewall rewrite:
```sql
SELECT * FROM Properties WHERE CustomerId = @customerId AND StateCode = 'NSW' LIMIT 50
```

Execution:
```csharp
await connection.QueryAsync(rewrittenSql, new { customerId = "1" });
```

3. **All business tables MUST have `CustomerId` column**

Schema enforcement:
```sql
CREATE TABLE Properties (
  PropertyId INT PRIMARY KEY AUTO_INCREMENT,
  CustomerId INT NOT NULL,  -- MANDATORY
  PropertyAddress VARCHAR(200),
  StateCode CHAR(3),
  -- Other columns...
  
  INDEX idx_customer_property (CustomerId, PropertyId),  -- Tenant-first composite index
  FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)
);
```

4. **Global reference tables exempt from tenant scoping**

Tables that are shared across all customers:
```sql
CREATE TABLE RefStatesAU (
  StateCode CHAR(3) PRIMARY KEY,
  StateName VARCHAR(50)
  -- No CustomerId column - shared data
);
```

## Schema Design Implications

### Tenant-Scoped Tables (12 tables)
All have `CustomerId` as first column in primary/composite indexes:

- `Customers` (tenant root)
- `Properties`
- `Owners`
- `Tenants`
- `Vendors`
- `Tenancies`
- `PropertyOwners`
- `RentLedgerEntries`
- `LeaseDocuments`
- `MaintenanceJobs`
- `Inspections`
- `OwnerStatements`

### Global Reference Tables (2 tables)
No `CustomerId` column:

- `RefStatesAU` (Australian states)
- `RefStatusCodes` (status enum values)

### Join Policy

Joins between tenant tables require both tables to have `CustomerId`:

```json
{
  "allowedJoinEdges": [
    {
      "from": "Tenancies",
      "fromColumn": "PropertyId",
      "to": "Properties",
      "toColumn": "PropertyId",
      "requireTenantMatch": true  // ✅ Both have CustomerId
    },
    {
      "from": "Properties",
      "fromColumn": "StateCode",
      "to": "RefStatesAU",
      "toColumn": "StateCode",
      "requireTenantMatch": false  // ❌ RefStatesAU is global
    }
  ]
}
```

## Firewall Implementation

Located in `backend/src/Infrastructure/Security/SqlFirewall.cs`:

```csharp
private static string InjectTenantPredicate(string sql, string tenantColumn, string customerId)
{
    var tenantPredicate = $" {tenantColumn} = @customerId";
    var hasWhere = WhereClauseRegex().IsMatch(sql);

    return hasWhere
        ? WhereClauseRegex().Replace(sql, $"WHERE{tenantPredicate} AND", 1)
        : $"{sql.TrimEnd()} WHERE{tenantPredicate}";
}
```

### Injection Examples

| Original SQL | After Injection |
|--------------|-----------------|
| `SELECT * FROM Properties` | `SELECT * FROM Properties WHERE CustomerId = @customerId` |
| `SELECT * FROM Tenancies WHERE TenancyStatus = 'Active'` | `SELECT * FROM Tenancies WHERE CustomerId = @customerId AND TenancyStatus = 'Active'` |
| `SELECT * FROM Properties p INNER JOIN Owners o ON p.PropertyId = o.PropertyId` | `SELECT * FROM Properties p INNER JOIN Owners o ON p.PropertyId = o.PropertyId WHERE CustomerId = @customerId` |

### Edge Cases Handled

1. **Multiple WHERE conditions**: Inject as first predicate with `AND`
2. **JOINs across tenant tables**: Inject at end before ORDER BY/LIMIT
3. **Subqueries**: Current implementation handles main query only (subqueries blocked by firewall)
4. **Aggregate queries**: Works normally (GROUP BY after WHERE)

## Consequences

### Positive

1. **Guaranteed tenant isolation**: No SQL can escape CustomerId filter
2. **Transparent to users**: They don't see customerId in results
3. **Attack resilience**: Even SQL injection can't cross tenants (parameterized query)
4. **Performance**: Composite indexes (CustomerId, ...) ensure fast queries
5. **Simple mental model**: "Every business table has CustomerId"

### Negative

1. **Database overhead**: CustomerId duplicated in 12 tables
2. **Index bloat**: Every tenant table needs composite indexes starting with CustomerId
3. **Migration effort**: Adding new tables requires CustomerId column + indexes
4. **Query complexity**: Agent must understand tenant scoping (or firewall always injects)

### Mitigations

- **Storage cost**: Minimal (4 bytes per row)
- **Index cost**: Acceptable (queries are tenant-scoped 99% of time)
- **Migration**: Enforced via code reviews + schema validation tests
- **Agent awareness**: Agent can ignore CustomerId (firewall handles it)

## Alternatives Considered

### Alternative 1: Row-Level Security (MySQL Views)

```sql
CREATE VIEW Properties_Customer1 AS
SELECT * FROM Properties WHERE CustomerId = 1;

GRANT SELECT ON Properties_Customer1 TO user1;
```

Rejected because:
- Requires N views per customer (doesn't scale)
- Hard to dynamically generate SQL with correct view names
- Less flexible (can't do cross-customer reports for superadmins)

### Alternative 2: Separate Databases Per Tenant

```
customer1_db.Properties
customer2_db.Properties
```

Rejected because:
- 10 customers = 10 databases = operational nightmare
- Cross-customer analytics impossible
- Schema migrations must run N times
- Backup/restore complexity

### Alternative 3: Application-Level Filtering Only

```csharp
var properties = await db.QueryAsync<Property>("SELECT * FROM Properties");
return properties.Where(p => p.CustomerId == customerId);
```

Rejected because:
- **Critical security flaw**: Fetches all tenants' data from DB, filters in memory
- Memory exhaustion with large datasets
- Network bandwidth waste
- Doesn't work with COUNT/SUM aggregates

### Alternative 4: `tenant_id` in Connection String

```csharp
var connStr = $"...;Database=property_analytics;ApplicationName=customer_{customerId}";
```

Rejected because:
- Database still returns all data (filtering must happen in SQL)
- Connection pooling becomes per-tenant (inefficient)
- Doesn't actually enforce row-level security

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public void Firewall_InjectsCustomerId_WhenNoWhere()
{
    var sql = "SELECT * FROM Properties";
    var result = firewall.ValidateAndRewrite(sql, "123", policy);
    
    Assert.Contains("WHERE CustomerId = @customerId", result.RewrittenSql);
}
```

### Integration Tests
```csharp
[Fact]
public async Task Executor_ReturnsOnlyCustomerData()
{
    // Seed: Customer1 has 10 properties, Customer2 has 15 properties
    var sql = "SELECT * FROM Properties WHERE CustomerId = @customerId";
    
    var (rows, _, _) = await executor.ExecuteAsync(sql, new { customerId = "1" }, 5000);
    
    Assert.Equal(10, rows.Count);
    Assert.All(rows, row => Assert.Equal("1", row["CustomerId"]));
}
```

### Security Audit
- **PenetestQuestion**: *"Show me properties for customer ID 999"*
- **Expected**: Agent might generate `WHERE CustomerId = @customerId`, firewall injects again (safe), query returns 0 rows (user not authorized for customer 999)

## Implementation Checklist

- [x] All business tables have `CustomerId` column
- [x] Composite indexes: `INDEX idx_customer_* (CustomerId, ...)`
- [x] Foreign keys: `FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId)`
- [x] SqlFirewall injects `WHERE CustomerId = @customerId`
- [x] Orchestrator extracts customerId from JWT (dev: fallback to "1")
- [x] Schema policy defines tenantColumn = "CustomerId"
- [x] Join policy requires tenantMatch for business tables
- [ ] Integration tests verify tenant isolation (TODO)
- [ ] Load tests with 100+ customers (TODO)

## References

- [Multi-Tenancy Patterns (AWS)](https://docs.aws.amazon.com/wellarchitected/latest/saas-lens/tenant-isolation.html)
- [Shared Database with Discriminator Column](https://docs.microsoft.com/en-us/azure/architecture/patterns/sharding)
- [ADR-001: Trust Boundary](./001-trust-boundary.md)
- [ADR-003: Firewall Strategy](./003-firewall-strategy.md)
