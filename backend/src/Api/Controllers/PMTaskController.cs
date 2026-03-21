using Contracts.PropertyManagement;
using Infrastructure.PropertyManagement;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/pm-tasks")]
public class PMTaskController : ControllerBase
{
    private readonly IPMTaskRepository _repo;

    public PMTaskController(IPMTaskRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PMTaskDto>>> GetTasks(
        [FromQuery] string customerId = "1",
        [FromQuery] string? assignedTo = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetTasksAsync(customerId, assignedTo, status, cancellationToken);
        return Ok(results);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<PMTaskSummaryDto>> GetSummary(
        [FromQuery] string customerId = "1",
        [FromQuery] string? assignedTo = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _repo.GetSummaryAsync(customerId, assignedTo, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PMTaskDto>> CreateTask(
        [FromBody] CreatePMTaskRequest request,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Title is required." });

        var userId = User.FindFirst("sub")?.Value ?? "local-dev-user";
        var task = await _repo.CreateAsync(customerId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetTasks), new { customerId }, task);
    }

    [HttpPatch("{taskId:long}/status")]
    public async Task<IActionResult> UpdateStatus(
        long taskId,
        [FromBody] UpdatePMTaskStatusRequest request,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
            return BadRequest(new { message = "Status is required." });

        var ok = await _repo.UpdateStatusAsync(customerId, taskId, request, cancellationToken);
        if (!ok) return NotFound(new { message = "Task not found." });

        return NoContent();
    }
}
