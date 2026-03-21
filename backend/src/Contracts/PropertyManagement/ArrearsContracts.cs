namespace Contracts.PropertyManagement;

public sealed class ArrearsEscalationDto
{
    public long EscalationId { get; init; }
    public long TenancyId { get; init; }
    public long PropertyId { get; init; }
    public string PropertyAddress { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string EscalationStage { get; init; } = string.Empty;
    public decimal ArrearsAmount { get; init; }
    public int ArrearsDays { get; init; }
    public DateTime EscalationDate { get; init; }
    public DateTime? NextActionDate { get; init; }
    public string? Notes { get; init; }
    public string? HandledByUserId { get; init; }
    public bool IsResolved { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class ArrearsSummaryDto
{
    public int TotalTenanciesInArrears { get; init; }
    public decimal TotalArrearsAmount { get; init; }
    public int AtTribunalCount { get; init; }
    public int OnPaymentPlanCount { get; init; }
    public IReadOnlyList<ArrearsEscalationDto> Escalations { get; init; } = Array.Empty<ArrearsEscalationDto>();
}
