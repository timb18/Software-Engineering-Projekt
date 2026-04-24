using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class OrganizationController(IOrganizationAdminService organizationAdminService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(CreateOrganizationResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            CreateOrganizationResult result = await organizationAdminService.CreateOrganizationAsync(
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
}