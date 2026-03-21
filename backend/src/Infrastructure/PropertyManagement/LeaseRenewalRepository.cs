using Contracts.PropertyManagement;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.PropertyManagement;

public interface ILeaseRenewalRepository
{
    Task<LeaseRenewalSummaryDto> GetSummaryAsync(string customerId, CancellationToken ct);
    Task<IReadOnlyList<LeaseRenewalOutcomeDto>> GetRenewalsAsync(string customerId, string? outcomeCode, CancellationToken ct);
}

public sealed class LeaseRenewalRepository : ILeaseRenewalRepository
{
    private readonly string _connectionString;

    public LeaseRenewalRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<LeaseRenewalSummaryDto> GetSummaryAsync(string customerId, CancellationToken ct)
    {
        var renewals = await GetRenewalsAsync(customerId, null, ct);

        return new LeaseRenewalSummaryDto
        {
            TotalUpcoming = renewals.Count,
            OfferedCount = renewals.Count(r => r.OutcomeCode == "OFFERED"),
            AcceptedCount = renewals.Count(r => r.OutcomeCode == "ACCEPTED"),
            VacatingCount = renewals.Count(r => r.OutcomeCode == "VACATING"),
            PeriodicCount = renewals.Count(r => r.OutcomeCode == "PERIODIC"),
            Renewals = renewals
        };
    }

    public async Task<IReadOnlyList<LeaseRenewalOutcomeDto>> GetRenewalsAsync(string customerId, string? outcomeCode, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<LeaseRenewalOutcomeDto>(@"
            SELECT lr.OutcomeId, lr.TenancyId,
                   t.PropertyId,
                   CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
                   tn.FullName AS TenantName,
                   t.LeaseEndDate,
                   lr.ProposedNewRent, lr.ProposedStartDate,
                   lr.OutcomeCode, lr.OutcomeDate, lr.Notes, lr.HandledByUserId,
                   lr.CreatedAtUtc
            FROM LeaseRenewalOutcomes lr
            JOIN Tenancies t ON t.TenancyId = lr.TenancyId
            JOIN Properties p ON p.PropertyId = t.PropertyId
            JOIN Tenants tn ON tn.TenantId = t.TenantId
            WHERE lr.CustomerId = @CustomerId
              AND (@OutcomeCode IS NULL OR lr.OutcomeCode = @OutcomeCode)
            ORDER BY t.LeaseEndDate ASC
            LIMIT 100",
            new { CustomerId = customerId, OutcomeCode = outcomeCode });

        return rows.ToList();
    }
}
