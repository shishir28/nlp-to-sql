using Application.Abstractions;
using Contracts.Agent;
using Contracts.Requests;
using Contracts.Responses;

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
                    TenantColumn = "CustomerId",
                    DefaultLimit = 50,
                    MaxLimit = policy.MaxLimit,
                    SelectOnly = true
                }
            }, cancellationToken);

            // Step 2: Handle clarification requests
            if (agentResponse.NeedsClarification || agentResponse.Confidence < 0.5)
            {
                await LogAuditAsync(requestId, userId, customerId, role, request.Question,
                    agentResponse.DetectedDomain ?? "unknown", agentResponse.SqlCandidate, null, "clarification_needed",
                    Array.Empty<string>(), Array.Empty<string>(), 0, 0, null, null, cancellationToken);

                return new QueryResponse
                {
                    RequestId = requestId,
                    Status = "clarification_needed",
                    Domain = agentResponse.DetectedDomain ?? "unknown",
                    Message = agentResponse.ClarificationPrompt ?? "Please provide more details about your query."
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
                ExecutionMs = executionMs
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
                ErrorCode = "EXECUTION_ERROR"
            };
        }
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
