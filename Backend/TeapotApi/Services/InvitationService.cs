using Api.Dto;
using DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace Services;

public class InvitationService
{
    private readonly TeapotDbContext _context;
    private readonly IConfiguration _configuration;

    public InvitationService(TeapotDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<int> GetRemainingInvitationsAsync(Guid organizationId)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId);

        if (organization == null)
        {
            throw new InvalidOperationException("Organisation nicht gefunden.");
        }

        var memberCount = await _context.Memberships
            .CountAsync(m => m.OrganizationId == organizationId);

        var openInvitationCount = await _context.Invitations
            .CountAsync(i =>
                i.OrganizationId == organizationId &&
                i.Status == EInvitationStatus.Open &&
                (i.ExpiryDate == null || i.ExpiryDate > DateTime.UtcNow));

        return organization.MaxUsers - memberCount - openInvitationCount;
    }

    public async Task<InvitationResponse> CreateInvitationAsync(CreateInvitationRequest request)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var normalizedCreatorEmail = NormalizeEmail(request.CreatedByEmail);

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new InvalidOperationException("Alle Pflichtfelder müssen ausgefüllt sein.");
        }

        if (!IsValidEmail(normalizedEmail))
        {
            throw new InvalidOperationException("Die E-Mail-Adresse ist ungültig.");
        }

        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId);

        if (organization == null)
        {
            throw new InvalidOperationException("Organisation nicht gefunden.");
        }

        var creator = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedCreatorEmail);

        if (creator == null)
        {
            throw new InvalidOperationException("Einladender Benutzer wurde nicht gefunden.");
        }

        var isOrganizer = await _context.Memberships.AnyAsync(m =>
            m.OrganizationId == request.OrganizationId &&
            m.UserId == creator.Id &&
            m.Role == ERole.Organizer);

        if (!isOrganizer)
        {
            throw new InvalidOperationException("Nur Organisierende dürfen Einladungen versenden.");
        }

        var remaining = await GetRemainingInvitationsAsync(request.OrganizationId);
        if (remaining <= 0)
        {
            throw new InvalidOperationException("Das Einladungskontingent ist aufgebraucht. Erwerben Sie weitere Einladungen, um neue Mitglieder einzuladen.");
        }

        var alreadyOpen = await _context.Invitations.AnyAsync(i =>
            i.OrganizationId == request.OrganizationId &&
            i.Email.ToLower() == normalizedEmail &&
            i.Status == EInvitationStatus.Open &&
            (i.ExpiryDate == null || i.ExpiryDate > DateTime.UtcNow));

        if (alreadyOpen)
        {
            throw new InvalidOperationException("Für diese E-Mail-Adresse existiert bereits eine offene Einladung.");
        }

        var invitedUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (invitedUser == null)
        {
            invitedUser = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                Username = $"{request.FirstName} {request.LastName}".Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(invitedUser);
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            CreatedBy = creator.Id,
            Email = normalizedEmail,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            CreatedAt = DateTime.UtcNow,
            EditedAt = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            Status = EInvitationStatus.Open
        };

        _context.Invitations.Add(invitation);
        await _context.SaveChangesAsync();

        await TrySendInvitationEmailAsync(organization, invitation);

        return new InvitationResponse
        {
            Id = invitation.Id,
            OrganizationId = invitation.OrganizationId,
            OrgName = organization.Name,
            FirstName = invitation.FirstName,
            LastName = invitation.LastName,
            Email = invitation.Email,
            Status = invitation.Status.ToString().ToLower(),
            ExpiryDate = invitation.ExpiryDate
        };
    }

    public async Task<List<InvitationResponse>> GetPendingInvitationsForEmailAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);

        var invitations = await _context.Invitations
            .Include(i => i.Organization)
            .Where(i =>
                i.Email.ToLower() == normalizedEmail &&
                i.Status == EInvitationStatus.Open &&
                (i.ExpiryDate == null || i.ExpiryDate > DateTime.UtcNow))
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();

        return invitations.Select(i => new InvitationResponse
        {
            Id = i.Id,
            OrganizationId = i.OrganizationId,
            OrgName = i.Organization.Name,
            FirstName = i.FirstName,
            LastName = i.LastName,
            Email = i.Email,
            Status = i.Status.ToString().ToLower(),
            ExpiryDate = i.ExpiryDate
        }).ToList();
    }

    public async Task AcceptInvitationAsync(Guid invitationId, string currentUserEmail)
    {
        var normalizedEmail = NormalizeEmail(currentUserEmail);

        var invitation = await _context.Invitations
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Id == invitationId);

        if (invitation == null)
        {
            throw new InvalidOperationException("Einladung nicht gefunden.");
        }

        if (invitation.Status != EInvitationStatus.Open)
        {
            throw new InvalidOperationException("Einladung ist nicht mehr offen.");
        }

        if (invitation.ExpiryDate != null && invitation.ExpiryDate <= DateTime.UtcNow)
        {
            invitation.Status = EInvitationStatus.Expired;
            invitation.EditedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            throw new InvalidOperationException("Einladung ist abgelaufen.");
        }

        if (!string.Equals(invitation.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Diese Einladung gehört zu einer anderen E-Mail-Adresse.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                Username = $"{invitation.FirstName} {invitation.LastName}".Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        var alreadyMember = await _context.Memberships.AnyAsync(m =>
            m.OrganizationId == invitation.OrganizationId &&
            m.UserId == user.Id);

        if (!alreadyMember)
        {
            _context.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                OrganizationId = invitation.OrganizationId,
                UserId = user.Id,
                Role = ERole.User,
                CreatedAt = DateTime.UtcNow
            });
        }

        invitation.Status = EInvitationStatus.Accepted;
        invitation.EditedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DeclineInvitationAsync(Guid invitationId, string currentUserEmail)
    {
        var normalizedEmail = NormalizeEmail(currentUserEmail);

        var invitation = await _context.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId);

        if (invitation == null)
        {
            throw new InvalidOperationException("Einladung nicht gefunden.");
        }

        if (!string.Equals(invitation.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Diese Einladung gehört zu einer anderen E-Mail-Adresse.");
        }

        if (invitation.Status != EInvitationStatus.Open)
        {
            throw new InvalidOperationException("Einladung ist nicht mehr offen.");
        }

        invitation.Status = EInvitationStatus.Closed;
        invitation.EditedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task WithdrawInvitationAsync(Guid invitationId, string requesterEmail)
    {
        var normalizedEmail = NormalizeEmail(requesterEmail);

        var invitation = await _context.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId);

        if (invitation == null)
        {
            throw new InvalidOperationException("Einladung nicht gefunden.");
        }

        var requester = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (requester == null)
        {
            throw new InvalidOperationException("Benutzer nicht gefunden.");
        }

        var isOrganizer = await _context.Memberships.AnyAsync(m =>
            m.OrganizationId == invitation.OrganizationId &&
            m.UserId == requester.Id &&
            m.Role == ERole.Organizer);

        if (!isOrganizer)
        {
            throw new InvalidOperationException("Nur Organisierende dürfen Einladungen zurückziehen.");
        }

        if (invitation.Status != EInvitationStatus.Open)
        {
            throw new InvalidOperationException("Nur offene Einladungen können zurückgezogen werden.");
        }

        invitation.Status = EInvitationStatus.Closed;
        invitation.EditedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    private async Task TrySendInvitationEmailAsync(Organization organization, Invitation invitation)
    {
        var host = _configuration["Smtp:Host"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
        var portValue = _configuration["Smtp:Port"] ?? Environment.GetEnvironmentVariable("SMTP_PORT");
        var username = _configuration["Smtp:Username"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
        var password = _configuration["Smtp:Password"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
        var from = _configuration["Smtp:From"] ?? Environment.GetEnvironmentVariable("SMTP_FROM");
        var frontendBaseUrl =
            _configuration["Frontend:BaseUrl"] ??
            Environment.GetEnvironmentVariable("FRONTEND_BASE_URL") ??
            "http://localhost:5173/orgs";

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(portValue) ||
            string.IsNullOrWhiteSpace(from))
        {
            return;
        }

        if (!int.TryParse(portValue, out var port))
        {
            return;
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true
        };

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new System.Net.NetworkCredential(username, password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(from),
            Subject = $"Einladung zu {organization.Name}",
            Body =
$@"Hallo {invitation.FirstName} {invitation.LastName},

du wurdest zur Organisation ""{organization.Name}"" eingeladen.

Bitte melde dich an oder registriere dich und öffne anschließend:
{frontendBaseUrl}

Die Einladung ist bis {invitation.ExpiryDate:dd.MM.yyyy HH:mm} gültig.

Viele Grüße
TeaPot"
        };

        mail.To.Add(invitation.Email);

        await client.SendMailAsync(mail);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}