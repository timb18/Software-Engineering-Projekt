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
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await workProfileService.GetAsync(userId, cancellationToken);
        if (profile is null)
            return NoContent();

        return Ok(profile);
    }

    /// <summary>Creates or replaces the work profile for a user.</summary>
    [HttpPut("")]
    [ProducesResponseType(typeof(WorkProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put(Guid userId, [FromBody] WorkProfile profile,
        CancellationToken cancellationToken)
    {
        try
        {
            var saved = await workProfileService.SaveAsync(userId, profile, cancellationToken);
            return Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Deletes the work profile and dependent planning data for a user.</summary>
    [HttpDelete("")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await workProfileService.DeleteAsync(userId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
