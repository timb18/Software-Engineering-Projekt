using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.Extensions.Configuration;

namespace Services;

public class Auth0InvitationService
{
    private readonly HttpClient _httpClient;
    private readonly Auth0TokenService _tokenService;

    public Auth0InvitationService(HttpClient httpClient, Auth0TokenService tokenService)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
    }

    public async Task InviteUserAsync(string email, string organizationId, Guid createdBy)
    {
        var token = await _tokenService.GetTokenAsync();

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var requestBody = new
        {
            inviter = new
            {
                name = ""
            },
            invitee = new
            {
                email = email
            },
            client_id = "YOUR_CLIENT_ID", //Client-ID of the Auth0 application
            connection_id = "YOUR_CONNECTION_ID", //Connection-ID of the database connection (e.g., "Username-Password-Authentication")
            ttl_sec = 86400
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://dev-v87zvco20c7uficw.eu.auth0.com/api/v2/organizations/{organizationId}/invitations",
            requestBody);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Invite failed: {error}");
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.Parse(organizationId),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            Status = EInvitationStatus.Open,
            Email = email
        };

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();
    }
}