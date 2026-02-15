using Contracts.Agent;
using Contracts.Requests;
using Contracts.Responses;

namespace Application.Abstractions;

/// <summary>
/// Main orchestrator coordinating NL→SQL workflow
/// </summary>
public interface INlSqlOrchestrator
{
    Task<QueryResponse> HandleAsync(
        NlQueryRequest request,
        string customerId,
        string userId,
        string role,
        CancellationToken cancellationToken);
}

/// <summary>
/// Client for Python LangGraph multi-agent service
/// </summary>
public interface INl2SqlAgentClient
{
    Task<Nl2SqlGenerateResponse> GenerateAsync(
        Nl2SqlGenerateRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Provides schema policy from JSON configuration
/// </summary>
public interface ISchemaPolicyProvider
{
    SchemaPolicy GetPolicy();
}

/// <summary>
/// SQL firewall: validates, rewrites, injects tenant predicates
/// </summary>
public interface ISqlFirewall
{
    FirewallResult ValidateAndRewrite(
        string sqlCandidate,
        string customerId,
        SchemaPolicy policy);
}

/// <summary>
/// Executes approved SQL against MySQL
/// </summary>
public interface IQueryExecutor
{
    Task<(IReadOnlyList<Dictionary<string, object?>> Rows, IReadOnlyList<ColumnInfo> Columns, long ExecutionMs)> ExecuteAsync(
        string sql,
        object parameters,
        int timeoutMs,
        CancellationToken cancellationToken);
}

/// <summary>
/// Logs audit events for compliance
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
