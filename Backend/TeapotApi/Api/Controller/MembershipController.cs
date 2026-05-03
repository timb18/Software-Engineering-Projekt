using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class MembershipController(IMembershipService membershipService) : ControllerBase
{
    [HttpDelete("leave")]
    public async Task<IActionResult> LeaveOrganizationAsync(
        [FromBody] RemoveMembershipRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return BadRequest("UserId must be a valid GUID.");
        }

        if (!Guid.TryParse(request.OrganizationId, out var organizationId))
        {
            return BadRequest("OrganizationId must be a valid GUID.");
        }

        try
        {
            await membershipService.LeaveOrganizationAsync(userId, organizationId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("remove")]
    public async Task<IActionResult> RemoveUserFromOrganizationAsync(
        [FromBody] RemoveMembershipRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return BadRequest("UserId must be a valid GUID.");
        }

        if (!Guid.TryParse(request.OrganizationId, out var organizationId))
        {
            return BadRequest("OrganizationId must be a valid GUID.");
        }

        try
        {
            await membershipService.RemoveUserFromOrganizationAsync(userId, organizationId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}

public class RemoveMembershipRequest
{
    public string UserId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
}
