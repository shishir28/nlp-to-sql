using Contracts.Dashboard;
using Infrastructure.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsRepository _repo;

    public AnalyticsController(IAnalyticsRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AnalyticsSummary>> GetSummary(
        [FromQuery] string customerId = "1",
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var summary = await _repo.GetSummaryAsync(customerId, days, cancellationToken);
        return Ok(summary);
    }
}
