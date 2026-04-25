using System.Net;
using System.Net.Mail;
using System.Text;
using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Services;

public class InvitationService : IInvitationService
{
    private readonly IGenericRepository<Invitation> _invitationRepository;
    private readonly IGenericRepository<Organization> _organizationRepository;
    private readonly IGenericRepository<User> _userRepository;
    private readonly IGenericRepository<Membership> _membershipRepository;
    private readonly EmailOptions _emailOptions;

    public InvitationService(
        IGenericRepository<Invitation> invitationRepository,
        IGenericRepository<Organization> organizationRepository,
        IGenericRepository<User> userRepository,
        IGenericRepository<Membership> membershipRepository,
        IOptions<EmailOptions> emailOptions)
    {
        _invitationRepository = invitationRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _membershipRepository = membershipRepository;
        _emailOptions = emailOptions.Value;
    }

    public async Task<InvitationDto> SendInvitationAsync(
        string email,
        Guid organizationId,
        Guid? createdByUserId = null,
        string? createdByEmail = null,
        string? firstName = null,
        string? lastName = null)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedCreatorEmail = string.IsNullOrWhiteSpace(createdByEmail) ? null : NormalizeEmail(createdByEmail);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("E-Mail ist erforderlich.");

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
            throw new ArgumentException($"Organisation mit ID {organizationId} nicht gefunden.");

        var creator = await ResolveCreatorAsync(createdByUserId, normalizedCreatorEmail);
        var creatorMembership = await _membershipRepository.GetQueryable()
            .FirstOrDefaultAsync(m =>
                m.OrganizationId == organizationId &&
                m.UserId == creator.Id &&
                m.Role == ERole.Organizer);

        if (creatorMembership == null)
            throw new InvalidOperationException("Nur Organisierende dürfen Mitglieder einladen.");

