using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class MembershipController(IMembershipService membershipService) : ControllerBase
{
    [HttpDelete("leave")]
    public async Task<IActionResult> LeaveOrganizationAsync(
        [FromBody] LeaveOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await membershipService.LeaveOrganizationAsync(request.UserId, request.OrganizationId, cancellationToken);
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

public class LeaveOrganizationRequest
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
}
