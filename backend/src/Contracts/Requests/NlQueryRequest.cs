namespace Contracts.Requests;

/// <summary>
/// Natural language query request from UI
/// </summary>
public sealed class NlQueryRequest
{
    public string Question { get; init; } = string.Empty;
    public string? ConversationId { get; init; }
}
