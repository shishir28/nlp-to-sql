namespace Contracts.PropertyManagement;

public sealed class ComplianceItemDto
{
    public long ComplianceItemId { get; init; }
    public long PropertyId { get; init; }
    public string PropertyAddress { get; init; } = string.Empty;
    public string ComplianceType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public DateTime? LastCheckedDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsOverdue { get; init; }
    public int DaysUntilDue { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<ComplianceCheckDto> RecentChecks { get; init; } = Array.Empty<ComplianceCheckDto>();
}

public sealed class ComplianceCheckDto
{
    public long ComplianceCheckId { get; init; }
    public long ComplianceItemId { get; init; }
    public string Result { get; init; } = string.Empty;
    public DateTime CheckDate { get; init; }
    public DateTime? NextDueDate { get; init; }
    public string? CheckedByContractor { get; init; }
    public string? CertificateNumber { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class ComplianceSummaryDto
{
    public int TotalItems { get; init; }
    public int OverdueCount { get; init; }
    public int DueSoonCount { get; init; }
    public int PassedCount { get; init; }
    public IReadOnlyList<ComplianceItemDto> OverdueItems { get; init; } = Array.Empty<ComplianceItemDto>();
    public IReadOnlyList<ComplianceItemDto> DueSoonItems { get; init; } = Array.Empty<ComplianceItemDto>();
}
