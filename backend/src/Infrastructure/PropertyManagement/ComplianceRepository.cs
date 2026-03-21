using Contracts.PropertyManagement;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.PropertyManagement;

public interface IComplianceRepository
{
    Task<ComplianceSummaryDto> GetSummaryAsync(string customerId, CancellationToken ct);
    Task<IReadOnlyList<ComplianceItemDto>> GetByPropertyAsync(string customerId, long propertyId, CancellationToken ct);
    Task<IReadOnlyList<ComplianceItemDto>> GetOverdueAsync(string customerId, CancellationToken ct);
    Task<IReadOnlyList<ComplianceItemDto>> GetDueSoonAsync(string customerId, int daysAhead, CancellationToken ct);
}

public sealed class ComplianceRepository : IComplianceRepository
{
    private readonly string _connectionString;

    public ComplianceRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<ComplianceSummaryDto> GetSummaryAsync(string customerId, CancellationToken ct)
    {
        var overdue = await GetOverdueAsync(customerId, ct);
        var dueSoon = await GetDueSoonAsync(customerId, 30, ct);

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var totals = await conn.QueryFirstAsync<(int Total, int Passed)>(@"
            SELECT COUNT(*) AS Total,
                   SUM(CASE WHEN ci.Status = 'PASSED' THEN 1 ELSE 0 END) AS Passed
            FROM ComplianceItems ci
            WHERE ci.CustomerId = @CustomerId",
            new { CustomerId = customerId });

        return new ComplianceSummaryDto
        {
            TotalItems = totals.Total,
            OverdueCount = overdue.Count,
            DueSoonCount = dueSoon.Count,
            PassedCount = totals.Passed,
            OverdueItems = overdue,
            DueSoonItems = dueSoon
        };
    }

    public async Task<IReadOnlyList<ComplianceItemDto>> GetByPropertyAsync(string customerId, long propertyId, CancellationToken ct)
    {
        return await QueryItemsAsync(customerId,
            "AND ci.PropertyId = @PropertyId AND CURRENT_DATE >= IFNULL(ci.DueDate, '9999-12-31') = 0",
            new { CustomerId = customerId, PropertyId = propertyId }, ct);
    }

    public async Task<IReadOnlyList<ComplianceItemDto>> GetOverdueAsync(string customerId, CancellationToken ct)
    {
        return await QueryItemsAsync(customerId,
            "AND ci.DueDate < CURRENT_DATE AND ci.Status != 'PASSED'",
            new { CustomerId = customerId }, ct);
    }

    public async Task<IReadOnlyList<ComplianceItemDto>> GetDueSoonAsync(string customerId, int daysAhead, CancellationToken ct)
    {
        return await QueryItemsAsync(customerId,
            "AND ci.DueDate BETWEEN CURRENT_DATE AND DATE_ADD(CURRENT_DATE, INTERVAL @DaysAhead DAY)",
            new { CustomerId = customerId, DaysAhead = daysAhead }, ct);
    }

    private async Task<IReadOnlyList<ComplianceItemDto>> QueryItemsAsync(string customerId, string whereClause, object parameters, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var sql = $@"
            SELECT ci.ComplianceItemId, ci.PropertyId,
                   CONCAT(p.StreetNumber, ' ', p.StreetName, ', ', p.Suburb) AS PropertyAddress,
                   ci.ComplianceType, ci.Description, ci.DueDate, ci.LastCheckedDate,
                   ci.Status,
                   CASE WHEN ci.DueDate < CURRENT_DATE THEN 1 ELSE 0 END AS IsOverdue,
                   DATEDIFF(ci.DueDate, CURRENT_DATE) AS DaysUntilDue,
                   ci.Notes
            FROM ComplianceItems ci
            JOIN Properties p ON p.PropertyId = ci.PropertyId
            WHERE ci.CustomerId = @CustomerId
              {whereClause}
            ORDER BY ci.DueDate ASC
            LIMIT 100";

        var rows = await conn.QueryAsync<ComplianceItemDto>(sql, parameters);
        return rows.ToList();
    }
}
