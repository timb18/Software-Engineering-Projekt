using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class OrganizationController : ControllerBase
{
    private readonly OrganizationQueryService _organizationQueryService;

    public OrganizationController(OrganizationQueryService organizationQueryService)
    {
        _organizationQueryService = organizationQueryService;
    }

    [HttpGet("by-user-email")]
    public async Task<IActionResult> GetByUserEmail([FromQuery] string email)
    {
        var organizations = await _organizationQueryService.GetOrganizationsForUserAsync(email);
        return Ok(organizations);
    }
}