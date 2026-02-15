namespace Application.Abstractions;

/// <summary>
/// Schema policy configuration loaded from JSON
/// </summary>
public sealed class SchemaPolicy
{
    public string Version { get; init; } = "1.0.0";
    public string Dialect { get; init; } = "mysql8";
    public bool SelectOnly { get; init; } = true;
    public bool NoViews { get; init; } = true;
    public int DefaultLimit { get; init; } = 50;
    public int MaxLimit { get; init; } = 200;
    public int TimeoutPolicyMs { get; init; } = 5000;
    public string TenantColumn { get; init; } = "CustomerId";
    public int MaxJoinDepth { get; init; } = 4;

    public IReadOnlyList<string> TenantRootTables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GlobalReferenceTables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedTables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<JoinEdge> AllowedJoinEdges { get; init; } = Array.Empty<JoinEdge>();
    public IReadOnlyList<string> AllowedFunctions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ForbiddenPatterns { get; init; } = Array.Empty<string>();
}

public sealed class JoinEdge
{
    public string From { get; init; } = string.Empty;
    public string FromColumn { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string ToColumn { get; init; } = string.Empty;
    public bool RequireTenantMatch { get; init; } = true;
}

/// <summary>
/// Audit event for query execution
/// </summary>
public sealed class AuditEvent
{
    public string RequestId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? Question { get; init; }
    public string? Domain { get; init; }
    public string CandidateSql { get; init; } = string.Empty;
    public string? ApprovedSql { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<string> RuleHits { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Violations { get; init; } = Array.Empty<string>();
    public long ExecutionMs { get; init; }
    public int RowCount { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}
