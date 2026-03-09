using Contracts.Dashboard;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.Dashboard;

public interface IDashboardWidgetRepository
{
    Task<IReadOnlyList<DashboardWidgetDto>> GetByCustomerAsync(string customerId, CancellationToken ct);
    Task<DashboardWidgetDto> SaveAsync(string customerId, string userId, SaveDashboardWidgetRequest request, CancellationToken ct);
    Task UpdateViewTypeAsync(int id, string customerId, string viewType, CancellationToken ct);
    Task DeleteAsync(int id, string customerId, CancellationToken ct);
    Task DeleteAllAsync(string customerId, CancellationToken ct);
}

public sealed class DashboardWidgetRepository : IDashboardWidgetRepository
{
    private readonly string _connectionString;

    public DashboardWidgetRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<IReadOnlyList<DashboardWidgetDto>> GetByCustomerAsync(string customerId, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        var rows = await connection.QueryAsync<DashboardWidgetDto>(@"
            SELECT Id, Title, Question, ViewType, SortOrder, CreatedAtUtc
            FROM DashboardWidgets
            WHERE CustomerId = @CustomerId
            ORDER BY SortOrder ASC, CreatedAtUtc ASC",
            new { CustomerId = customerId });
        return rows.ToList();
    }

    public async Task<DashboardWidgetDto> SaveAsync(
        string customerId, string userId, SaveDashboardWidgetRequest request, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var id = await connection.ExecuteScalarAsync<int>(@"
            INSERT INTO DashboardWidgets (CustomerId, UserId, Title, Question, ViewType, SortOrder, CreatedAtUtc)
            VALUES (@CustomerId, @UserId, @Title, @Question, @ViewType, @SortOrder, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();",
            new
            {
                CustomerId = customerId,
                UserId = userId,
                request.Title,
                request.Question,
                request.ViewType,
                request.SortOrder
            });

        return new DashboardWidgetDto
        {
            Id = id,
            Title = request.Title,
            Question = request.Question,
            ViewType = request.ViewType,
            SortOrder = request.SortOrder,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public async Task UpdateViewTypeAsync(int id, string customerId, string viewType, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            "UPDATE DashboardWidgets SET ViewType = @ViewType WHERE Id = @Id AND CustomerId = @CustomerId",
            new { Id = id, CustomerId = customerId, ViewType = viewType });
    }

    public async Task DeleteAsync(int id, string customerId, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            "DELETE FROM DashboardWidgets WHERE Id = @Id AND CustomerId = @CustomerId",
            new { Id = id, CustomerId = customerId });
    }

    public async Task DeleteAllAsync(string customerId, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            "DELETE FROM DashboardWidgets WHERE CustomerId = @CustomerId",
            new { CustomerId = customerId });
    }
}
