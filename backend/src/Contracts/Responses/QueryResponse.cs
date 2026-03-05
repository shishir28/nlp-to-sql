namespace Contracts.Responses;

/// <summary>
/// Query execution result returned to UI
/// </summary>
public sealed class QueryResponse
{
    public string RequestId { get; init; } = string.Empty;
    public string Status { get; init; } = "ok";
    public IReadOnlyList<Dictionary<string, object?>> Rows { get; init; } = Array.Empty<Dictionary<string, object?>>();
    public IReadOnlyList<ColumnInfo> Columns { get; init; } = Array.Empty<ColumnInfo>();
    public int RowCount { get; init; }
    public long ExecutionMs { get; init; }
    public string? Domain { get; init; }
    public string? Explanation { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public string? NlSummary { get; init; }
}

public sealed class ColumnInfo
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "string";
}
