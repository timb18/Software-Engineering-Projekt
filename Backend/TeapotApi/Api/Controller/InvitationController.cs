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
                request.CreatedByEmail,
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
            if (request.UserId.HasValue)
            {
                await _invitationService.AcceptInvitationAsync(invitationId, request.UserId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(request.Email))
            {
                await _invitationService.AcceptInvitationByEmailAsync(invitationId, request.Email);
            }
            else
            {
                return BadRequest(new { success = false, message = "UserId oder E-Mail ist erforderlich." });
            }

            return Ok(new { success = true, message = "Einladung akzeptiert" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{invitationId}/accept-link")]
    public IActionResult AcceptInvitationLinkAsync([FromRoute] Guid invitationId, [FromQuery] string email)
    {
        return Redirect(BuildFrontendRedirect("pending", invitationId, email));
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

    [HttpGet("{invitationId}/reject-link")]
    public async Task<IActionResult> RejectInvitationLinkAsync([FromRoute] Guid invitationId)
    {
        try
        {
            await _invitationService.RejectInvitationAsync(invitationId);
            return Redirect(BuildFrontendRedirect("rejected"));
        }
        catch (Exception ex)
        {
            return Redirect(BuildFrontendRedirect("error", ex.Message));
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

    private static string BuildFrontendRedirect(string status, Guid? invitationId = null, string? email = null, string? message = null)
    {
        var baseUrl = Environment.GetEnvironmentVariable("FRONTEND_BASE_URL") ?? "http://127.0.0.1:5173/";
        var separator = baseUrl.Contains('?') ? "&" : "?";
        var url = $"{baseUrl}{separator}inviteStatus={Uri.EscapeDataString(status)}";

        if (invitationId.HasValue)
            url += $"&invitationId={Uri.EscapeDataString(invitationId.Value.ToString())}";

        if (!string.IsNullOrWhiteSpace(email))
            url += $"&email={Uri.EscapeDataString(email)}";

        if (!string.IsNullOrWhiteSpace(message))
            url += $"&message={Uri.EscapeDataString(message)}";

        return url;
    }
}

public class SendInvitationRequest
{
    public string Email { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByEmail { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

public class AcceptInvitationRequest
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
}
