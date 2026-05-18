using Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        catch (InvalidOperationException)
        {
            return Conflict("The organization could not be created.");
        }
    }

    [HttpGet("by-user-email")]
    public async Task<IActionResult> GetByUserEmail([FromQuery] string email, CancellationToken cancellationToken)
    {
        try
        {
            var organizations = await organizationService.GetOrganizationsForUserAsync(email, cancellationToken);
            return Ok(organizations);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Organization not found.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPatch("{organizationId}")]
    [HttpPost("{organizationId}/rename")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rename(
        string organizationId,
        [FromBody] RenameOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(organizationId, out var parsedOrganizationId))
        {
            return BadRequest("OrganizationId must be a valid GUID. Reload organizations from the backend before editing.");
        }

        try
        {
            await organizationService.RenameOrganizationAsync(
                new RenameOrganizationCommand(
                    parsedOrganizationId,
                    request.InitiatorUserId,
                    request.Name),
                cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You are not allowed to rename this organization.");
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Organization not found.");
        }
        catch (InvalidOperationException)
        {
            return Conflict("The organization could not be renamed.");
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
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "You are not allowed to delete this organization.");
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Organization not found.");
        }
        catch (InvalidOperationException)
        {
            return Conflict("The organization could not be deleted.");
        }
    }
}

public sealed record RenameOrganizationRequest(Guid InitiatorUserId, string Name);
public sealed record DeleteOrganizationRequest(Guid InitiatorUserId, string ConfirmationText);
