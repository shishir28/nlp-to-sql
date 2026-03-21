using Contracts.PropertyManagement;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.PropertyManagement;

public interface IArrearsRepository
{
    Task<ArrearsSummaryDto> GetSummaryAsync(string customerId, CancellationToken ct);
    Task<IReadOnlyList<ArrearsEscalationDto>> GetActiveEscalationsAsync(string customerId, CancellationToken ct);
}

public sealed class ArrearsRepository : IArrearsRepository
{
    private readonly string _connectionString;

    public ArrearsRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<ArrearsSummaryDto> GetSummaryAsync(string customerId, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var escalations = (await GetActiveEscalationsAsync(customerId, ct)).ToList();

        return new ArrearsSummaryDto
        {
            TotalTenanciesInArrears = escalations.Select(e => e.TenancyId).Distinct().Count(),
            TotalArrearsAmount = escalations.Sum(e => e.ArrearsAmount),
            AtTribunalCount = escalations.Count(e => e.EscalationStage == "TRIBUNAL"),
            OnPaymentPlanCount = escalations.Count(e => e.EscalationStage == "PAYMENT_PLAN"),
            Escalations = escalations
        };
    }

    public async Task<IReadOnlyList<ArrearsEscalationDto>> GetActiveEscalationsAsync(string customerId, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<ArrearsEscalationDto>(@"
            SELECT ae.EscalationId, ae.TenancyId,
                   t.PropertyId,
                   CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
                   tn.FullName AS TenantName,
                   ae.Stage AS EscalationStage, ae.ArrearsAmountAtStage AS ArrearsAmount,
                   0 AS ArrearsDays,
                   ae.EscalationDate, NULL AS NextActionDate, ae.Notes,
                   ae.HandledByUserId, 0 AS IsResolved, ae.CreatedAtUtc
            FROM ArrearsEscalations ae
            JOIN Tenancies t ON t.TenancyId = ae.TenancyId
            JOIN Properties p ON p.PropertyId = t.PropertyId
            JOIN Tenants tn ON tn.TenantId = t.TenantId
            WHERE ae.CustomerId = @CustomerId
            ORDER BY ae.ArrearsAmountAtStage DESC
            LIMIT 100",
            new { CustomerId = customerId });

        return rows.ToList();
    }
}
