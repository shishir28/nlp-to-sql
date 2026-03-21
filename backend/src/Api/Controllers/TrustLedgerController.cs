using Contracts.PropertyManagement;
using Infrastructure.PropertyManagement;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/trust-ledger")]
public class TrustLedgerController : ControllerBase
{
    private readonly ITrustLedgerRepository _repo;

    public TrustLedgerController(ITrustLedgerRepository repo) => _repo = repo;

    [HttpGet("owners")]
    public async Task<ActionResult<IReadOnlyList<TrustLedgerSummaryDto>>> GetOwnerSummaries(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetOwnerSummariesAsync(customerId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("owners/{ownerId:long}/entries")]
    public async Task<ActionResult<IReadOnlyList<TrustLedgerEntryDto>>> GetEntries(
        long ownerId,
        [FromQuery] string customerId = "1",
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetEntriesForOwnerAsync(customerId, ownerId, limit, cancellationToken);
        return Ok(results);
    }

    [HttpGet("fee-schedules")]
    public async Task<ActionResult<IReadOnlyList<ManagementFeeScheduleDto>>> GetFeeSchedules(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetFeeSchedulesAsync(customerId, cancellationToken);
        return Ok(results);
    }
}
