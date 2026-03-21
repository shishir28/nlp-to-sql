using Contracts.PropertyManagement;
using Infrastructure.PropertyManagement;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/maintenance-workflow")]
public class MaintenanceWorkflowController : ControllerBase
{
    private readonly IMaintenanceWorkflowRepository _repo;

    public MaintenanceWorkflowController(IMaintenanceWorkflowRepository repo) => _repo = repo;

    [HttpGet("awaiting-approval")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceJobWorkflowDto>>> GetJobsAwaitingApproval(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetJobsAwaitingApprovalAsync(customerId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("jobs/{jobId:long}/quotes")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceQuoteDto>>> GetQuotes(
        long jobId,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetQuotesForJobAsync(customerId, jobId, cancellationToken);
        return Ok(results);
    }

    [HttpPost("quotes/{quoteId:long}/approve")]
    public async Task<IActionResult> ApproveQuote(
        long quoteId,
        [FromBody] ApproveQuoteRequest request,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApprovedByUser))
            return BadRequest(new { message = "ApprovedByUser is required." });

        var ok = await _repo.ApproveQuoteAsync(customerId, quoteId, request, cancellationToken);
        if (!ok) return NotFound(new { message = "Quote not found or already processed." });

        return Ok(new { message = "Quote approved." });
    }
}
