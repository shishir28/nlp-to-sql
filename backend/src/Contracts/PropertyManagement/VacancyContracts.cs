namespace Contracts.PropertyManagement;

public sealed class VacancyDto
{
    public long VacancyId { get; init; }
    public long PropertyId { get; init; }
    public string PropertyAddress { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal? AdvertisedRent { get; init; }
    public string? RentFrequency { get; init; }
    public DateTime? AvailableFrom { get; init; }
    public int DaysVacant { get; init; }
    public int EnquiryCount { get; init; }
    public int ApplicationCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class ListingDto
{
    public long ListingId { get; init; }
    public long VacancyId { get; init; }
    public string Platform { get; init; } = string.Empty;
    public string? ExternalListingId { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal? AdvertisedRent { get; init; }
    public DateTime? ListedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int? ViewCount { get; init; }
    public int? EnquiryCount { get; init; }
}

public sealed class LettingApplicationDto
{
    public long ApplicationId { get; init; }
    public long VacancyId { get; init; }
    public string PropertyAddress { get; init; } = string.Empty;
    public string ApplicantName { get; init; } = string.Empty;
    public string? ApplicantEmail { get; init; }
    public string? ApplicantPhone { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? ProposedMoveIn { get; init; }
    public decimal? OfferedRent { get; init; }
    public int? ApplicantCount { get; init; }
    public string? Notes { get; init; }
    public DateTime SubmittedAt { get; init; }
}
