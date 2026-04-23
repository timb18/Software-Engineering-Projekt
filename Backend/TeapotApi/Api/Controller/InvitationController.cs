using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class InvitationController : ControllerBase
{
    private readonly Auth0InvitationService _invitationService;
    
    public InvitationController(Auth0InvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    [HttpPost]
    public async Task<IActionResult> InviteAsync([FromBody] InviteRequest request)
    {
        await _invitationService.InviteUserAsync(
            request.Email,
            request.OrganizationId);
        return Ok();
    }
}

public class InviteRequest
{
    public string OrganizationId { get; set; }
    public string Email { get; set; }
}