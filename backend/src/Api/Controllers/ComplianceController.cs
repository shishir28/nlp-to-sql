using Contracts.PropertyManagement;
using Infrastructure.PropertyManagement;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceRepository _repo;

    public ComplianceController(IComplianceRepository repo) => _repo = repo;

    [HttpGet("summary")]
    public async Task<ActionResult<ComplianceSummaryDto>> GetSummary(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var result = await _repo.GetSummaryAsync(customerId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<IReadOnlyList<ComplianceItemDto>>> GetOverdue(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetOverdueAsync(customerId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("due-soon")]
    public async Task<ActionResult<IReadOnlyList<ComplianceItemDto>>> GetDueSoon(
        [FromQuery] string customerId = "1",
        [FromQuery] int daysAhead = 30,
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetDueSoonAsync(customerId, daysAhead, cancellationToken);
        return Ok(results);
    }

    [HttpGet("property/{propertyId:long}")]
    public async Task<ActionResult<IReadOnlyList<ComplianceItemDto>>> GetByProperty(
        long propertyId,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetByPropertyAsync(customerId, propertyId, cancellationToken);
        return Ok(results);
    }
}
