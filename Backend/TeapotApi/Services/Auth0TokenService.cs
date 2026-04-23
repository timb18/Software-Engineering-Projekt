using System.Net.Http.Json;
using System.Text.Json;

namespace Services;

public class Auth0TokenService
{
    private readonly HttpClient _httpClient;
    
    public Auth0TokenService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<string?> GetTokenAsync()
    {
        var request = new
        {
            client_id = "YOUR_CLIENT_ID",
            client_secret = "YOUR_CLIENT_SECRET",
            audience = "https://YOUR_DOMAIN/api/v2/",
            grant_type = "client_credentials",
        };
        
        var response = await _httpClient.PostAsJsonAsync("https://YOUR_DOMAIN/oauth/token", request);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString();
    }
}