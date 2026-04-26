using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class UserService(
    IGenericRepository<User> userRepository,
    IGenericRepository<Organization> orgRepository,
    IGenericRepository<Membership> membershipRepository,
    IGenericRepository<WorkProfile> workProfileRepository,
    TeapotDbContext dbContext) : IUserService
{
    public async Task<(Guid UserId, Guid WorkProfileId)> EnsureUserAsync(
        string email, CancellationToken cancellationToken = default)
    {
        // Wrap in a transaction to prevent race conditions when two requests
        // arrive simultaneously for the same new user
        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = await userRepository.GetQueryable()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            user = new User { Email = email, CreatedAt = DateTime.UtcNow };
            await userRepository.AddAsync(user, cancellationToken);
        }

        // Look for an existing personal work profile (membership to a personal org)
        var existingProfile = await workProfileRepository.GetQueryable()
            .FirstOrDefaultAsync(wp => wp.Membership.UserId == user.Id, cancellationToken);

        if (existingProfile is not null)
        {
            await tx.CommitAsync(cancellationToken);
            return (user.Id, existingProfile.Id);
        }

        // No work profile yet — create a personal org + membership + work profile
        var personalOrg = new Organization
        {
            Name = $"Personal ({email})",
            Description = "Auto-created personal workspace",
            MaxUsers = 1,
            CreatedAt = DateTime.UtcNow,
        };
        await orgRepository.AddAsync(personalOrg, cancellationToken);

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
}