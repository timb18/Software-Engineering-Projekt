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

    [HttpDelete("{organizationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid organizationId,
        [FromBody] DeleteOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await organizationService.DeleteOrganizationAsync(
                new DeleteOrganizationCommand(
                    organizationId,
                    request.InitiatorUserId,
                    request.ConfirmationText),
                cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}

public sealed record DeleteOrganizationRequest(Guid InitiatorUserId, string ConfirmationText);
