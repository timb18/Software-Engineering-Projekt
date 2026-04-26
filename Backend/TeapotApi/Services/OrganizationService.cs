using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class OrganizationService(
    IGenericRepository<Organization> organizationRepository) : IOrganizationService
{
    public async Task<IEnumerable<OrganizationDetailsDto>> GetOrganizationsForUserAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var organizations = await organizationRepository.GetQueryable()
            .Include(o => o.Memberships)
                .ThenInclude(m => m.User)
            .Include(o => o.Invitations)
            .Where(o => o.Memberships.Any(m => m.User.Email.ToLower() == normalizedEmail))
            .OrderBy(o => o.Name)
            .ToListAsync();

        return organizations.Select(o => new OrganizationDetailsDto
        {
            Id = o.Id,
            Name = o.Name,
            Description = o.Description,
            MaxUsers = o.MaxUsers,
            Users = o.Memberships
                .OrderBy(m => m.User.Username)
                .Select(m => new OrganizationUserDto
                {
                    Id = m.User.Id,
                    Email = m.User.Email,
                    Username = m.User.Username ?? m.User.Email,
                    Role = m.Role.ToString().ToLowerInvariant()
                })
                .ToList(),
            Invites = o.Invitations
                .Where(i => i.Status == EInvitationStatus.Open && i.ExpiryDate > DateTime.UtcNow)
                .OrderBy(i => i.CreatedAt)
                .Select(i => new InvitationDto
                {
                    Id = i.Id,
                    OrganizationId = i.OrganizationId,
                    Email = i.Email,
                    FirstName = i.FirstName,
                    LastName = i.LastName,
                    Status = i.Status.ToString().ToLowerInvariant(),
                    CreatedAt = i.CreatedAt,
                    ExpiryDate = i.ExpiryDate,
                    InvitationLink = string.Empty
                })
                .ToList()
        });
    }
}
