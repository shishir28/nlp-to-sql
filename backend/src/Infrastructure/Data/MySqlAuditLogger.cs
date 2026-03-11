using Application.Abstractions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Infrastructure.Data;

/// <summary>
/// Writes audit events to QueryAuditLog table in MySQL (and falls back to console)
/// </summary>
public sealed class MySqlAuditLogger : IAuditLogger
{
    private readonly string _connectionString;
    private readonly ILogger<MySqlAuditLogger> _logger;

    public MySqlAuditLogger(IConfiguration configuration, ILogger<MySqlAuditLogger> logger)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException("MySql connection string not configured");
        _logger = logger;
    }

    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO QueryAuditLog
                    (RequestId, CustomerId, UserId, Role, Question, Domain, Status, ExecutionMs, RowCount, ErrorCode, CreatedAtUtc)
                VALUES
                    (@RequestId, @CustomerId, @UserId, @Role, @Question, @Domain, @Status, @ExecutionMs, @RowCount, @ErrorCode, UTC_TIMESTAMP())",
                new
                {
                    auditEvent.RequestId,
                    auditEvent.CustomerId,
                    auditEvent.UserId,
                    auditEvent.Role,
                    auditEvent.Question,
                    auditEvent.Domain,
                    auditEvent.Status,
                    auditEvent.ExecutionMs,
                    auditEvent.RowCount,
                    auditEvent.ErrorCode
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist audit event to MySQL; event: {RequestId}", auditEvent.RequestId);
        }
    }
}
