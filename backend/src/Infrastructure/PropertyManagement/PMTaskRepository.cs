using Contracts.PropertyManagement;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace Infrastructure.PropertyManagement;

public interface IPMTaskRepository
{
    Task<PMTaskSummaryDto> GetSummaryAsync(string customerId, string? assignedToUserId, CancellationToken ct);
    Task<IReadOnlyList<PMTaskDto>> GetTasksAsync(string customerId, string? assignedToUserId, string? status, CancellationToken ct);
    Task<PMTaskDto> CreateAsync(string customerId, string createdByUserId, CreatePMTaskRequest request, CancellationToken ct);
    Task<bool> UpdateStatusAsync(string customerId, long taskId, UpdatePMTaskStatusRequest request, CancellationToken ct);
}

public sealed class PMTaskRepository : IPMTaskRepository
{
    private readonly string _connectionString;

    public PMTaskRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
    }

    public async Task<PMTaskSummaryDto> GetSummaryAsync(string customerId, string? assignedToUserId, CancellationToken ct)
    {
        var tasks = await GetTasksAsync(customerId, assignedToUserId, null, ct);
        var open = tasks.Where(t => t.Status != "DONE" && t.Status != "CANCELLED").ToList();
        var today = DateTime.UtcNow.Date;
        var weekEnd = today.AddDays(7);

        return new PMTaskSummaryDto
        {
            OpenCount = open.Count,
            OverdueCount = open.Count(t => t.IsOverdue),
            DueTodayCount = open.Count(t => t.DueDate.HasValue && t.DueDate.Value.Date == today),
            DueThisWeekCount = open.Count(t => t.DueDate.HasValue && t.DueDate.Value.Date <= weekEnd),
            Tasks = tasks
        };
    }

    public async Task<IReadOnlyList<PMTaskDto>> GetTasksAsync(string customerId, string? assignedToUserId, string? status, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<PMTaskDto>(@"
            SELECT pt.TaskId, pt.Title, pt.Description,
                   COALESCE(pt.RelatedEntityType, 'GENERAL') AS Category,
                   pt.Priority, pt.StatusCode AS Status, pt.AssignedToUserId,
                   pt.DueDate,
                   CASE WHEN pt.DueDate < CURRENT_DATE AND pt.StatusCode NOT IN ('DONE','CANCELLED') THEN 1 ELSE 0 END AS IsOverdue,
                   NULL AS PropertyId,
                   NULL AS PropertyAddress,
                   NULL AS TenancyId, NULL AS MaintenanceJobId,
                   pt.CreatedAtUtc, NULL AS CompletedAtUtc
            FROM PMTasks pt
            WHERE pt.CustomerId = @CustomerId
              AND (@AssignedToUserId IS NULL OR pt.AssignedToUserId = @AssignedToUserId)
              AND (@Status IS NULL OR pt.StatusCode = @Status)
            ORDER BY
                FIELD(pt.Priority, 'URGENT', 'HIGH', 'MEDIUM', 'LOW'),
                pt.DueDate ASC
            LIMIT 100",
            new { CustomerId = customerId, AssignedToUserId = assignedToUserId, Status = status });

        return rows.ToList();
    }

    public async Task<PMTaskDto> CreateAsync(string customerId, string createdByUserId, CreatePMTaskRequest request, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var id = await conn.ExecuteScalarAsync<long>(@"
            INSERT INTO PMTasks (CustomerId, Title, Description, RelatedEntityType, Priority, StatusCode,
                                  AssignedToUserId, DueDate, CreatedAtUtc)
            VALUES (@CustomerId, @Title, @Description, @Category, @Priority, 'OPEN',
                    @AssignedToUserId, @DueDate, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();",
            new
            {
                CustomerId = customerId,
                request.Title,
                request.Description,
                request.Category,
                request.Priority,
                request.AssignedToUserId,
                request.DueDate
            });

        return new PMTaskDto
        {
            TaskId = id,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            Priority = request.Priority,
            Status = "OPEN",
            AssignedToUserId = request.AssignedToUserId,
            DueDate = request.DueDate,
            PropertyId = request.PropertyId,
            TenancyId = request.TenancyId,
            MaintenanceJobId = request.MaintenanceJobId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<bool> UpdateStatusAsync(string customerId, long taskId, UpdatePMTaskStatusRequest request, CancellationToken ct)
    {
        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var affected = await conn.ExecuteAsync(@"
            UPDATE PMTasks
            SET StatusCode = @Status,
                CompletedDate = CASE WHEN @Status = 'DONE' THEN UTC_TIMESTAMP() ELSE CompletedDate END
            WHERE TaskId = @TaskId AND CustomerId = @CustomerId",
            new { TaskId = taskId, CustomerId = customerId, request.Status });

        return affected > 0;
    }
}
