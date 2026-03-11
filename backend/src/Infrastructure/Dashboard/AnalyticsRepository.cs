using Contracts.Dashboard;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.Dashboard;

public interface IAnalyticsRepository
{
    Task<AnalyticsSummary> GetSummaryAsync(string customerId, int days, CancellationToken ct);
}

public sealed class AnalyticsRepository : IAnalyticsRepository
{
    private readonly string _connectionString;

    public AnalyticsRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<AnalyticsSummary> GetSummaryAsync(string customerId, int days, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var since = DateTime.UtcNow.AddDays(-days);

        var totals = await connection.QueryFirstOrDefaultAsync<(int Total, int Ok, int Failed, double AvgMs)>(@"
            SELECT
                COUNT(*)                                               AS Total,
                SUM(CASE WHEN Status = 'ok' THEN 1 ELSE 0 END)       AS Ok,
                SUM(CASE WHEN Status NOT IN ('ok','clarification_needed') THEN 1 ELSE 0 END) AS Failed,
                COALESCE(AVG(CASE WHEN Status = 'ok' THEN ExecutionMs END), 0) AS AvgMs
            FROM QueryAuditLog
            WHERE CustomerId = @CustomerId AND CreatedAtUtc >= @Since",
            new { CustomerId = customerId, Since = since });

        var domains = (await connection.QueryAsync<DomainCount>(@"
            SELECT COALESCE(Domain, 'unknown') AS Domain, COUNT(*) AS Count
            FROM QueryAuditLog
            WHERE CustomerId = @CustomerId AND CreatedAtUtc >= @Since AND Status = 'ok'
            GROUP BY Domain
            ORDER BY Count DESC
            LIMIT 10",
            new { CustomerId = customerId, Since = since })).ToList();

        var daily = (await connection.QueryAsync<(string Date, int Count)>(@"
            SELECT DATE_FORMAT(CreatedAtUtc, '%Y-%m-%d') AS Date, COUNT(*) AS Count
            FROM QueryAuditLog
            WHERE CustomerId = @CustomerId AND CreatedAtUtc >= @Since
            GROUP BY DATE_FORMAT(CreatedAtUtc, '%Y-%m-%d')
            ORDER BY Date",
            new { CustomerId = customerId, Since = since }))
            .Select(r => new DailyCount { Date = r.Date, Count = r.Count })
            .ToList();

        var recent = (await connection.QueryAsync<RecentQuery>(@"
            SELECT Question, COALESCE(Domain,'unknown') AS Domain, Status, ExecutionMs, CreatedAtUtc
            FROM QueryAuditLog
            WHERE CustomerId = @CustomerId
            ORDER BY CreatedAtUtc DESC
            LIMIT 20",
            new { CustomerId = customerId })).ToList();

        return new AnalyticsSummary
        {
            TotalQueries = totals.Total,
            SuccessfulQueries = totals.Ok,
            FailedQueries = totals.Failed,
            AvgExecutionMs = Math.Round(totals.AvgMs, 0),
            DomainBreakdown = domains,
            DailyTrend = daily,
            RecentQueries = recent
        };
    }
}
