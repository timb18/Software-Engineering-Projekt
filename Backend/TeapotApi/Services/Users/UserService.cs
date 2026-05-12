using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;
using System.ComponentModel.DataAnnotations;
using Auth0.ManagementApi;
using Organization = DataAccess.Models.Organization;

namespace Services.Users;

/// <summary>
/// Provides user management functionality including user creation, profile retrieval, profile updates,
/// and password management for the Auth0 user management system.
/// </summary>
/// <param name="userRepository">Repository interface for user data access operations.</param>
/// <param name="organizationRepository">Repository interface for organization data access operations.</param>
/// <param name="membershipRepository">Repository interface for membership data access operations.</param>
/// <param name="workProfileRepository">Repository interface for work profile data access operations.</param>
/// <param name="unitOfWork">Unit of work for transactional data persistence.</param>
/// <param name="managementClient">Auth0 Management API client for external service integration.</param>
/// <param name="auth0Config">Configuration settings for Auth0 management client.</param>
/// <remarks>
/// This service handles user-centric logic and coordinates between different repository interfaces
/// and the external Auth0 API to ensure data consistency and security.
/// </remarks>
public class UserService(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IMembershipRepository membershipRepository,
    IWorkProfileRepository workProfileRepository,
    IUnitOfWork unitOfWork,
    IManagementApiClient managementClient,
    Auth0Config auth0Config) : IUserService
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    /// <summary>
    /// Ensures that a user account exists for the specified email address.
    /// If no user is found, a new user is created with a personal organization,
    /// membership, and a new work profile with default settings.
    /// </summary>
    /// <param name="email">The unique email address for the user account.</param>
    /// <param name="authProviderSubject">The subject identifier from the authentication provider, if applicable.</param>
    /// <param name="cancellationToken">A token to monitor the cancellation status of the operation.</param>
    /// <returns>A tuple containing the GUID of the user and the GUID of the work profile.</returns>
    /// <exception cref="ArgumentException">Thrown when the email is invalid or the profile image URL is not valid.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled by the token.</exception>
    public async Task<(Guid UserId, Guid WorkProfileId)> EnsureUserAsync(
        string email,
        string? authProviderSubject = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);

        // Wrap in a transaction to prevent race conditions when two requests
        // arrive simultaneously for the same new user
        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var user = await FindOrCreateUserAsync(normalizedEmail, authProviderSubject,
            cancellationToken);

        var existingProfile = await workProfileRepository.FindByUserIdAsync(user.Id, cancellationToken);
        if (existingProfile is not null)
        {
            await tx.CommitAsync(cancellationToken);
            return (user.Id, existingProfile.Id);
        }

        // No work profile yet — create a personal org + membership + work profile
        var personalOrg = new Organization
        {
            Name = $"Personal ({normalizedEmail})",
            Description = "Auto-created personal workspace",
            MaxUsers = 1,
            CreatedAt = DateTime.UtcNow,
        };
        await organizationRepository.AddAsync(personalOrg, cancellationToken);

        var membership = new Membership
        {
            UserId = user.Id,
            OrganizationId = personalOrg.Id,
            Role = ERole.Organizer,
            CreatedAt = DateTime.UtcNow,
        };
        await membershipRepository.AddAsync(membership, cancellationToken);

        var workProfile = new WorkProfile
        {
            MembershipId = membership.Id,
            MaxDailyLoad = TimeSpan.FromHours(8),
            CreatedAt = DateTime.UtcNow,
        };
        await workProfileRepository.AddAsync(workProfile, cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return (user.Id, workProfile.Id);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken)
                   ?? throw new KeyNotFoundException("User not found.");

        return MapProfile(user);
    }

    /// <summary>
    /// Updates the profile information for a specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose profile is being updated.</param>
    /// <param name="command">The update command containing the new profile values for display name, email, profile image URL, and timezone.</param>
    /// <param name="cancellationToken">A token to propagate cancellation requests.</param>
    /// <returns>A task that when completed successfully contains the updated profile data as a UserProfileDto.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the specified user is not found.</exception>
    /// <exception cref="ArgumentException">Thrown when the specified email is already in use by another user.</exception>
    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken)
                   ?? throw new KeyNotFoundException("User not found.");

        var authUser = await GetUserFromAuth0(user.Email, cancellationToken);

        var normalizedTimezone = NormalizeTimezone(command.Timezone);

        var updateUserRequestContent = new UpdateUserRequestContent();

        if (command.Email is not null)
        {
            var normalizedEmail = NormalizeEmail(command.Email);

            if (await userRepository.IsEmailTakenByOtherAsync(userId, normalizedEmail, cancellationToken))
                throw new ArgumentException("Email is already in use.");
            user.Email = normalizedEmail;
            updateUserRequestContent.Email = normalizedEmail;
        }

        if (command.DisplayName is not null)
        {
            var normalizedDisplayName = NormalizeRequired(command.DisplayName, "Display name is required.");
            updateUserRequestContent.Username = normalizedDisplayName;
        }

        if (command.ProfileImageUrl is not null)
        {
            var normalizedProfileImageUrl = NormalizeOptional(command.ProfileImageUrl);
            ValidateProfileImageUrl(normalizedProfileImageUrl);
            updateUserRequestContent.Picture = normalizedProfileImageUrl;
        }

        user.Timezone = normalizedTimezone;
        user.EditedAt = DateTime.UtcNow;

        if ((updateUserRequestContent.Email != null || updateUserRequestContent.Username != null ||
             updateUserRequestContent.Picture != null) && authUser.UserId is not null)
        {
            var result = await managementClient.Users.UpdateAsync(authUser.UserId,
                updateUserRequestContent,
                cancellationToken: cancellationToken);
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        return MapProfile(user);
    }

    /// <summary>
    /// Changes the password in Auth0 of a user
    /// </summary>
    /// <param name="changePasswordRequest">Contains e-mail and new password</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ArgumentNullException">changePasswordRequest is null</exception>
    /// <exception cref="KeyNotFoundException">no user with the given e-mail could be found</exception>
    public async Task ChangePasswordAsync(ChangePasswordRequest changePasswordRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changePasswordRequest);
        ArgumentNullException.ThrowIfNull(changePasswordRequest.Email);
        ArgumentNullException.ThrowIfNull(changePasswordRequest.Password);

        var users = await managementClient.Users.ListAsync(new ListUsersRequestParameters(),
            cancellationToken: cancellationToken);

        var user = users.CurrentPage.Items.FirstOrDefault(u => u.Email == changePasswordRequest.Email);

        if (user?.UserId is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        var response = await managementClient.Users.UpdateAsync(user.UserId,
            new UpdateUserRequestContent
                { Password = changePasswordRequest.Password, Connection = auth0Config.ConnectionId },
            cancellationToken: cancellationToken);
    }

    private async Task<User> FindOrCreateUserAsync(string normalizedEmail, string? authProviderSubject,
        CancellationToken cancellationToken)
    {
        User? user = null;

        if (!string.IsNullOrWhiteSpace(authProviderSubject))
            user = await userRepository.FindByAuthProviderSubjectAsync(authProviderSubject, cancellationToken);

        user ??= await userRepository.FindByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Email = normalizedEmail,
                AuthProviderSubject = NormalizeOptional(authProviderSubject),
                Timezone = "Europe/Berlin",
                CreatedAt = DateTime.UtcNow,
            };
            await userRepository.AddAsync(user, cancellationToken);
        }
        else
            switch (string.IsNullOrWhiteSpace(authProviderSubject))
            {
                case false when
                    string.IsNullOrWhiteSpace(user.AuthProviderSubject):
                    user.AuthProviderSubject = authProviderSubject;
                    user.EditedAt = DateTime.UtcNow;
                    await userRepository.UpdateAsync(user, cancellationToken);
                    break;
                case false when
                    string.Equals(user.AuthProviderSubject, authProviderSubject, StringComparison.Ordinal) &&
                    !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase):
                {
                    if (await userRepository.IsEmailTakenByOtherAsync(user.Id, normalizedEmail, cancellationToken))
                        throw new ArgumentException("Email is already in use.");

                    user.Email = normalizedEmail;
                    user.EditedAt = DateTime.UtcNow;
                    await userRepository.UpdateAsync(user, cancellationToken);
                    break;
                }
            }

        return user;
    }

    private static UserProfileDto MapProfile(User user) => new(
        user.Id,
        user.Email,
        user.Timezone ?? "Europe/Berlin");

    private static string NormalizeRequired(string? value, string errorMessage) =>
        NormalizeOptional(value) ?? throw new ArgumentException(errorMessage);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeEmail(string email)
    {
        var normalized = NormalizeRequired(email, "Email is required.").ToLowerInvariant();
        return !EmailValidator.IsValid(normalized)
            ? throw new ArgumentException("Email format is invalid.")
            : normalized;
    }

    private static string NormalizeTimezone(string? timezone)
    {
        var normalized = NormalizeOptional(timezone) ?? "Europe/Berlin";

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalized);
            return normalized;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException("Timezone is invalid.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ArgumentException("Timezone is invalid.");
        }
    }

    private static void ValidateProfileImageUrl(string? profileImageUrl)
    {
        if (string.IsNullOrWhiteSpace(profileImageUrl))
        {
            return;
        }

        if (!Uri.TryCreate(profileImageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Profile image URL is invalid.");
        }
    }

    /// <summary>
    /// Checks whether an Auth0 user account with the specified email address already exists.
    /// </summary>
    /// <param name="email">The email address to check for existing user accounts.</param>
    /// <param name="cancellationToken">A token to monitor the cancellation status of the operation.</param>
    /// <returns>A boolean value indicating whether a user account exists for the specified email address.</returns>
    /// <exception cref="ArgumentException">Thrown when the email is null or empty.</exception>
    private async Task<bool> IsAuth0EmailTaken(string email, CancellationToken cancellationToken = default)
    {
        var usersEnumerable = await
            managementClient.Users.ListUsersByEmailAsync(new ListUsersByEmailRequestParameters { Email = email },
                cancellationToken: cancellationToken);

        var users = usersEnumerable.ToArray();

        var isTaken = users.Length > 0;
        return isTaken;
    }

    /// <summary>
    /// Retrieves the user account details from Auth0 for the specified email address.
    /// </summary>
    /// <param name="email">The email address to search for in the user database.</param>
    /// <param name="cancellationToken">A token to monitor the cancellation status of the operation.</param>
    /// <returns>The user record corresponding to the provided email address.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if no user account is found for the provided email address.</exception>
    private async Task<UserResponseSchema> GetUserFromAuth0(string email, CancellationToken cancellationToken = default)
    {
        var usersEnumerable = await managementClient.Users.ListUsersByEmailAsync(
            new ListUsersByEmailRequestParameters { Email = email }, cancellationToken: cancellationToken);

        var users = usersEnumerable.ToArray();

        return users.FirstOrDefault() ?? throw new KeyNotFoundException("User not found.");
    }
}