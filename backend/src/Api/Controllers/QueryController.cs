using Application.Abstractions;
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

            // Extract context from auth claims (fallback to dev values for local testing)
            var customerId = User.FindFirst("customer_id")?.Value ?? "1";
            var userId = User.FindFirst("sub")?.Value ?? "local-dev-user";
            var role = User.FindFirst("role")?.Value ?? "PropertyManager";

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

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "nl2sql-api", timestamp = DateTime.UtcNow });
    }
}
