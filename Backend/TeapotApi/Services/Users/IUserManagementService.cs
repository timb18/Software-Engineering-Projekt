using System.Text.Json.Serialization;

namespace Services.Users;

/// <summary>
/// Auth0-backed account management operations.
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Changes the password for a user account in Auth0.
    /// </summary>
    Task ChangePasswordAsync(ChangePasswordRequest changePasswordRequest,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Auth0 management configuration used by the user management service.
/// </summary>
public record Auth0Config(
    string Domain,
    string Audience,
    string ClientId,
    string ClientSecret,
    string ConnectionId,
    string ManagementAudience);

/// <summary>
/// Request payload used by the password change endpoint.
/// </summary>
public record ChangePasswordRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; init; }
    [JsonPropertyName("password")]
    public required string Password { get; init; }
};