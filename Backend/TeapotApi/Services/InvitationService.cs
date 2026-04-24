using System.Net.Mail;
using System.Text;
using DataAccess.Models;
using DataAccess.Repositories;
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

    public async Task<InvitationDto> SendInvitationAsync(string email, Guid organizationId, Guid createdByUserId, string? firstName = null, string? lastName = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("E-Mail ist erforderlich.");

        // Prüfe, ob Organisation existiert
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
            throw new ArgumentException($"Organisation mit ID {organizationId} nicht gefunden.");

        // Prüfe, ob Benutzer bereits Mitglied ist
        var existingMembership = (await _membershipRepository.GetAllAsync())
            .FirstOrDefault(m => m.OrganizationId == organizationId && m.User.Email == email);
        if (existingMembership != null)
            throw new InvalidOperationException("Benutzer ist bereits Mitglied dieser Organisation.");

        // Erstelle neue Einladung
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedBy = createdByUserId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Status = EInvitationStatus.Open,
            CreatedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7) // 7 Tage Gültigkeit
        };

        await _invitationRepository.AddAsync(invitation);

        // Sende E-Mail
        await SendInvitationEmailAsync(invitation, organization);

        return MapToDto(invitation);
    }

    public async Task<bool> AcceptInvitationAsync(Guid invitationId, Guid userId)
    {
        var invitation = await _invitationRepository.GetByIdAsync(invitationId);
        if (invitation == null)
            throw new ArgumentException($"Einladung mit ID {invitationId} nicht gefunden.");

        // Prüfe Status
        if (invitation.Status != EInvitationStatus.Open)
            throw new InvalidOperationException($"Einladung kann nicht akzeptiert werden. Status: {invitation.Status}");

        // Prüfe Ablauf
        if (invitation.ExpiryDate < DateTime.UtcNow)
        {
            invitation.Status = EInvitationStatus.Expired;
            await _invitationRepository.UpdateAsync(invitation);
            throw new InvalidOperationException("Einladung ist abgelaufen.");
        }

        // Hole Benutzer (oder erstelle ihn)
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                Email = invitation.Email,
                Username = invitation.Email.Split('@')[0],
                CreatedAt = DateTime.UtcNow
            };
            await _userRepository.AddAsync(user);
        }

        // Erstelle Membership
        var membership = new Membership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = invitation.OrganizationId,
            Role = ERole.User,
            CreatedAt = DateTime.UtcNow
        };

        await _membershipRepository.AddAsync(membership);

        // Update Einladung
        invitation.Status = EInvitationStatus.Accepted;
        invitation.EditedAt = DateTime.UtcNow;
        await _invitationRepository.UpdateAsync(invitation);

        return true;
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
        var invitations = (await _invitationRepository.GetAllAsync())
            .Where(i => i.Email == email && i.Status == EInvitationStatus.Open && i.ExpiryDate > DateTime.UtcNow)
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

        var acceptUrl = $"https://yourapp.com/invitations/{invitation.Id}/accept";
        var rejectUrl = $"https://yourapp.com/invitations/{invitation.Id}/reject";

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
        sb.AppendLine("Klicke auf einen der folgenden Links:");
        sb.AppendLine($"- Akzeptieren: {acceptUrl}");
        sb.AppendLine($"- Ablehnen: {rejectUrl}");
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
            ExpiryDate = invitation.ExpiryDate
        };
    }
}

