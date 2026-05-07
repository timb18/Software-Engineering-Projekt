using System.Text.Json.Serialization;

namespace Services.Users;

public interface IUserManagementService
{
    Task ChangePasswordAsync(ChangePasswordRequest changePasswordRequest,
        CancellationToken cancellationToken = default);
}

public record Auth0Config(
    string Domain,
    string Audience,
    string ClientId,
    string ClientSecret,
    string ConnectionId,
    string ManagementAudience);

public record ChangePasswordRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; init; }
    [JsonPropertyName("password")]
    public required string Password { get; init; }
};