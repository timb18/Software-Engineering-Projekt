using DataAccess.Models;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class PlanningController : ControllerBase
{
}

[Route("api/[controller]/{userId:guid}")]
[ApiController]
public class WorkProfileController(IWorkProfileService workProfileService) : ControllerBase
{
    /// <summary>Returns the work profile for a user. Returns 204 No Content if none exists yet.</summary>
    [HttpGet("")]
    [ProducesResponseType(typeof(WorkProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Get(Guid userId)
    {
        var profile = await workProfileService.GetAsync(userId);
        if (profile is null)
            return NoContent();

        return Ok(profile);
    }

    /// <summary>Creates or replaces the work profile for a user.</summary>
    [HttpPut("")]
    [ProducesResponseType(typeof(WorkProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put(Guid userId, [FromBody] WorkProfile profile)
    {
        if (profile.Membership.UserId != userId)
            return BadRequest("UserId in the body must match the route parameter.");

        try
        {
            var saved = await workProfileService.SaveAsync(profile);
            return Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}