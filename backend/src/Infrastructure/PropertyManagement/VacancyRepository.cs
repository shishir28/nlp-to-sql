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
                   CONCAT(p.StreetNumber, ' ', p.StreetName, ', ', p.Suburb) AS PropertyAddress,
                   v.Status, v.AdvertisedRent, v.RentFrequency, v.AvailableFrom,
                   DATEDIFF(CURRENT_DATE, v.AvailableFrom) AS DaysVacant,
                   (SELECT COUNT(*) FROM LettingEnquiries le WHERE le.VacancyId = v.VacancyId) AS EnquiryCount,
                   (SELECT COUNT(*) FROM LettingApplications la WHERE la.VacancyId = v.VacancyId) AS ApplicationCount,
                   v.CreatedAtUtc
            FROM Vacancies v
            JOIN Properties p ON p.PropertyId = v.PropertyId
            WHERE v.CustomerId = @CustomerId
              AND (@Status IS NULL OR v.Status = @Status)
            ORDER BY v.AvailableFrom ASC
            LIMIT 100";

        var rows = await conn.QueryAsync<VacancyDto>(sql, new { CustomerId = customerId, Status = status });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ListingDto>> GetListingsAsync(string customerId, long? vacancyId, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT l.ListingId, l.VacancyId, l.Platform, l.ExternalListingId,
                   l.Status, l.AdvertisedRent, l.ListedAt, l.ExpiresAt,
                   l.ViewCount, l.EnquiryCount
            FROM Listings l
            JOIN Vacancies v ON v.VacancyId = l.VacancyId
            WHERE v.CustomerId = @CustomerId
              AND (@VacancyId IS NULL OR l.VacancyId = @VacancyId)
            ORDER BY l.ListedAt DESC
            LIMIT 100";

        var rows = await conn.QueryAsync<ListingDto>(sql, new { CustomerId = customerId, VacancyId = vacancyId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<LettingApplicationDto>> GetApplicationsAsync(string customerId, long? vacancyId, string? status, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT la.ApplicationId, la.VacancyId,
                   CONCAT(p.StreetNumber, ' ', p.StreetName, ', ', p.Suburb) AS PropertyAddress,
                   la.ApplicantName, la.ApplicantEmail, la.ApplicantPhone,
                   la.Status, la.ProposedMoveIn, la.OfferedRent, la.ApplicantCount,
                   la.Notes, la.SubmittedAt
            FROM LettingApplications la
            JOIN Vacancies v ON v.VacancyId = la.VacancyId
            JOIN Properties p ON p.PropertyId = v.PropertyId
            WHERE v.CustomerId = @CustomerId
              AND (@VacancyId IS NULL OR la.VacancyId = @VacancyId)
              AND (@Status IS NULL OR la.Status = @Status)
            ORDER BY la.SubmittedAt DESC
            LIMIT 100";

        var rows = await conn.QueryAsync<LettingApplicationDto>(sql,
            new { CustomerId = customerId, VacancyId = vacancyId, Status = status });
        return rows.ToList();
    }
}
