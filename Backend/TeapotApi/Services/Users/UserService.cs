using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;
using System.ComponentModel.DataAnnotations;
using Auth0.ManagementApi;

namespace Services.Users;

public class UserService(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IMembershipRepository membershipRepository,
    IWorkProfileRepository workProfileRepository,
    IUnitOfWork unitOfWork) : IUserService
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<(Guid UserId, Guid WorkProfileId)> EnsureUserAsync(
        string email,
        string? authProviderSubject = null,
        string? displayName = null,
        string? profileImageUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        ValidateProfileImageUrl(profileImageUrl);

        // Wrap in a transaction to prevent race conditions when two requests
        // arrive simultaneously for the same new user
        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var user = await FindOrCreateUserAsync(normalizedEmail, authProviderSubject, displayName, profileImageUrl,
            cancellationToken);

        var existingProfile = await workProfileRepository.FindByUserIdAsync(user.Id, cancellationToken);
        if (existingProfile is not null)
        {
            await tx.CommitAsync(cancellationToken);
            return (user.Id, existingProfile.Id);
        }

        // No work profile yet — create a personal org + membership + work profile
        var personalOrg = new DataAccess.Models.Organization
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

    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateUserProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FindByIdAsync(userId, cancellationToken)
                   ?? throw new KeyNotFoundException("User not found.");

        var normalizedDisplayName = NormalizeRequired(command.DisplayName, "Display name is required.");
        var normalizedEmail = NormalizeEmail(command.Email);
        var normalizedTimezone = NormalizeTimezone(command.Timezone);
        var normalizedProfileImageUrl = NormalizeOptional(command.ProfileImageUrl);

        ValidateProfileImageUrl(normalizedProfileImageUrl);

        if (await userRepository.IsEmailTakenByOtherAsync(userId, normalizedEmail, cancellationToken))
            throw new ArgumentException("Email is already in use.");

        user.DisplayName = normalizedDisplayName;
        user.Email = normalizedEmail;
        user.ProfileImageUrl = normalizedProfileImageUrl;
        user.Timezone = normalizedTimezone;
        user.Username = BuildUsername(normalizedEmail, normalizedDisplayName);
        user.EditedAt = DateTime.UtcNow;

        await userRepository.UpdateAsync(user, cancellationToken);
        return MapProfile(user);
    }

    private async Task<User> FindOrCreateUserAsync(string normalizedEmail, string? authProviderSubject,
        string? displayName, string? profileImageUrl, CancellationToken cancellationToken)
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
                DisplayName = NormalizeOptional(displayName),
                Username = BuildUsername(normalizedEmail, displayName),
                ProfileImageUrl = NormalizeOptional(profileImageUrl),
                Timezone = "Europe/Berlin",
                CreatedAt = DateTime.UtcNow,
            };
            await userRepository.AddAsync(user, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(authProviderSubject) &&
                 string.IsNullOrWhiteSpace(user.AuthProviderSubject))
        {
            user.AuthProviderSubject = authProviderSubject;
            user.EditedAt = DateTime.UtcNow;
            await userRepository.UpdateAsync(user, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(authProviderSubject) &&
                 string.Equals(user.AuthProviderSubject, authProviderSubject, StringComparison.Ordinal) &&
                 !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (await userRepository.IsEmailTakenByOtherAsync(user.Id, normalizedEmail, cancellationToken))
                throw new ArgumentException("Email is already in use.");

            user.Email = normalizedEmail;
            user.Username = BuildUsername(normalizedEmail, user.DisplayName);
            user.EditedAt = DateTime.UtcNow;
            await userRepository.UpdateAsync(user, cancellationToken);
        }

        return user;
    }

    private static UserProfileDto MapProfile(User user) => new(
        user.Id,
        user.Username ?? BuildUsername(user.Email, user.DisplayName),
        user.DisplayName ?? user.Username ?? BuildUsername(user.Email, user.DisplayName),
        user.Email,
        user.ProfileImageUrl,
        user.Timezone ?? "Europe/Berlin");

    private static string BuildUsername(string email, string? displayName)
    {
        var preferred = NormalizeOptional(displayName);
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        return NormalizeOptional(email.Split('@')[0]) ?? "user";
    }

    private static string NormalizeRequired(string? value, string errorMessage) =>
        NormalizeOptional(value) ?? throw new ArgumentException(errorMessage);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeEmail(string email)
    {
        var normalized = NormalizeRequired(email, "Email is required.").ToLowerInvariant();
        if (!EmailValidator.IsValid(normalized))
        {
            throw new ArgumentException("Email format is invalid.");
        }

        return normalized;
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
}