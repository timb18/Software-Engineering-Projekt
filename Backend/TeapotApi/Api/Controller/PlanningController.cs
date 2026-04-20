using Microsoft.AspNetCore.Mvc;
using Model;
using Services;

namespace Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class WorkProfileController(IWorkProfileService workProfileService) : ControllerBase
    {
        /// <summary>Returns the work profile for a user. Returns 204 No Content if none exists yet.</summary>
        [HttpGet("{userId}")]
        [ProducesResponseType(typeof(WorkProfile), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Get(string userId)
        {
            var profile = await workProfileService.GetAsync(userId);
            if (profile is null)
                return NoContent();

            return Ok(profile);
        }

        /// <summary>Creates or replaces the work profile for a user.</summary>
        [HttpPut("{userId}")]
        [ProducesResponseType(typeof(WorkProfile), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Put(string userId, [FromBody] WorkProfile profile)
        {
            if (profile.UserId != userId)
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
}
