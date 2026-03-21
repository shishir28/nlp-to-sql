using Contracts.PropertyManagement;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.PropertyManagement;

public interface IVacancyRepository
{
    Task<IReadOnlyList<VacancyDto>> GetVacanciesAsync(string customerId, string? status, CancellationToken ct);
    Task<IReadOnlyList<ListingDto>> GetListingsAsync(string customerId, long? vacancyId, CancellationToken ct);
    Task<IReadOnlyList<LettingApplicationDto>> GetApplicationsAsync(string customerId, long? vacancyId, string? status, CancellationToken ct);
}

public sealed class VacancyRepository : IVacancyRepository
{
    private readonly string _connectionString;

    public VacancyRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<IReadOnlyList<VacancyDto>> GetVacanciesAsync(string customerId, string? status, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT v.VacancyId, v.PropertyId,
                   CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
                   v.StatusCode AS Status, v.TargetRentWeekly AS AdvertisedRent,
                   'Weekly' AS RentFrequency, v.VacancyDate AS AvailableFrom,
                   v.DaysOnMarket AS DaysVacant,
                   (SELECT COALESCE(SUM(l2.EnquiryCount), 0) FROM Listings l2 WHERE l2.VacancyId = v.VacancyId) AS EnquiryCount,
                   (SELECT COUNT(*) FROM LettingApplications la JOIN Listings l3 ON l3.ListingId = la.ListingId WHERE l3.VacancyId = v.VacancyId) AS ApplicationCount,
                   v.CreatedAtUtc
            FROM Vacancies v
            JOIN Properties p ON p.PropertyId = v.PropertyId
            WHERE v.CustomerId = @CustomerId
              AND (@Status IS NULL OR v.StatusCode = @Status)
            ORDER BY v.VacancyDate ASC
            LIMIT 100";

        var rows = await conn.QueryAsync<VacancyDto>(sql, new { CustomerId = customerId, Status = status });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ListingDto>> GetListingsAsync(string customerId, long? vacancyId, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT l.ListingId, l.VacancyId, l.ListingPortal AS Platform,
                   NULL AS ExternalListingId,
                   l.StatusCode AS Status, l.AdvertisedRentWeekly AS AdvertisedRent,
                   l.ListedDate AS ListedAt, l.RemovedDate AS ExpiresAt,
                   l.ClickCount AS ViewCount, l.EnquiryCount
            FROM Listings l
            JOIN Vacancies v ON v.VacancyId = l.VacancyId
            WHERE v.CustomerId = @CustomerId
              AND (@VacancyId IS NULL OR l.VacancyId = @VacancyId)
            ORDER BY l.ListedDate DESC
            LIMIT 100";

        var rows = await conn.QueryAsync<ListingDto>(sql, new { CustomerId = customerId, VacancyId = vacancyId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<LettingApplicationDto>> GetApplicationsAsync(string customerId, long? vacancyId, string? status, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT la.ApplicationId, l.VacancyId,
                   CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
                   la.ApplicantName, la.ApplicantEmail, la.ApplicantPhone,
                   la.StatusCode AS Status, la.ApplicationDate AS ProposedMoveIn,
                   la.WeeklyIncome AS OfferedRent, NULL AS ApplicantCount,
                   la.Notes, la.CreatedAtUtc AS SubmittedAt
            FROM LettingApplications la
            JOIN Listings l ON l.ListingId = la.ListingId
            JOIN Vacancies v ON v.VacancyId = l.VacancyId
            JOIN Properties p ON p.PropertyId = v.PropertyId
            WHERE v.CustomerId = @CustomerId
              AND (@VacancyId IS NULL OR l.VacancyId = @VacancyId)
              AND (@Status IS NULL OR la.StatusCode = @Status)
            ORDER BY la.CreatedAtUtc DESC
            LIMIT 100";

        var rows = await conn.QueryAsync<LettingApplicationDto>(sql,
            new { CustomerId = customerId, VacancyId = vacancyId, Status = status });
        return rows.ToList();
    }
}
