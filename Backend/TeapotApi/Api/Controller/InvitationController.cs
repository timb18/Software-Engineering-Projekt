using Microsoft.AspNetCore.Mvc;
using Services;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class InvitationController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    
    public InvitationController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <summary>
    /// Sendet eine Einladung an einen Benutzer
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendInvitationAsync([FromBody] SendInvitationRequest request)
    {
        try
        {
            var result = await _invitationService.SendInvitationAsync(
                request.Email,
                request.OrganizationId,
                request.CreatedByUserId,
                request.FirstName,
                request.LastName);

            return Ok(new { success = true, message = "Einladung versendet", data = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Akzeptiert eine Einladung
    /// </summary>
    [HttpPost("{invitationId}/accept")]
    public async Task<IActionResult> AcceptInvitationAsync([FromRoute] Guid invitationId, [FromBody] AcceptInvitationRequest request)
    {
        try
        {
            var result = await _invitationService.AcceptInvitationAsync(invitationId, request.UserId);
            return Ok(new { success = true, message = "Einladung akzeptiert" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Lehnt eine Einladung ab
    /// </summary>
    [HttpPost("{invitationId}/reject")]
    public async Task<IActionResult> RejectInvitationAsync([FromRoute] Guid invitationId)
    {
        try
        {
            await _invitationService.RejectInvitationAsync(invitationId);
            return Ok(new { success = true, message = "Einladung abgelehnt" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Ruft offene Einladungen für eine E-Mail-Adresse ab
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingInvitationsAsync([FromQuery] string email)
    {
        try
        {
            var invitations = await _invitationService.GetPendingInvitationsForEmailAsync(email);
            return Ok(new { success = true, data = invitations });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Ruft alle Einladungen für eine Organisation ab
    /// </summary>
    [HttpGet("organization/{organizationId}")]
    public async Task<IActionResult> GetOrganizationInvitationsAsync([FromRoute] Guid organizationId)
    {
        try
        {
            var invitations = await _invitationService.GetInvitationsForOrganizationAsync(organizationId);
            return Ok(new { success = true, data = invitations });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public class SendInvitationRequest
{
    public string Email { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class AcceptInvitationRequest
{
    public Guid UserId { get; set; }
}
