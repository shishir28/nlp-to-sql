using System.Text.Json;
using Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Logging;

/// <summary>
/// Audit logger that writes structured JSON to ILogger
/// </summary>
public sealed class ConsoleAuditLogger : IAuditLogger
{
    private readonly ILogger<ConsoleAuditLogger> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConsoleAuditLogger(ILogger<ConsoleAuditLogger> logger)
    {
        _logger = logger;
    }

    public Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(auditEvent, JsonOptions);
            _logger.LogInformation("AUDIT {AuditJson}", json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize audit event");
        }

        return Task.CompletedTask;
    }
}
