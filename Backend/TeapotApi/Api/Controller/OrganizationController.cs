using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class OrganizationController(IOrganizationService organizationService) : ControllerBase
{
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
