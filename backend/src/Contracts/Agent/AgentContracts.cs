using System.Text.Json.Serialization;

namespace Contracts.Agent;

/// <summary>
/// Request to Python LangGraph service for SQL generation
/// </summary>
public sealed class Nl2SqlGenerateRequest
{
    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    [JsonPropertyName("context")]
    public UserContext Context { get; init; } = new();

    [JsonPropertyName("constraints")]
    public Nl2SqlConstraints Constraints { get; init; } = new();

    [JsonPropertyName("conversation_id")]
    public string? ConversationId { get; init; }
}

public sealed class UserContext
{
    [JsonPropertyName("customer_id")]
    public string CustomerId { get; init; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; init; } = "PropertyManager";
}

public sealed class Nl2SqlConstraints
{
    [JsonPropertyName("tenant_column")]
    public string TenantColumn { get; init; } = "CustomerId";

    [JsonPropertyName("default_limit")]
    public int DefaultLimit { get; init; } = 50;

    [JsonPropertyName("max_limit")]
    public int MaxLimit { get; init; } = 200;

    [JsonPropertyName("select_only")]
    public bool SelectOnly { get; init; } = true;

    [JsonPropertyName("allowed_tables")]
    public IReadOnlyList<string> AllowedTables { get; init; } = Array.Empty<string>();

    [JsonPropertyName("allowed_functions")]
    public IReadOnlyList<string> AllowedFunctions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Response from Python LangGraph service containing SQL candidate
/// </summary>
public sealed class Nl2SqlGenerateResponse
{
    [JsonPropertyName("sql_candidate")]
    public string SqlCandidate { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("needs_clarification")]
    public bool NeedsClarification { get; init; }

    [JsonPropertyName("clarification_prompt")]
    public string? ClarificationPrompt { get; init; }

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; init; } = string.Empty;

    [JsonPropertyName("original_question")]
    public string OriginalQuestion { get; init; } = string.Empty;

    [JsonPropertyName("detected_domain")]
    public string? DetectedDomain { get; init; }

    [JsonPropertyName("scoped_tables")]
    public IReadOnlyList<string> ScopedTables { get; init; } = Array.Empty<string>();
}

/// <summary>
/// SQL firewall validation result
/// </summary>
public sealed class FirewallResult
{
    public bool Approved { get; init; }
    public string Status { get; init; } = "rejected";
    public string? RewrittenSql { get; init; }
    public IReadOnlyList<string> RuleHits { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Violations { get; init; } = Array.Empty<string>();
    public string? Reason { get; init; }
}
