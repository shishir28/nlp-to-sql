using Contracts.PropertyManagement;
using Infrastructure.PropertyManagement;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/lease-renewal")]
public class LeaseRenewalController : ControllerBase
{
    private readonly ILeaseRenewalRepository _repo;

    public LeaseRenewalController(ILeaseRenewalRepository repo) => _repo = repo;

    [HttpGet("summary")]
    public async Task<ActionResult<LeaseRenewalSummaryDto>> GetSummary(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var result = await _repo.GetSummaryAsync(customerId, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeaseRenewalOutcomeDto>>> GetRenewals(
        [FromQuery] string customerId = "1",
        [FromQuery] string? outcomeCode = null,
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetRenewalsAsync(customerId, outcomeCode, cancellationToken);
        return Ok(results);
    }
}
