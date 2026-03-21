using Contracts.PropertyManagement;
using Infrastructure.PropertyManagement;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArrearsController : ControllerBase
{
    private readonly IArrearsRepository _repo;

    public ArrearsController(IArrearsRepository repo) => _repo = repo;

    [HttpGet("summary")]
    public async Task<ActionResult<ArrearsSummaryDto>> GetSummary(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var result = await _repo.GetSummaryAsync(customerId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("escalations")]
    public async Task<ActionResult<IReadOnlyList<ArrearsEscalationDto>>> GetEscalations(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetActiveEscalationsAsync(customerId, cancellationToken);
        return Ok(results);
    }
}
