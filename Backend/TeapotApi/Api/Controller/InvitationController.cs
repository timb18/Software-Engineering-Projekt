using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controller;

[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = "Auth0")]
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
    /// Sends a new invitation email for a user and returns the created invitation data.
    /// The response includes the invitation state so the client can update the UI immediately.
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
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Invitation not found." });
        }
        catch (Exception)
        {
            return BadRequest(new { success = false, message = "Unable to send the invitation." });
        }
    }

    /// <summary>
    /// Accepts an invitation either by authenticated user ID or by email address.
    /// The endpoint supports both flows so the frontend can reuse the same action after login
    /// and from the invitation link workflow.
    /// </summary>
    [HttpPost("{invitationId:guid}/accept")]
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
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Invitation not found." });
        }
        catch (Exception)
        {
            return BadRequest(new { success = false, message = "Unable to accept the invitation." });
        }
    }

    [HttpGet("{invitationId:guid}/accept-link")]
    public IActionResult AcceptInvitationLink([FromRoute] Guid invitationId, [FromQuery] string email)
    {
        return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "pending", invitationId, email));
    }

    /// <summary>
    /// Rejects an invitation and returns a simple success or error payload for the client.
    /// This keeps the rejection flow lightweight for both API consumers and email link usage.
    /// </summary>
    [HttpPost("{invitationId:guid}/reject")]
    public async Task<IActionResult> RejectInvitationAsync([FromRoute] Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            await _invitationService.RejectInvitationAsync(invitationId, cancellationToken);
            return Ok(new { success = true, message = "Invite rejected" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Invitation not found." });
        }
        catch (Exception)
        {
            return BadRequest(new { success = false, message = "Unable to reject the invitation." });
        }
    }

    [HttpGet("{invitationId:guid}/reject-link")]
    public async Task<IActionResult> RejectInvitationLink([FromRoute] Guid invitationId, CancellationToken cancellationToken)
    {
        try
        {
            await _invitationService.RejectInvitationAsync(invitationId, cancellationToken);
            return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "rejected"));
        }
        catch (Exception)
        {
            return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "error", message: "Unable to process the invitation."));
        }
    }

    /// <summary>
    /// Retrieves all open invitations for a specific email address.
    /// The result is filtered server-side so the frontend does not need to duplicate invitation state logic.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingInvitationsAsync([FromQuery] string email, CancellationToken cancellationToken)
    {
        try
        {
            var invitations = await _invitationService.GetPendingInvitationsForEmailAsync(email, cancellationToken);
            return Ok(new { success = true, data = invitations });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Invitation not found." });
        }
        catch (Exception)
        {
            return BadRequest(new { success = false, message = "Unable to load pending invitations." });
        }
    }

    /// <summary>
    /// Retrieves all invitations that belong to a specific organization.
    /// This endpoint is used to render the organization's invitation overview in the UI.
    /// </summary>
    [HttpGet("organization/{organizationId:guid}")]
    public async Task<IActionResult> GetOrganizationInvitationsAsync([FromRoute] Guid organizationId, CancellationToken cancellationToken)
    {
        try
        {
            var invitations = await _invitationService.GetInvitationsForOrganizationAsync(organizationId, cancellationToken);
            return Ok(new { success = true, data = invitations });
        }
        catch (Exception)
        {
            return BadRequest(new { success = false, message = "Unable to load organization invitations." });
        }
    }

    /// <summary>
    /// Builds a frontend redirect URL with the invitation status and optional context parameters.
    /// </summary>
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

    /// <summary>
    /// Resolves the public API base URL that is embedded into invitation links.
    /// When the configured value is local or empty, the current request host is used instead.
    /// </summary>
    private string ResolvePublicApiBaseUrl()
    {
        var configuredBaseUrl = TrimTrailingSlash(_emailOptions.ApiBaseUrl);
        var requestBaseUrl = BuildRequestBaseUrl();

        return !string.IsNullOrWhiteSpace(requestBaseUrl) &&
               (string.IsNullOrWhiteSpace(configuredBaseUrl) || IsLocalBaseUrl(configuredBaseUrl))
            ? requestBaseUrl
            : configuredBaseUrl;
    }

    /// <summary>
    /// Builds the public base URL from forwarded request headers or the current request host.
    /// </summary>
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

    /// <summary>
    /// Returns true when the supplied base URL points to a local development host.
    /// </summary>
    private static bool IsLocalBaseUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Removes any trailing slash so URL concatenation stays consistent.
    /// </summary>
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
