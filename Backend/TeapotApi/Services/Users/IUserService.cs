using System.Text.Json.Serialization;

namespace Services.Users;

public interface IUserService
{
    Task<(Guid UserId, Guid WorkProfileId)> EnsureUserAsync(
        string email,
        string? authProviderSubject = null,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default);
    
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
    [JsonPropertyName("email")] public required string Email { get; init; }
    [JsonPropertyName("password")] public required string Password { get; init; }
};

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string Timezone);

public sealed record UpdateUserProfileCommand(
    string? DisplayName,
    string? Email,
    string? ProfileImageUrl,
    string? Timezone);
