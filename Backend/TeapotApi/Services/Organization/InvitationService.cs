using System.Net;
using System.Net.Mail;
using System.Text;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Services.Organizations;

public class InvitationService(
    IInvitationRepository invitationRepository,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    IMembershipRepository membershipRepository,
    IOptions<EmailOptions> emailOptions,
    ILogger<InvitationService> logger) : IInvitationService
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task<InvitationDto> SendInvitationAsync(
        string email,
        Guid organizationId,
        int expiryDays,
        Guid? createdByUserId = null,
        string? createdByEmail = null,
        string? firstName = null,
        string? lastName = null)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedCreatorEmail = string.IsNullOrWhiteSpace(createdByEmail) ? null : NormalizeEmail(createdByEmail);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("Email is required.");

        var organization = await organizationRepository.FindByIdAsync(organizationId)
            ?? throw new ArgumentException($"Organization with ID {organizationId} not found.");

        var creator = await ResolveCreatorAsync(createdByUserId, normalizedCreatorEmail);

        var creatorMembership = await membershipRepository.FindOrganizerAsync(organizationId, creator.Id);
        if (creatorMembership is null)
            throw new InvalidOperationException("Only organizers are allowed to invite members.");

        if (await membershipRepository.IsMemberByEmailAsync(organizationId, normalizedEmail))
            throw new InvalidOperationException("User is already a member of this organization.");

        var existingInvitation = await invitationRepository.FindOpenAsync(organizationId, normalizedEmail);
        if (existingInvitation is not null)
            throw new InvalidOperationException("An open invitation already exists for this email address.");

        var invitation = new Invitation
        {
            OrganizationId = organizationId,
            CreatedBy = creator.Id,
            Email = normalizedEmail,
            FirstName = firstName,
            LastName = lastName,
            Status = EInvitationStatus.Open,
            ExpiryDate = DateTime.UtcNow.AddDays(expiryDays)
        };

        await invitationRepository.AddAsync(invitation);
        await SendInvitationEmailAsync(invitation, organization);

        return MapToDto(invitation);
    }

    public async Task<bool> AcceptInvitationAsync(Guid invitationId, Guid userId)
    {
        var invitation = await invitationRepository.FindByIdAsync(invitationId)
            ?? throw new ArgumentException($"Invitation with ID {invitationId} not found.");

        if (invitation.Status != EInvitationStatus.Open)
            throw new InvalidOperationException($"Invitation cannot be accepted. Status: {invitation.Status}");

        if (invitation.ExpiryDate < DateTime.UtcNow)
        {
            invitation.Status = EInvitationStatus.Expired;
            await invitationRepository.UpdateAsync(invitation);
            throw new InvalidOperationException("Invitation has expired.");
        }

        var user = await userRepository.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("An account must be created or signed in first for this invitation.");

        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The invitation can only be accepted with the invited email address.");

        var existingMembership = await membershipRepository.FindAsync(userId, invitation.OrganizationId);
        if (existingMembership is null)
        {
            var membership = new Membership
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = invitation.OrganizationId,
                Role = ERole.User,
                CreatedAt = DateTime.UtcNow
            };
            await membershipRepository.AddAsync(membership);
        }

        invitation.Status = EInvitationStatus.Accepted;
        invitation.EditedAt = DateTime.UtcNow;
        await invitationRepository.UpdateAsync(invitation);

        return true;
    }

    public async Task<bool> AcceptInvitationByEmailAsync(Guid invitationId, string email)
    {
        var normalizedEmail = NormalizeEmail(email);

        var invitation = await invitationRepository.FindByIdAsync(invitationId)
            ?? throw new ArgumentException($"Invitation with ID {invitationId} not found.");

        if (!string.Equals(invitation.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This invitation belongs to a different email address.");

        var existingUser = await userRepository.FindByEmailAsync(normalizedEmail)
            ?? throw new InvalidOperationException("Please create an account or sign in with the invited email address first.");

        return await AcceptInvitationAsync(invitationId, existingUser.Id);
    }

    public async Task<bool> RejectInvitationAsync(Guid invitationId)
    {
        var invitation = await invitationRepository.FindByIdAsync(invitationId)
            ?? throw new ArgumentException($"Invitation with ID {invitationId} not found.");

        invitation.Status = EInvitationStatus.Closed;
        invitation.EditedAt = DateTime.UtcNow;
        await invitationRepository.UpdateAsync(invitation);

        return true;
    }

    public async Task<InvitationDto?> GetInvitationAsync(Guid invitationId)
    {
        var invitation = await invitationRepository.FindByIdAsync(invitationId);
        return invitation is null ? null : MapToDto(invitation);
    }

    public async Task<IEnumerable<InvitationDto>> GetPendingInvitationsForEmailAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var invitations = await invitationRepository.GetPendingForEmailAsync(normalizedEmail);
        return invitations.Select(MapToDto);
    }

    public async Task<IEnumerable<InvitationDto>> GetInvitationsForOrganizationAsync(Guid organizationId)
    {
        var invitations = await invitationRepository.GetForOrganizationAsync(organizationId);
        return invitations.Select(MapToDto);
    }

    public async Task<int> CleanupExpiredInvitationsAsync()
    {
        var expiredInvitations = (await invitationRepository.GetExpiredOpenAsync()).ToList();

        foreach (var invitation in expiredInvitations)
            invitation.Status = EInvitationStatus.Expired;

        await invitationRepository.UpdateRangeAsync(expiredInvitations);
        return expiredInvitations.Count;
    }

    private async Task<User> ResolveCreatorAsync(Guid? createdByUserId, string? normalizedCreatorEmail)
    {
        if (createdByUserId.HasValue)
        {
            var creator = await userRepository.FindByIdAsync(createdByUserId.Value);
            if (creator is not null) return creator;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCreatorEmail))
        {
            var creator = await userRepository.FindByEmailAsync(normalizedCreatorEmail);
            if (creator is not null) return creator;
        }

        throw new ArgumentException("Inviting user could not be found.");
    }

    private async Task SendInvitationEmailAsync(Invitation invitation, Organization organization)
    {
        if (string.IsNullOrWhiteSpace(_emailOptions.SmtpHost) ||
            string.IsNullOrWhiteSpace(_emailOptions.SmtpUsername) ||
            string.IsNullOrWhiteSpace(_emailOptions.SmtpPassword) ||
            string.IsNullOrWhiteSpace(_emailOptions.FromEmail))
        {
            logger.LogWarning("Email configuration incomplete. Invitation {InvitationId} created, but email not sent.", invitation.Id);
            return;
        }

        using var smtpClient = new SmtpClient(_emailOptions.SmtpHost, _emailOptions.SmtpPort)
        {
            Credentials = new System.Net.NetworkCredential(_emailOptions.SmtpUsername, _emailOptions.SmtpPassword),
            EnableSsl = true
        };

        var acceptUrl = BuildAcceptLink(invitation);
        var rejectUrl = $"{TrimTrailingSlash(_emailOptions.ApiBaseUrl)}/api/Invitation/{invitation.Id}/reject-link";

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_emailOptions.FromEmail, "Teapot"),
            Subject = $"You are invited to {organization.Name}!",
            Body = GenerateInvitationEmailBody(organization, invitation, acceptUrl, rejectUrl),
            IsBodyHtml = false
        };

        mailMessage.To.Add(invitation.Email);

        try
        {
            await smtpClient.SendMailAsync(mailMessage);
            logger.LogInformation("Invitation email sent to {Email}.", invitation.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending invitation email to {Email}.", invitation.Email);
        }
    }

    private static string GenerateInvitationEmailBody(Organization organization, Invitation invitation, string acceptUrl, string rejectUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hello {invitation.FirstName ?? ""},");
        sb.AppendLine();
        sb.AppendLine($"You have been invited to join the organization '{organization.Name}'!");
        sb.AppendLine();
        sb.AppendLine($"Description: {organization.Description}");
        sb.AppendLine();
        sb.AppendLine("Click the link below to sign in or create an account and then join the organization:");
        sb.AppendLine(acceptUrl);
        sb.AppendLine();
        sb.AppendLine("If you want to decline the invitation, you can use this link instead:");
        sb.AppendLine(rejectUrl);
        sb.AppendLine();
        sb.AppendLine("This invitation expires in 7 days.");
        sb.AppendLine();
        sb.AppendLine("Best regards,");
        sb.AppendLine("The Teapot Team");

        return sb.ToString();
    }

    private InvitationDto MapToDto(Invitation invitation) => new()
    {
        Id = invitation.Id,
        OrganizationId = invitation.OrganizationId,
        Email = invitation.Email,
        FirstName = invitation.FirstName,
        LastName = invitation.LastName,
        Status = invitation.Status.ToString(),
        CreatedAt = invitation.CreatedAt,
        ExpiryDate = invitation.ExpiryDate,
        InvitationLink = BuildAcceptLink(invitation)
    };

    private string BuildAcceptLink(Invitation invitation) =>
        $"{TrimTrailingSlash(_emailOptions.ApiBaseUrl)}/api/Invitation/{invitation.Id}/accept-link?email={WebUtility.UrlEncode(invitation.Email)}";

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string TrimTrailingSlash(string url) => url.TrimEnd('/');
}
