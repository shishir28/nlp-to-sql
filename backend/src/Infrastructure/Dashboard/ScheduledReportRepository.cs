using Contracts.Dashboard;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.Dashboard;

public interface IScheduledReportRepository
{
    Task<IReadOnlyList<ScheduledReportDto>> GetByCustomerAsync(string customerId, CancellationToken ct);
    Task<ScheduledReportDto> CreateAsync(string customerId, string userId, CreateScheduledReportRequest request, CancellationToken ct);
    Task DeleteAsync(int id, string customerId, CancellationToken ct);
    Task<IReadOnlyList<ScheduledReportDto>> GetDueReportsAsync(CancellationToken ct);
    Task UpdateLastRunAsync(int id, DateTime nextRunAt, CancellationToken ct);
}

public sealed class ScheduledReportRepository : IScheduledReportRepository
{
    private readonly string _connectionString;

    public ScheduledReportRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<IReadOnlyList<ScheduledReportDto>> GetByCustomerAsync(string customerId, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        var rows = await connection.QueryAsync<ScheduledReportDto>(@"
            SELECT Id, Name, Question, Role, RecipientEmail, Schedule, AlertCondition, IsActive, LastRunAtUtc, NextRunAtUtc, CreatedAtUtc
            FROM ScheduledReports
            WHERE CustomerId = @CustomerId
            ORDER BY CreatedAtUtc DESC",
            new { CustomerId = customerId });
        return rows.ToList();
    }

    public async Task<ScheduledReportDto> CreateAsync(string customerId, string userId, CreateScheduledReportRequest request, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var nextRun = ComputeNextRun(request.Schedule, DateTime.UtcNow);

        var id = await connection.ExecuteScalarAsync<int>(@"
            INSERT INTO ScheduledReports (CustomerId, UserId, Name, Question, Role, RecipientEmail, Schedule, AlertCondition, IsActive, NextRunAtUtc, CreatedAtUtc)
            VALUES (@CustomerId, @UserId, @Name, @Question, @Role, @RecipientEmail, @Schedule, @AlertCondition, 1, @NextRunAtUtc, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();",
            new
            {
                CustomerId = customerId,
                UserId = userId,
                request.Name,
                request.Question,
                request.Role,
                request.RecipientEmail,
                request.Schedule,
                AlertCondition = request.AlertCondition,
                NextRunAtUtc = nextRun
            });

        return new ScheduledReportDto
        {
            Id = id,
            Name = request.Name,
            Question = request.Question,
            Role = request.Role,
            RecipientEmail = request.RecipientEmail,
            Schedule = request.Schedule,
            AlertCondition = request.AlertCondition,
            IsActive = true,
            NextRunAtUtc = nextRun,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public async Task DeleteAsync(int id, string customerId, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            "DELETE FROM ScheduledReports WHERE Id = @Id AND CustomerId = @CustomerId",
            new { Id = id, CustomerId = customerId });
    }

    public async Task<IReadOnlyList<ScheduledReportDto>> GetDueReportsAsync(CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        var rows = await connection.QueryAsync<ScheduledReportDto>(@"
            SELECT Id, Name, Question, Role, RecipientEmail, Schedule, AlertCondition, IsActive, LastRunAtUtc, NextRunAtUtc, CreatedAtUtc
            FROM ScheduledReports
            WHERE IsActive = 1 AND NextRunAtUtc <= UTC_TIMESTAMP()");
        return rows.ToList();
    }

    public async Task UpdateLastRunAsync(int id, DateTime nextRunAt, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(@"
            UPDATE ScheduledReports
            SET LastRunAtUtc = UTC_TIMESTAMP(), NextRunAtUtc = @NextRunAtUtc
            WHERE Id = @Id",
            new { Id = id, NextRunAtUtc = nextRunAt });
    }

    public static DateTime ComputeNextRun(string schedule, DateTime from) => schedule switch
    {
        "weekly"  => from.AddDays(7),
        "monthly" => from.AddMonths(1),
        _         => from.AddDays(1),  // daily (default)
    };
}
