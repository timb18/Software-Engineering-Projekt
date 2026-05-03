using Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class OrganizationController(
    IOrganizationAdminService organizationAdminService,
    IOrganizationService organizationService) : ControllerBase
{
    [HttpPost]
    [Authorize(AuthenticationSchemes = "Auth0", Policy = AdminAuthRequirement.PolicyName)]
    [ProducesResponseType(typeof(CreateOrganizationResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await organizationAdminService.CreateOrganizationAsync(
                request,
                cancellationToken);

            return Created($"/api/organizations/{result.OrganizationId}", result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("by-user-email")]
    public async Task<IActionResult> GetByUserEmail([FromQuery] string email)
    {
        try
        {
            var organizations = await organizationService.GetOrganizationsForUserAsync(email);
            return Ok(organizations);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}