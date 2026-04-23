using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Services;

public class Auth0InvitationService
{
    private readonly HttpClient _httpClient;
    private readonly Auth0TokenService _tokenService;
    private readonly Auth0Options _options;

    public Auth0InvitationService(
        HttpClient httpClient,
        Auth0TokenService tokenService,
        IOptions<Auth0Options> options)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public async Task<Auth0InvitationResult> InviteUserAsync(string email, string organizationId)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Email and organization ID are required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Domain) ||
            string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ConnectionId))
        {
            throw new InvalidOperationException("Auth0 invitation configuration is incomplete. Fill the Auth0 section in configuration.");
        }

        var token = await _tokenService.GetTokenAsync();

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var requestBody = new
        {
            inviter = new
            {
                name = "Teapot Admin"
            },
            invitee = new
            {
                email = email
            },
            client_id = _options.ClientId,
            connection_id = _options.ConnectionId,
            ttl_sec = 86400,
            send_invitation_email = false
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://{_options.Domain}/api/v2/organizations/{organizationId}/invitations",
            requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Invite failed: {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return new Auth0InvitationResult(null, null);
        }

        var invitationId = payload.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
        var invitationUrl = payload.TryGetProperty("invitation_url", out var urlValue) ? urlValue.GetString() : null;

        return new Auth0InvitationResult(invitationId, invitationUrl);
    }
}

public record Auth0InvitationResult(string? InvitationId, string? InvitationUrl);
