using Contracts.Dashboard;
using Infrastructure.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardWidgetsController : ControllerBase
{
    private readonly IDashboardWidgetRepository _repo;

    public DashboardWidgetsController(IDashboardWidgetRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DashboardWidgetDto>>> GetAll(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var widgets = await _repo.GetByCustomerAsync(customerId, cancellationToken);
        return Ok(widgets);
    }

    [HttpPost]
    public async Task<ActionResult<DashboardWidgetDto>> Save(
        [FromBody] SaveDashboardWidgetRequest request,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Question is required." });

        var userId = User.FindFirst("sub")?.Value ?? "local-dev-user";
        var widget = await _repo.SaveAsync(customerId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { customerId }, widget);
    }

    [HttpPatch("{id:int}/view")]
    public async Task<IActionResult> UpdateView(
        int id,
        [FromBody] UpdateWidgetViewRequest request,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        await _repo.UpdateViewTypeAsync(id, customerId, request.ViewType, cancellationToken);
        return NoContent();
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

    [HttpDelete]
    public async Task<IActionResult> DeleteAll(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        await _repo.DeleteAllAsync(customerId, cancellationToken);
        return NoContent();
    }
}
