using Api.Dto;
using DataAccess;
using Microsoft.EntityFrameworkCore;
using Model;

namespace Services;

public class OrganizationQueryService
{
    private readonly TeapotDbContext _context;
    private readonly InvitationService _invitationService;

    public OrganizationQueryService(TeapotDbContext context, InvitationService invitationService)
    {
        _context = context;
        _invitationService = invitationService;
    }

    public async Task<List<OrganizationResponse>> GetOrganizationsForUserAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var organizations = await _context.Organizations
            .Where(o => o.Memberships.Any(m => m.User.Email.ToLower() == normalizedEmail))
            .Include(o => o.Memberships)
                .ThenInclude(m => m.User)
            .Include(o => o.Invitations)
            .OrderBy(o => o.Name)
            .ToListAsync();

        var result = new List<OrganizationResponse>();

        foreach (var organization in organizations)
        {
            var remaining = await _invitationService.GetRemainingInvitationsAsync(organization.Id);

            result.Add(new OrganizationResponse
            {
                Id = organization.Id,
                Name = organization.Name,
                Description = organization.Description,
                RemainingInvitations = remaining,
                Users = organization.Memberships
                    .OrderBy(m => m.User.Username)
                    .Select(m => new OrganizationMemberResponse
                    {
                        Id = m.User.Id,
                        Username = m.User.Username ?? "",
                        Email = m.User.Email,
                        Role = m.Role.ToString().ToLower()
                    })
                    .ToList(),
                Invites = organization.Invitations
                    .Where(i =>
                        i.Status == EInvitationStatus.Open &&
                        (i.ExpiryDate == null || i.ExpiryDate > DateTime.UtcNow))
                    .OrderBy(i => i.CreatedAt)
                    .Select(i => new InvitationResponse
                    {
                        Id = i.Id,
                        OrganizationId = i.OrganizationId,
                        OrgName = organization.Name,
                        FirstName = i.FirstName,
                        LastName = i.LastName,
                        Email = i.Email,
                        Status = i.Status.ToString().ToLower(),
                        ExpiryDate = i.ExpiryDate,
                        InviteCode = i.Id.ToString("N")[..8].ToUpperInvariant()
                    })
                    .ToList()
            });
        }

        return result;
    }
}