        var existingMembership = await _membershipRepository.GetQueryable()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m =>
                m.OrganizationId == organizationId &&
                m.User.Email.ToLower() == normalizedEmail);

        if (existingMembership != null)
            throw new InvalidOperationException("Benutzer ist bereits Mitglied dieser Organisation.");

        var existingInvitation = await _invitationRepository.GetQueryable()
            .FirstOrDefaultAsync(i =>
                i.OrganizationId == organizationId &&
                i.Email.ToLower() == normalizedEmail &&
                i.Status == EInvitationStatus.Open &&
                i.ExpiryDate > DateTime.UtcNow);

        if (existingInvitation != null)
            throw new InvalidOperationException("Für diese E-Mail-Adresse existiert bereits eine offene Einladung.");

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedBy = creator.Id,
            Email = normalizedEmail,
            FirstName = firstName,
            LastName = lastName,
            Status = EInvitationStatus.Open,
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        await _invitationRepository.AddAsync(invitation);
        await SendInvitationEmailAsync(invitation, organization);

        return MapToDto(invitation);
    }

    public async Task<bool> AcceptInvitationAsync(Guid invitationId, Guid userId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        if (invitation == null)
            throw new ArgumentException($"Einladung mit ID {invitationId} nicht gefunden.");

        if (invitation.Status != EInvitationStatus.Open)
            throw new InvalidOperationException($"Einladung kann nicht akzeptiert werden. Status: {invitation.Status}");

        if (invitation.ExpiryDate < DateTime.UtcNow)
        {
            invitation.Status = EInvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation);
            throw new InvalidOperationException("Einladung ist abgelaufen.");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("Für diese Einladung muss zuerst ein Konto erstellt oder sich angemeldet werden.");

        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Die Einladung kann nur mit der eingeladenen E-Mail-Adresse angenommen werden.");

        var existingMembership = await _membershipRepository.GetQueryable()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == invitation.OrganizationId);

        if (existingMembership != null)
        {
            invitation.Status = EInvitationStatus.Accepted;
            invitation.EditedAt = DateTime.UtcNow;
            await _invitationRepository.UpdateAsync(invitation);
            return true;
        }

        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = invitation.OrganizationId,
            Role = ERole.User,
            CreatedAt = DateTime.UtcNow
        };

        await _membershipRepository.AddAsync(membership);

        invitation.Status = EInvitationStatus.Accepted;
        invitation.EditedAt = DateTime.UtcNow;
        await _invitationRepository.UpdateAsync(invitation);

        return true;
    }

    public async Task<bool> AcceptInvitationByEmailAsync(Guid invitationId, string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        if (invitation == null)
            throw new ArgumentException($"Einladung mit ID {invitationId} nicht gefunden.");

        if (!string.Equals(invitation.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Diese Einladung gehört zu einer anderen E-Mail-Adresse.");

        var existingUser = await _userRepository.GetQueryable()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existingUser == null)
            throw new InvalidOperationException("Bitte erstelle zuerst ein Konto oder melde dich mit der eingeladenen E-Mail-Adresse an.");

        return await AcceptInvitationAsync(invitationId, existingUser.Id);
    }

    public async Task<bool> RejectInvitationAsync(Guid invitationId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        if (invitation == null)
            throw new ArgumentException($"Einladung mit ID {invitationId} nicht gefunden.");

        invitation.Status = EInvitationStatus.Closed;
        invitation.EditedAt = DateTime.UtcNow;

        await _invitationRepository.UpdateAsync(invitation);

        return true;
    }

    public async Task<InvitationDto?> GetInvitationAsync(Guid invitationId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        return invitation == null ? null : MapToDto(invitation);
    }

    public async Task<IEnumerable<InvitationDto>> GetPendingInvitationsForEmailAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var invitations = (await _invitationRepository.GetAllAsync())
            .Where(i => i.Email.ToLower() == normalizedEmail && i.Status == EInvitationStatus.Open && i.ExpiryDate > DateTime.UtcNow)
            .ToList();

        return invitations.Select(MapToDto);
    }

    public async Task<IEnumerable<InvitationDto>> GetInvitationsForOrganizationAsync(Guid organizationId)
    {
        var invitations = (await _invitationRepository.GetAllAsync())
            .Where(i => i.OrganizationId == organizationId)
            .ToList();

        return invitations.Select(MapToDto);
    }

    public async Task<int> CleanupExpiredInvitationsAsync()
    {
        var invitations = (await _invitationRepository.GetAllAsync())
            .Where(i => i.ExpiryDate < DateTime.UtcNow && i.Status == EInvitationStatus.Open)
            .ToList();

        foreach (var invitation in invitations)
        {
            invitation.Status = EInvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation);
        }

        return invitations.Count;
    }

    private async Task SendInvitationEmailAsync(Invitation invitation, Organization organization)
    {
        if (string.IsNullOrWhiteSpace(_emailOptions.SmtpHost) ||
            string.IsNullOrWhiteSpace(_emailOptions.SmtpUsername) ||
            string.IsNullOrWhiteSpace(_emailOptions.SmtpPassword) ||
            string.IsNullOrWhiteSpace(_emailOptions.FromEmail))
        {
            Console.WriteLine("E-Mail-Konfiguration unvollständig. Einladung erstellt, aber E-Mail nicht versendet.");
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
            Subject = $"Du bist eingeladen zu {organization.Name}!",
            Body = GenerateInvitationEmailBody(organization, invitation, acceptUrl, rejectUrl),
            IsBodyHtml = false
        };

        mailMessage.To.Add(invitation.Email);

        try
        {
            await smtpClient.SendMailAsync(mailMessage);
            Console.WriteLine($"Einladungs-E-Mail versendet an {invitation.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Versenden der Einladungs-E-Mail: {ex.Message}");
        }
    }

    private string GenerateInvitationEmailBody(Organization organization, Invitation invitation, string acceptUrl, string rejectUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hallo {invitation.FirstName ?? ""},");
        sb.AppendLine();
        sb.AppendLine($"Du wurdest eingeladen, der Organisation '{organization.Name}' beizutreten!");
        sb.AppendLine();
        sb.AppendLine($"Beschreibung: {organization.Description}");
        sb.AppendLine();
        sb.AppendLine("Klicke auf den folgenden Link, um dich anzumelden oder ein Konto zu erstellen und danach der Organisation beizutreten:");
        sb.AppendLine(acceptUrl);
        sb.AppendLine();
        sb.AppendLine("Falls du die Einladung ablehnen möchtest, kannst du alternativ diesen Link verwenden:");
        sb.AppendLine(rejectUrl);
        sb.AppendLine();
        sb.AppendLine("Diese Einladung läuft in 7 Tagen ab.");
        sb.AppendLine();
        sb.AppendLine("Viele Grüße,");
        sb.AppendLine("Das Teapot-Team");

        return sb.ToString();
    }

    private InvitationDto MapToDto(Invitation invitation)
    {
        return new InvitationDto
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
    }

    private async Task<User> ResolveCreatorAsync(Guid? createdByUserId, string? createdByEmail)
    {
        User? creator = null;

        if (createdByUserId.HasValue)
            creator = await _userRepository.GetByIdAsync(createdByUserId.Value);

        if (creator == null && !string.IsNullOrWhiteSpace(createdByEmail))
        {
            creator = await _userRepository.GetQueryable()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == createdByEmail);
        }

        return creator ?? throw new ArgumentException("Einladende Person konnte nicht gefunden werden.");
    }

    private string BuildAcceptLink(Invitation invitation)
    {
        return $"{TrimTrailingSlash(_emailOptions.ApiBaseUrl)}/api/Invitation/{invitation.Id}/accept-link?email={WebUtility.UrlEncode(invitation.Email)}";
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string TrimTrailingSlash(string url)
    {
        return url.TrimEnd('/');
    }
}
