namespace Services.Users;

public interface IUserService
{
    Task<(Guid UserId, Guid? WorkProfileId)> EnsureUserAsync(
        string email,
        string? authProviderSubject = null,
        string? displayName = null,
        string? profileImageUrl = null,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default);
}

public sealed record UserProfileDto(
    Guid Id,
    string Username,
    string DisplayName,
    string Email,
    string? ProfileImageUrl,
    string Timezone,
    string? BreakColor = null,
    string? BlockerColor = null,
    string? OrgColors = null);

public sealed record UpdateUserProfileCommand(
    string DisplayName,
    string Email,
    string? ProfileImageUrl,
    string? Timezone,
    string? BreakColor = null,
    string? BlockerColor = null,
    string? OrgColors = null);
