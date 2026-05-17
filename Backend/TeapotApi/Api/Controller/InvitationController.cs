using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
    /// Sends an invitation to the specified email address for the given organization.
    /// </summary>
    /// <param name="request">
    /// A request object containing the invitation details, including the recipient email, organization ID, expiry duration, and optional sender information.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor the cancellation status for the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task containing the result of the invitation sending operation.
    /// </returns>
    [HttpPost("send")]
    public async Task<IActionResult> SendInvitationAsync([FromBody] SendInvitationRequest request,
        CancellationToken cancellationToken = default)
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

            return Ok(result);
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
    /// Accepts the specified invitation using the provided user identifier or email address.
    /// </summary>
    /// <param name="invitationId">
    /// The unique identifier of the invitation to be accepted.
    /// </param>
    /// <param name="request">
    /// A request object containing either the user ID or email address required to complete the invitation acceptance.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor the cancellation status for the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task containing the HTTP result indicating the success or failure of the invitation acceptance operation.
    /// </returns>
    [HttpPost("{invitationId:guid}/accept")]
    public async Task<Results<Ok<string>, BadRequest<string>, NotFound<string>>> AcceptInvitationAsync(
        [FromRoute] Guid invitationId,
        [FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken = default)
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
                return TypedResults.BadRequest("UserId or Email is required.");
            }

            return TypedResults.Ok("Invite accepted");
        }
        catch (KeyNotFoundException ex)
        {
            return TypedResults.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Handles the acceptance of an invitation by redirecting the client to the frontend.
    /// </summary>
    /// <param name="invitationId">
    /// The unique identifier for the invitation to be accepted.
    /// </param>
    /// <param name="email">
    /// The email address of the recipient associated with the invitation.
    /// </param>
    /// <returns>
    /// An action result containing a redirect status code to the frontend application.
    /// </returns>
    [HttpGet("{invitationId:guid}/accept-link")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult AcceptInvitationLink([FromRoute] Guid invitationId, [FromQuery] string email)
    {
        return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "pending", invitationId, email));
    }


    /// <summary>
    /// Rejects the specified invitation for the given organization.
    /// </summary>
    /// <param name="invitationId">
    /// The unique identifier of the invitation to reject.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor the cancellation status for the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task containing a typed result that indicates the outcome of the invitation rejection operation.
    /// Returns Ok with a message on success, NotFound if the invitation does not exist, and BadRequest on other errors.
    /// </returns>
    [HttpPost("{invitationId:guid}/reject")]
    public async Task<Results<Ok<string>, BadRequest<string>, NotFound<string>>> RejectInvitationAsync(
        [FromRoute] Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _invitationService.RejectInvitationAsync(invitationId, cancellationToken);
            return TypedResults.Ok("Invite rejected");
        }
        catch (KeyNotFoundException ex)
        {
            return TypedResults.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Rejects the specified invitation and redirects the user to a rejection confirmation page.
    /// </summary>
    /// <param name="invitationId">
    /// The unique identifier of the invitation to reject.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor the cancellation status for the asynchronous operation.
    /// </param>
    /// <returns>
    /// An IActionResult representing a redirect to the frontend rejection page.
    /// </returns>
    [HttpGet("{invitationId:guid}/reject-link")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> RejectInvitationLink([FromRoute] Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _invitationService.RejectInvitationAsync(invitationId, cancellationToken);
            return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "rejected"));
        }
        catch (Exception ex)
        {
            return Redirect(BuildFrontendRedirect(_emailOptions.FrontendBaseUrl, "error", message: ex.Message));
        }
    }

    /// <summary>
    /// Retrieves a list of pending invitation records for the specified email address.
    /// </summary>
    /// <param name="email">
    /// The email address to search for pending invitations in the system.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor the cancellation status for the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task containing the result of the operation. On success, the result contains a collection of invitation details associated with the specified email. On error, a specific status code is returned with an error message.
    /// </returns>
    [HttpGet("pending")]
    public async Task<Results<Ok<IEnumerable<InvitationDto>>, BadRequest<string>, NotFound<string>>>
        GetPendingInvitationsAsync([FromQuery] string email,
            CancellationToken cancellationToken = default)
    {
        try
        {
            var invitations = await _invitationService.GetPendingInvitationsForEmailAsync(email, cancellationToken);
            return TypedResults.Ok(invitations);
        }
        catch (KeyNotFoundException ex)
        {
            return TypedResults.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves a collection of pending invitations for the specified organization.
    /// </summary>
    /// <param name="organizationId">
    /// The unique identifier of the organization for which to retrieve invitations.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to monitor the cancellation status for the asynchronous operation.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The value contains a result with the list of invitations if successful, or a bad request error if an error occurs.
    /// </returns>
    [HttpGet("organization/{organizationId:guid}")]
    public async Task<Results<Ok<IEnumerable<InvitationDto>>, BadRequest<string>>> GetOrganizationInvitationsAsync(
        [FromRoute] Guid organizationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var invitations =
                await _invitationService.GetInvitationsForOrganizationAsync(organizationId, cancellationToken);
            return TypedResults.Ok(invitations);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Constructs a frontend redirect URL based on the specified configuration and status.
    /// </summary>
    /// <param name="configuredBaseUrl">
    /// The base URL for the frontend application. Defaults to a local development address if empty or whitespace.
    /// </param>
    /// <param name="status">
    /// The status to represent in the URL query string (e.g., "pending", "rejected", "error").
    /// </param>
    /// <param name="invitationId">
    /// An optional unique identifier for the invitation included in the query string.
    /// </param>
    /// <param name="email">
    /// An optional email address associated with the invitation or error to include in the query string.
    /// </param>
    /// <param name="message">
    /// An optional error message string to include in the query string if an exception occurred.
    /// </param>
    /// <returns>
    /// A complete URL string formatted as the base URL with appropriate query parameters appended.
    /// </returns>
    private static string BuildFrontendRedirect(string configuredBaseUrl, string status, Guid? invitationId = null,
        string? email = null, string? message = null)
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