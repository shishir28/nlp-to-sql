namespace Contracts.PropertyManagement;

public sealed class MaintenanceQuoteDto
{
    public long QuoteId { get; init; }
    public long MaintenanceJobId { get; init; }
    public string Description { get; init; } = string.Empty;
    public long VendorId { get; init; }
    public string VendorName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime? QuoteDate { get; init; }
    public DateTime? ApprovedDate { get; init; }
    public string? ApprovedByUser { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class MaintenanceJobWorkflowDto
{
    public long MaintenanceJobId { get; init; }
    public long PropertyId { get; init; }
    public string PropertyAddress { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public decimal? QuoteAmount { get; init; }
    public DateTime? QuoteReceivedDate { get; init; }
    public DateTime? QuoteApprovedDate { get; init; }
    public string? QuoteApprovedByUser { get; init; }
    public string? InvoiceNumber { get; init; }
    public DateTime? InvoiceDate { get; init; }
    public decimal? InvoiceAmount { get; init; }
    public DateTime? InvoicePaidDate { get; init; }
    public string? WorkOrderNumber { get; init; }
    public DateTime? ScheduledDate { get; init; }
    public IReadOnlyList<MaintenanceQuoteDto> Quotes { get; init; } = Array.Empty<MaintenanceQuoteDto>();
}

public sealed class ApproveQuoteRequest
{
    public string ApprovedByUser { get; init; } = string.Empty;
    public string? Notes { get; init; }
}
