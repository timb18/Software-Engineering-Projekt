using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Services.Organizations;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
public class InvitationController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly EmailOptions _emailOptions;

    public InvitationController(IInvitationService invitationService, IOptions<EmailOptions> emailOptions)
    {
        _invitationService = invitationService;
        _emailOptions = emailOptions.Value;
    }

    /// <summary>
    /// Sendet eine Einladung an einen Benutzer
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendInvitationAsync([FromBody] SendInvitationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _invitationService.SendInvitationAsync(
                request.Email,
                request.OrganizationId,
                request.ExpiryDays,
                request.CreatedByUserId,
                request.CreatedByEmail,
                request.FirstName,
                request.LastName,
                ResolvePublicApiBaseUrl(),
                cancellationToken);

            return Ok(new { success = true, message = "Invite sent:", data = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
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
    public async Task<IActionResult> AcceptInvitationAsync([FromRoute] Guid invitationId, [FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.UserId.HasValue)
            {
                await _invitationService.AcceptInvitationAsync(invitationId, request.UserId.Value, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(request.Email))
            {
                await _invitationService.AcceptInvitationByEmailAsync(invitationId, request.Email, cancellationToken);
            }
            else
            {
                return BadRequest(new { success = false, message = "UserId or Email is required." });
            }

            return Ok(new { success = true, message = "Invite accepted" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{invitationId}/accept-link")]
    public IActionResult AcceptInvitationLink([FromRoute] Guid invitationId, [FromQuery] string email)
    {
        return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "pending", invitationId, email));
    }

    /// <summary>
    /// Lehnt eine Einladung ab
    /// </summary>
    [HttpPost("{invitationId}/reject")]
    public async Task<IActionResult> RejectInvitationAsync([FromRoute] Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            await _invitationService.RejectInvitationAsync(invitationId, cancellationToken);
            return Ok(new { success = true, message = "Invite rejected" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{invitationId}/reject-link")]
    public async Task<IActionResult> RejectInvitationLink([FromRoute] Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            await _invitationService.RejectInvitationAsync(invitationId, cancellationToken);
            return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "rejected"));
        }
        catch (Exception ex)
        {
            return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "error", message : ex.Message));
        }
    }

    /// <summary>
    /// Ruft offene Einladungen für eine E-Mail-Adresse ab
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingInvitationsAsync([FromQuery] string email, CancellationToken cancellationToken)
    {
        try
        {
            var invitations = await _invitationService.GetPendingInvitationsForEmailAsync(email, cancellationToken);
            return Ok(new { success = true, data = invitations });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
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
    public async Task<IActionResult> GetOrganizationInvitationsAsync([FromRoute] Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            var invitations = await _invitationService.GetInvitationsForOrganizationAsync(organizationId, cancellationToken);
            return Ok(new { success = true, data = invitations });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private static string BuildFrontendRedirect(string configuredBaseUrl, string status, Guid? invitationId = null, string? email = null, string? message = null)
    {
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? "http://127.0.0.1:5173/"
            : configuredBaseUrl;
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

    private string ResolvePublicApiBaseUrl()
    {
        var configuredBaseUrl = TrimTrailingSlash(_emailOptions.ApiBaseUrl);
        var requestBaseUrl = BuildRequestBaseUrl();

        return !string.IsNullOrWhiteSpace(requestBaseUrl) &&
               (string.IsNullOrWhiteSpace(configuredBaseUrl) || IsLocalBaseUrl(configuredBaseUrl))
            ? requestBaseUrl
            : configuredBaseUrl;
    }

    private string BuildRequestBaseUrl()
    {
        var forwardedProto = Request.Headers["X-Forwarded-Proto"]
            .FirstOrDefault()?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        var forwardedHost = Request.Headers["X-Forwarded-Host"]
            .FirstOrDefault()?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? Request.Scheme : forwardedProto;
        var host = string.IsNullOrWhiteSpace(forwardedHost) ? Request.Host.Value : forwardedHost;

        return string.IsNullOrWhiteSpace(host)
            ? string.Empty
            : TrimTrailingSlash($"{scheme}://{host}{Request.PathBase}");
    }

    private static bool IsLocalBaseUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase));

    private static string TrimTrailingSlash(string url) => url.TrimEnd('/');
}

public class SendInvitationRequest
{
    public string Email { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByEmail { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int ExpiryDays { get; set; } = 30;
}

public class AcceptInvitationRequest
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
}
