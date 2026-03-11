namespace Contracts.Dashboard;

public sealed class DashboardWidgetDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string ViewType { get; init; } = "table";
    public int SortOrder { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class SaveDashboardWidgetRequest
{
    public string Title { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string ViewType { get; init; } = "table";
    public int SortOrder { get; init; }
}

public sealed class UpdateWidgetViewRequest
{
    public string ViewType { get; init; } = "table";
}

public sealed class UpdateWidgetOrderRequest
{
    public int SortOrder { get; init; }
}

public sealed class SavedQueryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Role { get; init; } = "PropertyManager";
    public bool IsPinned { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class SaveQueryRequest
{
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Role { get; init; } = "PropertyManager";
}

public sealed class ScheduledReportDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Role { get; init; } = "PropertyManager";
    public string RecipientEmail { get; init; } = string.Empty;
    public string Schedule { get; init; } = "daily";
    public bool IsActive { get; init; }
    public DateTime? LastRunAtUtc { get; init; }
    public DateTime? NextRunAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class CreateScheduledReportRequest
{
    public string Name { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Role { get; init; } = "PropertyManager";
    public string RecipientEmail { get; init; } = string.Empty;
    public string Schedule { get; init; } = "daily";
}

public sealed class AnalyticsSummary
{
    public int TotalQueries { get; init; }
    public int SuccessfulQueries { get; init; }
    public int FailedQueries { get; init; }
    public double AvgExecutionMs { get; init; }
    public IReadOnlyList<DomainCount> DomainBreakdown { get; init; } = Array.Empty<DomainCount>();
    public IReadOnlyList<DailyCount> DailyTrend { get; init; } = Array.Empty<DailyCount>();
    public IReadOnlyList<RecentQuery> RecentQueries { get; init; } = Array.Empty<RecentQuery>();
}

public sealed class DomainCount
{
    public string Domain { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class DailyCount
{
    public string Date { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class RecentQuery
{
    public string Question { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long ExecutionMs { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
