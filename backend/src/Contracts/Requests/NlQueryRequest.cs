namespace Contracts.Requests;

/// <summary>
/// Natural language query request from UI
/// </summary>
public sealed class NlQueryRequest
{
    public string Question { get; init; } = string.Empty;
    public string? ConversationId { get; init; }
    /// <summary>Dev/demo override for customer context (ignored if auth claim present)</summary>
    public string? CustomerId { get; init; }
    public string? Role { get; init; }
}
