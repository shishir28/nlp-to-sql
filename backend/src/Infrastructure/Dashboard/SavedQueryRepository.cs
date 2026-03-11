using Contracts.Dashboard;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.Dashboard;

public interface ISavedQueryRepository
{
    Task<IReadOnlyList<SavedQueryDto>> GetByCustomerAsync(string customerId, CancellationToken ct);
    Task<SavedQueryDto> SaveAsync(string customerId, string userId, SaveQueryRequest request, CancellationToken ct);
    Task DeleteAsync(int id, string customerId, CancellationToken ct);
}

public sealed class SavedQueryRepository : ISavedQueryRepository
{
    private readonly string _connectionString;

    public SavedQueryRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<IReadOnlyList<SavedQueryDto>> GetByCustomerAsync(string customerId, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var rows = await connection.QueryAsync<SavedQueryDto>(@"
            SELECT Id, Name, Question, Role, IsPinned, CreatedAtUtc
            FROM SavedQueries
            WHERE CustomerId = @CustomerId
            ORDER BY CreatedAtUtc DESC
            LIMIT 50",
            new { CustomerId = customerId });

        return rows.ToList();
    }

    public async Task<SavedQueryDto> SaveAsync(string customerId, string userId, SaveQueryRequest request, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var id = await connection.ExecuteScalarAsync<int>(@"
            INSERT INTO SavedQueries (CustomerId, UserId, Name, Question, Role, IsPinned, CreatedAtUtc)
            VALUES (@CustomerId, @UserId, @Name, @Question, @Role, 1, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();",
            new
            {
                CustomerId = customerId,
                UserId = userId,
                request.Name,
                request.Question,
                request.Role
            });

        return new SavedQueryDto
        {
            Id = id,
            Name = request.Name,
            Question = request.Question,
            Role = request.Role,
            IsPinned = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public async Task DeleteAsync(int id, string customerId, CancellationToken ct)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            "DELETE FROM SavedQueries WHERE Id = @Id AND CustomerId = @CustomerId",
            new { Id = id, CustomerId = customerId });
    }
}
