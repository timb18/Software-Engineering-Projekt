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
        var result = await _invitationService.InviteUserAsync(
            request.Email,
            request.OrganizationId);

        return Ok(new InviteResponse
        {
            InvitationId = result.InvitationId,
            InvitationUrl = result.InvitationUrl
        });
    }
}

public class InviteRequest
{
    public string OrganizationId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class InviteResponse
{
    public string? InvitationId { get; set; }
    public string? InvitationUrl { get; set; }
}
