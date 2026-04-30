using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class OrganizationService(
    IGenericRepository<Organization> organizationRepository,
    TeapotDbContext dbContext) : IOrganizationService
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

    public async Task DeleteOrganizationAsync(DeleteOrganizationCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OrganizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(command.OrganizationId));

        if (command.InitiatorUserId == Guid.Empty)
            throw new ArgumentException("InitiatorUserId is required.", nameof(command.InitiatorUserId));

        var organization = await dbContext.Organizations
            .Include(o => o.Memberships)
                .ThenInclude(m => m.WorkProfile)
            .Include(o => o.Invitations)
            .FirstOrDefaultAsync(o => o.Id == command.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException("Organization not found.");

        var initiatorMembership = organization.Memberships
            .FirstOrDefault(m => m.UserId == command.InitiatorUserId)
            ?? throw new KeyNotFoundException("Initiator is not a member of this organization.");

        if (initiatorMembership.Role != ERole.Organizer)
            throw new UnauthorizedAccessException("Only organizers can delete an organization.");

        var otherOrganizers = organization.Memberships
            .Where(m => m.UserId != command.InitiatorUserId && m.Role == ERole.Organizer)
            .ToList();

        if (otherOrganizers.Count > 0)
            throw new InvalidOperationException("Die Organisation kann nicht gelöscht werden, solange es weitere Organizer gibt.");

        if (!string.Equals(organization.Name, command.ConfirmationText?.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Confirmation text does not match the organization name.");

        foreach (var membership in organization.Memberships.ToList())
            await DeleteMembershipDataAsync(membership, cancellationToken);

        if (organization.Invitations.Count > 0)
            dbContext.Invitations.RemoveRange(organization.Invitations);

        dbContext.Organizations.Remove(organization);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DeleteMembershipDataAsync(Membership membership, CancellationToken cancellationToken)
    {
        if (membership.WorkProfile is not null)
        {
            var workProfileId = membership.WorkProfile.Id;

            var workDayProfiles = await dbContext.WorkDayProfiles
                .Where(day => day.WorkProfileId == workProfileId)
                .ToListAsync(cancellationToken);

            if (workDayProfiles.Count > 0)
            {
                var workDayProfileIds = workDayProfiles.Select(day => day.Id).ToList();

                var workBlocks = await dbContext.WorkBlocks
                    .Where(block => workDayProfileIds.Contains(block.WorkDayProfileId))
                    .ToListAsync(cancellationToken);

                var workBreaks = await dbContext.WorkBreaks
                    .Where(workBreak => workDayProfileIds.Contains(workBreak.WorkDayProfileId))
                    .ToListAsync(cancellationToken);

                if (workBlocks.Count > 0)
                    dbContext.WorkBlocks.RemoveRange(workBlocks);

                if (workBreaks.Count > 0)
                    dbContext.WorkBreaks.RemoveRange(workBreaks);

                dbContext.WorkDayProfiles.RemoveRange(workDayProfiles);
            }

            var timeIntervals = await dbContext.WorkProfileTimeIntervals
                .Where(interval => interval.WorkProfileId == workProfileId)
                .ToListAsync(cancellationToken);

            if (timeIntervals.Count > 0)
                dbContext.WorkProfileTimeIntervals.RemoveRange(timeIntervals);

            var userTasks = await dbContext.UserTasks
                .Where(task => task.WorkProfileId == workProfileId)
                .ToListAsync(cancellationToken);

            if (userTasks.Count > 0)
                dbContext.UserTasks.RemoveRange(userTasks);

            dbContext.WorkProfiles.Remove(membership.WorkProfile);
        }

        dbContext.Memberships.Remove(membership);
    }
}
