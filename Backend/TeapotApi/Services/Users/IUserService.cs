namespace Services.Users;

/// <summary>
/// User profile and account synchronization operations.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Ensures that a user exists for the given email address and returns the user and work profile ids.
    /// </summary>
    Task<(Guid UserId, Guid? WorkProfileId)> EnsureUserAsync(
        string email,
        string? authProviderSubject = null,
        string? displayName = null,
        string? profileImageUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the public profile for the given user id.
    /// </summary>
    Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the editable profile fields for the given user.
    /// </summary>
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Public user profile payload returned to the frontend.
/// </summary>
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

/// <summary>
/// Command object used to update the editable profile fields.
/// </summary>
public sealed record UpdateUserProfileCommand(
    string DisplayName,
    string Email,
    string? ProfileImageUrl,
    string? Timezone,
    string? BreakColor = null,
    string? BlockerColor = null,
    string? OrgColors = null);
