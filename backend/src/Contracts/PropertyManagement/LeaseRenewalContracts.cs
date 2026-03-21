namespace Contracts.PropertyManagement;

public sealed class LeaseRenewalOutcomeDto
{
    public long OutcomeId { get; init; }
    public long TenancyId { get; init; }
    public long PropertyId { get; init; }
    public string PropertyAddress { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public DateTime? LeaseEndDate { get; init; }
    public decimal? ProposedNewRent { get; init; }
    public DateTime? ProposedStartDate { get; init; }
    public string OutcomeCode { get; init; } = string.Empty;
    public DateTime OutcomeDate { get; init; }
    public string? Notes { get; init; }
    public string? HandledByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class LeaseRenewalSummaryDto
{
    public int TotalUpcoming { get; init; }
    public int OfferedCount { get; init; }
    public int AcceptedCount { get; init; }
    public int VacatingCount { get; init; }
    public int PeriodicCount { get; init; }
    public IReadOnlyList<LeaseRenewalOutcomeDto> Renewals { get; init; } = Array.Empty<LeaseRenewalOutcomeDto>();
}
