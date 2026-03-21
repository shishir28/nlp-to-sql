using Contracts.PropertyManagement;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.PropertyManagement;

public interface ITrustLedgerRepository
{
    Task<IReadOnlyList<TrustLedgerSummaryDto>> GetOwnerSummariesAsync(string customerId, CancellationToken ct);
    Task<IReadOnlyList<TrustLedgerEntryDto>> GetEntriesForOwnerAsync(string customerId, long ownerId, int limit, CancellationToken ct);
    Task<IReadOnlyList<ManagementFeeScheduleDto>> GetFeeSchedulesAsync(string customerId, CancellationToken ct);
}

public sealed class TrustLedgerRepository : ITrustLedgerRepository
{
    private readonly string _connectionString;

    public TrustLedgerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<IReadOnlyList<TrustLedgerSummaryDto>> GetOwnerSummariesAsync(string customerId, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<TrustLedgerSummaryDto>(@"
            SELECT o.OwnerId,
                   o.FullName AS OwnerName,
                   COALESCE(SUM(CASE WHEN tl.TransactionType = 'RENT_IN' THEN tl.Amount ELSE 0 END), 0) AS TotalRentIn,
                   COALESCE(SUM(CASE WHEN tl.TransactionType = 'DISBURSEMENT' THEN tl.Amount ELSE 0 END), 0) AS TotalDisbursed,
                   COALESCE(SUM(CASE WHEN tl.TransactionType = 'MGMT_FEE' THEN tl.Amount ELSE 0 END), 0) AS TotalFees,
                   COALESCE((SELECT tl2.RunningBalance FROM TrustLedger tl2
                              WHERE tl2.OwnerId = o.OwnerId AND tl2.CustomerId = @CustomerId
                              ORDER BY tl2.TransactionDate DESC, tl2.TrustLedgerId DESC LIMIT 1), 0) AS CurrentBalance
            FROM Owners o
            LEFT JOIN TrustLedger tl ON tl.OwnerId = o.OwnerId AND tl.CustomerId = @CustomerId
            WHERE o.CustomerId = @CustomerId
            GROUP BY o.OwnerId, o.FullName
            ORDER BY o.FullName",
            new { CustomerId = customerId });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<TrustLedgerEntryDto>> GetEntriesForOwnerAsync(string customerId, long ownerId, int limit, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        limit = Math.Min(limit, 200);

        var rows = await conn.QueryAsync<TrustLedgerEntryDto>(@"
            SELECT tl.TrustLedgerId, tl.OwnerId,
                   o.FullName AS OwnerName,
                   NULL AS PropertyId,
                   NULL AS PropertyAddress,
                   tl.TransactionType, tl.Amount, tl.RunningBalance,
                   tl.TransactionDate, tl.Reference, tl.Description, tl.CreatedAtUtc
            FROM TrustLedger tl
            JOIN Owners o ON o.OwnerId = tl.OwnerId
            WHERE tl.CustomerId = @CustomerId
              AND tl.OwnerId = @OwnerId
            ORDER BY tl.TransactionDate DESC, tl.TrustLedgerId DESC
            LIMIT @Limit",
            new { CustomerId = customerId, OwnerId = ownerId, Limit = limit });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<ManagementFeeScheduleDto>> GetFeeSchedulesAsync(string customerId, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<ManagementFeeScheduleDto>(@"
            SELECT mfs.ScheduleId AS FeeScheduleId, mfs.OwnerId,
                   o.FullName AS OwnerName,
                   mfs.PropertyId,
                   CONCAT(p.AddressLine1, ', ', p.Suburb) AS PropertyAddress,
                   mfs.FeeType, mfs.FeeValue AS FeePercent, NULL AS FeeFixed,
                   1 AS IsActive, mfs.EffectiveFrom, mfs.EffectiveTo
            FROM ManagementFeeSchedules mfs
            JOIN Owners o ON o.OwnerId = mfs.OwnerId
            LEFT JOIN Properties p ON p.PropertyId = mfs.PropertyId
            WHERE mfs.CustomerId = @CustomerId
              AND (mfs.EffectiveTo IS NULL OR mfs.EffectiveTo >= CURRENT_DATE)
            ORDER BY o.FullName, mfs.FeeType",
            new { CustomerId = customerId });

        return rows.ToList();
    }
}
