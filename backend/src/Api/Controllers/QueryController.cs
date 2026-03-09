using System.Net.Http.Json;
using System.Text;
using Application.Abstractions;
using Contracts.Agent;
using Contracts.Requests;
using Contracts.Responses;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly INlSqlOrchestrator _orchestrator;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        INlSqlOrchestrator orchestrator,
        ILogger<QueryController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<QueryResponse>> ExecuteQuery(
        [FromBody] NlQueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new QueryResponse
                {
                    Status = "error",
                    Message = "Please provide a non-empty natural language question.",
                    ErrorCode = "INVALID_REQUEST"
                });
            }

            // Extract context from auth claims; fall back to request body for dev/demo
            var customerId = User.FindFirst("customer_id")?.Value
                ?? request.CustomerId ?? "1";
            var userId = User.FindFirst("sub")?.Value ?? "local-dev-user";
            var role = User.FindFirst("role")?.Value
                ?? request.Role ?? "PropertyManager";

            _logger.LogInformation(
                "Query request from user={UserId}, customer={CustomerId}, role={Role}, question={Question}",
                userId, customerId, role, request.Question);

            var response = await _orchestrator.HandleAsync(
                request,
                customerId,
                userId,
                role,
                cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute query: {Message}", ex.Message);
            return StatusCode(500, new QueryResponse
            {
                Status = "error",
                Message = "Internal server error.",
                ErrorCode = "INTERNAL_ERROR"
            });
        }
    }

    /// <summary>
    /// SSE streaming proxy: forwards the Python agent's SSE stream to the browser.
    /// The Angular client connects here via fetch() and receives events:
    ///   agent_start, sql_generated, validation_done, done, error
    /// </summary>
    [HttpPost("stream")]
    public async Task StreamQuery(
        [FromBody] NlQueryRequest request,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IConfiguration configuration,
        [FromServices] ISchemaPolicyProvider policyProvider,
        CancellationToken cancellationToken)
    {
        var customerId = User.FindFirst("customer_id")?.Value
            ?? request.CustomerId ?? "1";
        var userId = User.FindFirst("sub")?.Value ?? "local-dev-user";
        var role = User.FindFirst("role")?.Value
            ?? request.Role ?? "PropertyManager";

        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var agentBaseUrl = configuration["AgentService:BaseUrl"]
            ?? "http://localhost:8000";
        var streamEndpoint = $"{agentBaseUrl}/v1/nl2sql/stream";

        var httpClient = httpClientFactory.CreateClient();
        var policy = policyProvider.GetPolicy();

        var agentRequest = new Nl2SqlGenerateRequest
        {
            Question = request.Question,
            ConversationId = request.ConversationId,
            Context = new UserContext { CustomerId = customerId, UserId = userId, Role = role },
            Constraints = new Nl2SqlConstraints
            {
                Dialect = policy.Dialect,
                TenantColumn = policy.TenantColumn,
                DefaultLimit = policy.DefaultLimit,
                MaxLimit = policy.MaxLimit,
                SelectOnly = policy.SelectOnly,
                AllowedTables = policy.AllowedTables,
                AllowedFunctions = policy.AllowedFunctions
            }
        };

        try
        {
            var upstreamResponse = await httpClient.PostAsJsonAsync(
                streamEndpoint, agentRequest, cancellationToken);

            upstreamResponse.EnsureSuccessStatusCode();

            await using var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = await upstreamStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await Response.Body.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE proxy error: {Message}", ex.Message);
            var errorEvent = $"event: error\ndata: {{\"message\": \"{ex.Message}\"}}\n\n";
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(errorEvent), cancellationToken);
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "nl2sql-api", timestamp = DateTime.UtcNow });
    }
}
