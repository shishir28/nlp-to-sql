using Contracts.PropertyManagement;
using Infrastructure.PropertyManagement;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VacancyController : ControllerBase
{
    private readonly IVacancyRepository _repo;

    public VacancyController(IVacancyRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VacancyDto>>> GetVacancies(
        [FromQuery] string customerId = "1",
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetVacanciesAsync(customerId, status, cancellationToken);
        return Ok(results);
    }

    [HttpGet("{vacancyId:long}/listings")]
    public async Task<ActionResult<IReadOnlyList<ListingDto>>> GetListings(
        long vacancyId,
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetListingsAsync(customerId, vacancyId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("listings")]
    public async Task<ActionResult<IReadOnlyList<ListingDto>>> GetAllListings(
        [FromQuery] string customerId = "1",
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetListingsAsync(customerId, null, cancellationToken);
        return Ok(results);
    }

    [HttpGet("applications")]
    public async Task<ActionResult<IReadOnlyList<LettingApplicationDto>>> GetApplications(
        [FromQuery] string customerId = "1",
        [FromQuery] long? vacancyId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var results = await _repo.GetApplicationsAsync(customerId, vacancyId, status, cancellationToken);
        return Ok(results);
    }
}
