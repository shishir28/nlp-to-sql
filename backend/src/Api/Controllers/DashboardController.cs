using Contracts.Dashboard;
using Infrastructure.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ISavedQueryRepository _repo;

    public DashboardController(ISavedQueryRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("saved")]
    public async Task<ActionResult<IReadOnlyList<SavedQueryDto>>> GetSaved(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetByCustomerAsync(customerId, cancellationToken);
        return Ok(results);
    }

    [HttpPost("saved")]
    public async Task<ActionResult<SavedQueryDto>> SaveQuery(
        [FromBody] SaveQueryRequest request,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Name and Question are required." });

        var userId = User.FindFirst("sub")?.Value ?? "local-dev-user";
        var saved = await _repo.SaveAsync(customerId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetSaved), new { customerId }, saved);
    }

    [HttpDelete("saved/{id:int}")]
    public async Task<IActionResult> DeleteSaved(
        int id,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        await _repo.DeleteAsync(id, customerId, cancellationToken);
        return NoContent();
    }
}
