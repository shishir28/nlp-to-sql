using Contracts.Dashboard;
using Infrastructure.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduledReportsController : ControllerBase
{
    private readonly IScheduledReportRepository _repo;

    public ScheduledReportsController(IScheduledReportRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduledReportDto>>> GetAll(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var reports = await _repo.GetByCustomerAsync(customerId, cancellationToken);
        return Ok(reports);
    }

    [HttpPost]
    public async Task<ActionResult<ScheduledReportDto>> Create(
        [FromBody] CreateScheduledReportRequest request,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Question)
            || string.IsNullOrWhiteSpace(request.RecipientEmail))
            return BadRequest(new { message = "Name, Question, and RecipientEmail are required." });

        var userId = User.FindFirst("sub")?.Value ?? "local-dev-user";
        var report = await _repo.CreateAsync(customerId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { customerId }, report);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        await _repo.DeleteAsync(id, customerId, cancellationToken);
        return NoContent();
    }
}
