using Application.Abstractions;
using Contracts.Agent;
using Contracts.Requests;
using Contracts.Responses;
using System.Globalization;
using System.Linq;

namespace Application.Orchestration;

/// <summary>
/// Main orchestrator: coordinates agent→firewall→execution pipeline
/// </summary>
public sealed class NlSqlOrchestrator(
    INl2SqlAgentClient agentClient,
    ISchemaPolicyProvider policyProvider,
    ISqlFirewall sqlFirewall,
    IQueryExecutor queryExecutor,
    IAuditLogger auditLogger) : INlSqlOrchestrator
{
    private const double MinimumExecutionConfidence = 0.55;

    public async Task<QueryResponse> HandleAsync(
        NlQueryRequest request,
        string customerId,
        string userId,
        string role,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var policy = policyProvider.GetPolicy();

        try
        {
            if (!IsMySqlDialect(policy.Dialect))
            {
                await LogAuditAsync(requestId, userId, customerId, role, request.Question,
                    null, string.Empty, null, "blocked", Array.Empty<string>(), Array.Empty<string>(), 0, 0,
                    "UNSUPPORTED_DIALECT", $"Unsupported SQL dialect: {policy.Dialect}", cancellationToken);

                return new QueryResponse
                {
                    RequestId = requestId,
                    Status = "blocked",
                    Message = "This environment currently supports MySQL queries only.",
                    Explanation = "The query assistant is configured for MySQL 8-compatible execution only.",
                    ErrorCode = "UNSUPPORTED_DIALECT"
                };
            }

            // Step 1: Call Python LangGraph service to generate SQL candidate
            var agentResponse = await agentClient.GenerateAsync(new Nl2SqlGenerateRequest
            {
                Question = request.Question,
                Context = new UserContext
                {
                    CustomerId = customerId,
                    UserId = userId,
                    Role = role
                },
                Constraints = new Nl2SqlConstraints
                {
                    Dialect = policy.Dialect,
                    TenantColumn = policy.TenantColumn,
                    DefaultLimit = policy.DefaultLimit,
                    MaxLimit = policy.MaxLimit,
                    SelectOnly = policy.SelectOnly,
                    AllowedTables = policy.AllowedTables,
                    AllowedFunctions = policy.AllowedFunctions
                },
                ConversationId = request.ConversationId
            }, cancellationToken);

            // Step 2: Handle clarification requests
            if (agentResponse.NeedsClarification || agentResponse.Confidence < MinimumExecutionConfidence)
            {
                var clarificationMessage = agentResponse.ClarificationPrompt
                    ?? BuildFallbackClarificationPrompt(agentResponse.DetectedDomain);

                await LogAuditAsync(requestId, userId, customerId, role, request.Question,
                    agentResponse.DetectedDomain ?? "unknown", agentResponse.SqlCandidate, null, "clarification_needed",
                    Array.Empty<string>(), Array.Empty<string>(), 0, 0, null, null, cancellationToken);

                return new QueryResponse
                {
                    RequestId = requestId,
                    Status = "clarification_needed",
                    Domain = agentResponse.DetectedDomain ?? "unknown",
                    Message = clarificationMessage,
                    Explanation = BuildClarificationExplanation(agentResponse.DetectedDomain, agentResponse.Confidence)
                };
            }

            // Step 3: Run SQL through firewall (parse/validate/rewrite)
            var firewallResult = sqlFirewall.ValidateAndRewrite(
                agentResponse.SqlCandidate,
                customerId,
                policy);

            if (!firewallResult.Approved || string.IsNullOrWhiteSpace(firewallResult.RewrittenSql))
            {
                await LogAuditAsync(requestId, userId, customerId, role, request.Question,
                    agentResponse.DetectedDomain ?? "unknown", agentResponse.SqlCandidate, null, "blocked",
                    firewallResult.RuleHits, firewallResult.Violations, 0, 0, "SQL_FIREWALL_REJECTED",
                    firewallResult.Reason, cancellationToken);

                return new QueryResponse
                {
                    RequestId = requestId,
                    Status = "blocked",
                    Domain = agentResponse.DetectedDomain ?? "unknown",
                    Message = firewallResult.Reason ?? "Query blocked by security policy.",
                    Explanation = BuildBlockedExplanation(agentResponse.DetectedDomain),
                    ErrorCode = "SQL_FIREWALL_REJECTED"
                };
            }

            // Step 4: Execute approved SQL
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var (rows, columns, executionMs) = await queryExecutor.ExecuteAsync(
                firewallResult.RewrittenSql,
                new { customerId },
                policy.TimeoutPolicyMs,
                cancellationToken);
            sw.Stop();

            // Step 5: Log success audit
            await LogAuditAsync(requestId, userId, customerId, role, request.Question,
                agentResponse.DetectedDomain ?? "unknown", agentResponse.SqlCandidate, firewallResult.RewrittenSql,
                "ok", firewallResult.RuleHits, Array.Empty<string>(), executionMs, rows.Count, null, null, cancellationToken);

            return new QueryResponse
            {
                RequestId = requestId,
                Status = "ok",
                Domain = agentResponse.DetectedDomain ?? "unknown",
                Rows = rows,
                Columns = columns,
                RowCount = rows.Count,
                ExecutionMs = executionMs,
                Explanation = BuildSuccessExplanation(
                    agentResponse.DetectedDomain,
                    agentResponse.ScopedTables,
                    rows.Count,
                    firewallResult.RuleHits)
            };
        }
        catch (Exception ex)
        {
            await LogAuditAsync(requestId, userId, customerId, role, request.Question,
                null, string.Empty, null, "error", Array.Empty<string>(), Array.Empty<string>(),
                0, 0, "EXECUTION_ERROR", ex.Message, cancellationToken);

            return new QueryResponse
            {
                RequestId = requestId,
                Status = "error",
                Message = "An error occurred while processing your query.",
                Explanation = "The request could not be completed. Please rephrase your question and try again.",
                ErrorCode = "EXECUTION_ERROR"
            };
        }
    }

    private static bool IsMySqlDialect(string dialect) =>
        !string.IsNullOrWhiteSpace(dialect)
        && dialect.StartsWith("mysql", StringComparison.OrdinalIgnoreCase);

    private static string BuildFallbackClarificationPrompt(string? detectedDomain)
    {
        var domainHint = string.IsNullOrWhiteSpace(detectedDomain) || detectedDomain.Equals("general", StringComparison.OrdinalIgnoreCase)
            ? "your request"
            : $"{detectedDomain} request";

        return $"I can help with this {domainHint}, but I need one more detail such as a date range, status, or property scope.";
    }

    private static string BuildClarificationExplanation(string? detectedDomain, double confidence)
    {
        var domainLabel = string.IsNullOrWhiteSpace(detectedDomain) ? "general" : detectedDomain;
        var confidencePercent = Math.Round(confidence * 100, MidpointRounding.AwayFromZero);
        return $"The question was interpreted as '{domainLabel}' with {confidencePercent.ToString(CultureInfo.InvariantCulture)}% confidence, so more detail is needed before executing.";
    }

    private static string BuildBlockedExplanation(string? detectedDomain)
    {
        var domainLabel = string.IsNullOrWhiteSpace(detectedDomain) ? "general" : detectedDomain;
        return $"A {domainLabel} query was generated but blocked by safety rules before execution.";
    }

    private static string BuildSuccessExplanation(
        string? detectedDomain,
        IReadOnlyList<string> scopedTables,
        int rowCount,
        IReadOnlyList<string> ruleHits)
    {
        var domainLabel = string.IsNullOrWhiteSpace(detectedDomain) ? "general" : detectedDomain;
        var scopedTablesText = scopedTables.Count > 0
            ? string.Join(", ", scopedTables.Take(3))
            : "allowed property-management tables";
        var rowLabel = rowCount == 1 ? "row" : "rows";
        var limitApplied = ruleHits.Contains("R007_LIMIT_INJECTED", StringComparer.OrdinalIgnoreCase)
            || ruleHits.Contains("R007_LIMIT_CAPPED", StringComparer.OrdinalIgnoreCase);
        var limitNote = limitApplied ? " Result limits were applied for safety and performance." : string.Empty;

        return $"Ran a {domainLabel} query over {scopedTablesText} and returned {rowCount} {rowLabel}.{limitNote}";
    }

    private Task LogAuditAsync(
        string requestId, string userId, string customerId, string role, string? question,
        string? domain, string candidateSql, string? approvedSql, string status,
        IReadOnlyList<string> ruleHits, IReadOnlyList<string> violations,
        long executionMs, int rowCount, string? errorCode, string? errorMessage,
        CancellationToken cancellationToken)
    {
        return auditLogger.LogAsync(new AuditEvent
        {
            RequestId = requestId,
            UserId = userId,
            CustomerId = customerId,
            Role = role,
            Question = question,
            Domain = domain,
            CandidateSql = candidateSql,
            ApprovedSql = approvedSql,
            Status = status,
            RuleHits = ruleHits,
            Violations = violations,
            ExecutionMs = executionMs,
            RowCount = rowCount,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        }, cancellationToken);
    }
}
