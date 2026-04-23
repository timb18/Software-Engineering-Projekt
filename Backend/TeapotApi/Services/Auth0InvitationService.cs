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

    public async Task InviteUserAsync(string email, string organizationId)
    {
        var token = await _tokenService.GetTokenAsync();
        
        _httpClient.DefaultRequestHeaders.Authorization=
            new AuthenticationHeaderValue("Bearer", token);

        var reuestBody = new
        {
            inviter = new
            {
                name = ""
            },
            invitee = new
            {
                email = email
            },
            client_id = "YOUR_CLIENT_ID",
            connection_id = "YOUR_CONNECTION_ID",
            ttl_sec = 86400
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            $"https://YOUR_DOMAIN/api/v2/organizations/{organizationId}/invitations", reuestBody);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Invite failed: {error}");
        }
    }
}