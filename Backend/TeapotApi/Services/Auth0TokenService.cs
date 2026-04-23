using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Services;

public class Auth0TokenService
{
    private readonly HttpClient _httpClient;
    private readonly Auth0Options _options;
    
    public Auth0TokenService(HttpClient httpClient, IOptions<Auth0Options> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }
    
    public async Task<string> GetTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.Domain) ||
            string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret) ||
            string.IsNullOrWhiteSpace(_options.ManagementAudience))
        {
            throw new InvalidOperationException("Auth0 configuration is incomplete. Fill the Auth0 section in configuration.");
        }

        var request = new
        {
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            audience = _options.ManagementAudience,
            grant_type = "client_credentials",
        };
        
        var response = await _httpClient.PostAsJsonAsync($"https://{_options.Domain}/oauth/token", request);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("Auth0 token response did not contain an access token.");
    }
}
