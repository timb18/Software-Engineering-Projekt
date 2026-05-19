using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using DataAccess;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.Extensions.Options;

namespace Services.Organizations;

/// <summary>
/// Handles invitation creation, acceptance, rejection, and cleanup.
/// </summary>
public class InvitationService(
    IInvitationRepository invitationRepository,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    IMembershipRepository membershipRepository,
    IWorkProfileRepository workProfileRepository,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions) : IInvitationService
{
    private static readonly Regex EmailPattern = new(
        @"^[^\s@]+@[^\s@.]+(?:\.[^\s@.]+)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly EmailOptions _emailOptions = emailOptions.Value;

    /// <summary>
    /// Creates an invitation, persists it, and attempts to send the invitation email.
    /// </summary>
    public async Task<InvitationDto> SendInvitationAsync(
        string email,
        Guid organizationId,
        int expiryDays,
        Guid? createdByUserId = null,
        string? createdByEmail = null,
        string? firstName = null,
        string? lastName = null,
        string? publicApiBaseUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedCreatorEmail = string.IsNullOrWhiteSpace(createdByEmail) ? null : NormalizeEmail(createdByEmail);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("Email is required.");

        var organization = await organizationRepository.FindByIdAsync(organizationId, cancellationToken)
            ?? throw new ArgumentException($"Organization with ID {organizationId} not found.");

        var creator = await ResolveCreatorAsync(createdByUserId, normalizedCreatorEmail, cancellationToken);

        var creatorMembership = await membershipRepository.FindOrganizerAsync(organizationId, creator.Id, cancellationToken);
        if (creatorMembership is null)
            throw new InvalidOperationException("Only organizers are allowed to invite members.");

        if (await membershipRepository.IsMemberByEmailAsync(organizationId, normalizedEmail, cancellationToken))
            throw new InvalidOperationException("User is already a member of this organization.");

        var existingInvitation = await invitationRepository.FindOpenAsync(organizationId, normalizedEmail, cancellationToken);
        if (existingInvitation is not null)
            throw new InvalidOperationException("An open invitation already exists for this email address.");

        var invitation = new Invitation
        {
            OrganizationId = organizationId,
            Organization = organization,
            CreatedBy = creator.Id,
            Email = normalizedEmail,
            FirstName = firstName,
            LastName = lastName,
            Status = EInvitationStatus.Open,
            ExpiryDate = DateTime.UtcNow.AddDays(expiryDays)
        };

        await invitationRepository.AddAsync(invitation, cancellationToken);
        var emailSent = false;
        string? emailError = null;
        try
        {
            await SendInvitationEmailAsync(invitation, organization, expiryDays, publicApiBaseUrl, cancellationToken);
            emailSent = true;
        }
        catch (Exception ex)
        {
            emailSent = false;
            emailError = ex.Message;
        }

        return MapToDto(invitation, publicApiBaseUrl) with
        {
            EmailSent = emailSent,
            EmailError = emailError
        };
    }

    /// <summary>
    /// Accepts an invitation by authenticated user id and creates membership data when needed.
    /// </summary>
    public async Task<bool> AcceptInvitationAsync(Guid invitationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var invitation = await invitationRepository.FindByIdAsync(invitationId, cancellationToken)
            ?? throw new ArgumentException($"Invitation with ID {invitationId} not found.");

        if (invitation.Status != EInvitationStatus.Open)
            throw new InvalidOperationException($"Invitation cannot be accepted. Status: {invitation.Status}");

        if (invitation.ExpiryDate < DateTime.UtcNow)
        {
            invitation.Status = EInvitationStatus.Expired;
            await invitationRepository.UpdateAsync(invitation, cancellationToken);
            throw new InvalidOperationException("Invitation has expired.");
        }

        var user = await userRepository.FindByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("An account must be created or signed in first for this invitation.");

        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The invitation can only be accepted with the invited email address.");

        var existingMembership = await membershipRepository.FindAsync(userId, invitation.OrganizationId, cancellationToken);
        if (existingMembership is null)
        {
            await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);

            var membership = new Membership
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = invitation.OrganizationId,
                Role = ERole.User,
                CreatedAt = DateTime.UtcNow
            };
            await membershipRepository.AddAsync(membership, cancellationToken);

            var workProfile = new DataAccess.Models.WorkProfile
            {
                MembershipId = membership.Id,
                MaxDailyLoad = TimeSpan.FromHours(8),
                CreatedAt = DateTime.UtcNow,
            };
            await workProfileRepository.AddAsync(workProfile, cancellationToken);

            invitation.Status = EInvitationStatus.Accepted;
            invitation.EditedAt = DateTime.UtcNow;
            await invitationRepository.UpdateAsync(invitation, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        else
        {
            invitation.Status = EInvitationStatus.Accepted;
            invitation.EditedAt = DateTime.UtcNow;
            await invitationRepository.UpdateAsync(invitation, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Accepts an invitation after resolving the user account by email.
    /// </summary>
    public async Task<bool> AcceptInvitationByEmailAsync(Guid invitationId, string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);

        var invitation = await invitationRepository.FindByIdAsync(invitationId, cancellationToken)
            ?? throw new ArgumentException($"Invitation with ID {invitationId} not found.");

        if (!string.Equals(invitation.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This invitation belongs to a different email address.");

        var existingUser = await userRepository.FindByEmailAsync(normalizedEmail, cancellationToken)
            ?? throw new InvalidOperationException("Please create an account or sign in with the invited email address first.");

        return await AcceptInvitationAsync(invitationId, existingUser.Id, cancellationToken);
    }

    /// <summary>
    /// Rejects an invitation and removes it from the database.
    /// </summary>
    public async Task<bool> RejectInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await invitationRepository.FindByIdAsync(invitationId, cancellationToken)
            ?? throw new ArgumentException($"Invitation with ID {invitationId} not found.");

        await invitationRepository.DeleteAsync(invitation, cancellationToken);

        return true;
    }

    /// <summary>
    /// Loads a single invitation by id.
    /// </summary>
    public async Task<InvitationDto?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await invitationRepository.FindByIdAsync(invitationId, cancellationToken);
        return invitation is null ? null : MapToDto(invitation);
    }

    /// <summary>
    /// Returns all pending invitations for the given email address.
    /// </summary>
    public async Task<IEnumerable<InvitationDto>> GetPendingInvitationsForEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var invitations = await invitationRepository.GetPendingForEmailAsync(normalizedEmail, cancellationToken);
        return invitations.Select(invitation => MapToDto(invitation));
    }

    /// <summary>
    /// Returns all invitations for the given organization.
    /// </summary>
    public async Task<IEnumerable<InvitationDto>> GetInvitationsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var invitations = await invitationRepository.GetForOrganizationAsync(organizationId, cancellationToken);
        return invitations.Select(invitation => MapToDto(invitation));
    }

    /// <summary>
    /// Marks open invitations as expired when their expiration date has passed.
    /// </summary>
    public Task<int> CleanupExpiredInvitationsAsync(CancellationToken cancellationToken = default) =>
        invitationRepository.MarkExpiredInvitationsAsync(cancellationToken);

    private async Task<DataAccess.Models.User> ResolveCreatorAsync(Guid? createdByUserId, string? normalizedCreatorEmail, CancellationToken cancellationToken)
    {
        if (createdByUserId.HasValue)
        {
            var creator = await userRepository.FindByIdAsync(createdByUserId.Value, cancellationToken);
            if (creator is not null) return creator;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCreatorEmail))
        {
            var creator = await userRepository.FindByEmailAsync(normalizedCreatorEmail, cancellationToken);
            if (creator is not null) return creator;
        }

        throw new ArgumentException("Inviting user could not be found.");
    }

    private async Task SendInvitationEmailAsync(
        Invitation invitation,
        DataAccess.Models.Organization organization,
        int expiryDays,
        string? publicApiBaseUrl,
        CancellationToken cancellationToken)
    {
        var apiBaseUrl = ResolveApiBaseUrl(publicApiBaseUrl);
        var acceptUrl = BuildAcceptLink(invitation, apiBaseUrl);
        var rejectUrl = $"{apiBaseUrl}/api/Invitation/{invitation.Id}/reject-link";
        var body = GenerateInvitationEmailBody(organization, invitation, acceptUrl, rejectUrl, expiryDays);
        var subject = $"You are invited to {organization.Name}!";
        await emailSender.SendAsync(invitation.Email, subject, body, cancellationToken);
    }

    private static string GenerateInvitationEmailBody(DataAccess.Models.Organization organization, Invitation invitation, string acceptUrl, string rejectUrl, int expiryDays)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hello {invitation.FirstName ?? ""},");
        sb.AppendLine();
        sb.AppendLine($"You have been invited to join the organization '{organization.Name}'!");
        sb.AppendLine();
        sb.AppendLine("Click the link below to sign in or create an account and then join the organization:");
        sb.AppendLine(acceptUrl);
        sb.AppendLine();
        sb.AppendLine("If you want to decline the invitation, you can use this link instead:");
        sb.AppendLine(rejectUrl);
        sb.AppendLine();
        sb.AppendLine($"This invitation expires in {expiryDays} day(s).");
        sb.AppendLine();
        sb.AppendLine("Best regards,");
        sb.AppendLine("The Teapot Team");

        return sb.ToString();
    }

    private InvitationDto MapToDto(Invitation invitation, string? publicApiBaseUrl = null) => new()
    {
        Id = invitation.Id,
        OrganizationId = invitation.OrganizationId,
        OrganizationName = invitation.Organization?.Name,
        Email = invitation.Email,
        FirstName = invitation.FirstName,
        LastName = invitation.LastName,
        Status = invitation.Status.ToString(),
        CreatedAt = invitation.CreatedAt,
        ExpiryDate = invitation.ExpiryDate,
        InvitationLink = BuildAcceptLink(invitation, ResolveApiBaseUrl(publicApiBaseUrl))
    };

    private string BuildAcceptLink(Invitation invitation, string apiBaseUrl) =>
        $"{apiBaseUrl}/api/Invitation/{invitation.Id}/accept-link?email={WebUtility.UrlEncode(invitation.Email)}";

    private string ResolveApiBaseUrl(string? publicApiBaseUrl)
    {
        var configuredBaseUrl = TrimTrailingSlash(_emailOptions.ApiBaseUrl);
        var requestBaseUrl = string.IsNullOrWhiteSpace(publicApiBaseUrl)
            ? null
            : TrimTrailingSlash(publicApiBaseUrl);

        if (!string.IsNullOrWhiteSpace(requestBaseUrl) &&
            (string.IsNullOrWhiteSpace(configuredBaseUrl) || IsLocalBaseUrl(configuredBaseUrl)))
            return requestBaseUrl;

        return configuredBaseUrl;
    }

    private static bool IsLocalBaseUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase));

    private static string NormalizeEmail(string email)
    {
        var normalized = RemoveInvisibleEmailCharacters(email).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Email is required.");

        if (!EmailPattern.IsMatch(normalized))
            throw new ArgumentException("Email format is invalid.");

        return normalized;
    }

    private static string RemoveInvisibleEmailCharacters(string email) =>
        email
            .Replace("\u200B", string.Empty, StringComparison.Ordinal)
            .Replace("\u200C", string.Empty, StringComparison.Ordinal)
            .Replace("\u200D", string.Empty, StringComparison.Ordinal)
            .Replace("\uFEFF", string.Empty, StringComparison.Ordinal);

    private static string TrimTrailingSlash(string url) => url.TrimEnd('/');
}
