using System;
using System.Linq;
//+ datenbank
using Model;

namespace Services;

public class InvitationService
{
    private readonly ApplicationDbContext _context;

    public InvitationService(ApplicationDbContext context)
    {
        _context = context;
    }

    // berechnet, wie viele Einladungen noch möglich sind
    public int GetRemainingInvitations(Guid organizationId)
    {
        var organization = _context.Organizations.FirstOrDefault(o => o.Id == organizationId);
        if (organization == null)
        {
            throw new Exception("Organisation nicht gefunden");
        }

        var currentMembers = _context.Memberships.Count(m => m.OrganisationId == organizationId);

        var pendingInvitations = _context.Invitations.Count(i =>
            i.OrganizationId == organizationId &&
            i.Status == InvitationStatus.PENDING &&
            i.ExpiryDate > DateTime.UtcNow);

        return organization.MaxUsers - currentMembers - pendingInvitations;
    }

    // erstellt eine Einladung
    public Invitation CreateInvitation(Guid organizationId, Guid createdByUserId)
    {
        var remaining = GetRemainingInvitations(organizationId);

        if (remaining <= 0)
        {
            throw new Exception("Einladungslimit erreicht");
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedBy = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            EditedAt = DateTime.UtcNow,
            Status = InvitationStatus.PENDING,
            ExpiryDate = DateTime.UtcNow.AddDays(7)
        };

        _context.Invitations.Add(invitation);
        _context.SaveChanges();

        return invitation;
    }

    // akzeptiert Einladung und erstellt Membership
    public void AcceptInvitation(Guid invitationId, Guid userId)
    {
        var invitation = _context.Invitations.FirstOrDefault(i => i.Id == invitationId);

        if (invitation == null)
        {
            throw new Exception("Einladung nicht gefunden");
        }

        if (invitation.Status != InvitationStatus.PENDING)
        {
            throw new Exception("Einladung ist nicht mehr gültig");
        }

        if (invitation.ExpiryDate <= DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.EXPIRED;
            invitation.EditedAt = DateTime.UtcNow;
            _context.SaveChanges();

            throw new Exception("Einladung ist abgelaufen");
        }

        var alreadyMember = _context.Memberships.Any(m =>
            m.OrganisationId == invitation.OrganizationId &&
            m.UserId == userId);

        if (alreadyMember)
        {
            throw new Exception("Benutzer ist bereits Mitglied der Organisation");
        }

        var membership = new Membership
        {
            UserId = userId,
            OrganisationId = invitation.OrganizationId,
            Role = Role.USER
        };

        _context.Memberships.Add(membership);

        invitation.Status = InvitationStatus.ACCEPTED;
        invitation.EditedAt = DateTime.UtcNow;

        _context.SaveChanges();
    }
}