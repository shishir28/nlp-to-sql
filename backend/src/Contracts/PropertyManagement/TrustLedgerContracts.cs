namespace Contracts.PropertyManagement;

public sealed class TrustLedgerEntryDto
{
    public long TrustLedgerId { get; init; }
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public long? PropertyId { get; init; }
    public string? PropertyAddress { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal RunningBalance { get; init; }
    public DateOnly TransactionDate { get; init; }
    public string? Reference { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class TrustLedgerSummaryDto
{
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public decimal CurrentBalance { get; init; }
    public decimal TotalRentIn { get; init; }
    public decimal TotalDisbursed { get; init; }
    public decimal TotalFees { get; init; }
    public IReadOnlyList<TrustLedgerEntryDto> RecentEntries { get; init; } = Array.Empty<TrustLedgerEntryDto>();
}

public sealed class ManagementFeeScheduleDto
{
    public long FeeScheduleId { get; init; }
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public long? PropertyId { get; init; }
    public string? PropertyAddress { get; init; }
    public string FeeType { get; init; } = string.Empty;
    public decimal? FeePercent { get; init; }
    public decimal? FeeFixed { get; init; }
    public bool IsActive { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
}
