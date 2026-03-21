namespace Contracts.PropertyManagement;

public sealed class PMTaskDto
{
    public long TaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string AssignedToUserId { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public bool IsOverdue { get; init; }
    public long? PropertyId { get; init; }
    public string? PropertyAddress { get; init; }
    public long? TenancyId { get; init; }
    public long? MaintenanceJobId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}

public sealed class CreatePMTaskRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Category { get; init; } = "OTHER";
    public string Priority { get; init; } = "MEDIUM";
    public string AssignedToUserId { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public long? PropertyId { get; init; }
    public long? TenancyId { get; init; }
    public long? MaintenanceJobId { get; init; }
}

public sealed class UpdatePMTaskStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

public sealed class PMTaskSummaryDto
{
    public int OpenCount { get; init; }
    public int OverdueCount { get; init; }
    public int DueTodayCount { get; init; }
    public int DueThisWeekCount { get; init; }
    public IReadOnlyList<PMTaskDto> Tasks { get; init; } = Array.Empty<PMTaskDto>();
}
