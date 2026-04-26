using DataAccess.Models;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Services;

public class UserService(
    TeapotDbContext dbContext,
    IGenericRepository<User> userRepository,
    IGenericRepository<Membership> membershipRepository,
    IGenericRepository<Organization> organizationRepository,
    IGenericRepository<WorkProfile> workProfileRepository) : IUserService
{
    private const string PersonalWorkspaceDescription = "Personal workspace";

    public async Task<(Guid UserId, Guid WorkProfileId)> EnsureUserAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new ArgumentException("Email is required.", nameof(email));

        var existingUser = await userRepository.GetQueryable()
            .Include(u => u.Memberships)
                .ThenInclude(m => m.Organization)
            .Include(u => u.Memberships)
                .ThenInclude(m => m.WorkProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            var existingProfile = existingUser.Memberships
                .OrderByDescending(IsPersonalMembership)
                .Select(m => m.WorkProfile)
                .FirstOrDefault(wp => wp is not null);

            if (existingProfile is not null)
                return (existingUser.Id, existingProfile.Id);

            return await CreatePersonalWorkspaceAsync(existingUser, cancellationToken);
        }

        var username = normalizedEmail.Split('@')[0];
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            Username = username,
            CreatedAt = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, cancellationToken);
        return await CreatePersonalWorkspaceAsync(user, cancellationToken);
    }

    private async Task<(Guid UserId, Guid WorkProfileId)> CreatePersonalWorkspaceAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var existingPersonalMembership = await membershipRepository.GetQueryable()
            .Include(m => m.Organization)
            .Include(m => m.WorkProfile)
            .FirstOrDefaultAsync(
                m => m.UserId == user.Id &&
                     m.Role == ERole.Organizer &&
                     m.Organization.MaxUsers == 1 &&
                     m.Organization.Description == PersonalWorkspaceDescription,
                cancellationToken);

        if (existingPersonalMembership?.WorkProfile is not null)
            return (user.Id, existingPersonalMembership.WorkProfile.Id);

        var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                Name = $"{(user.Username ?? user.Email.Split('@')[0]).Trim()}'s Workspace",
                Description = PersonalWorkspaceDescription,
                MaxUsers = 1,
                CreatedAt = DateTime.UtcNow
            };

            var membership = existingPersonalMembership;
            if (membership is null)
            {
                await organizationRepository.AddAsync(organization, cancellationToken);

                membership = new Membership
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    OrganizationId = organization.Id,
                    Role = ERole.Organizer,
                    CreatedAt = DateTime.UtcNow
                };
                await membershipRepository.AddAsync(membership, cancellationToken);
            }

            var workProfile = new WorkProfile
            {
                Id = Guid.NewGuid(),
                MembershipId = membership.Id,
                MaxDailyLoad = TimeSpan.FromHours(8),
                PlannerViewStart = "06:00",
                PlannerViewEnd = "22:00",
                CreatedAt = DateTime.UtcNow
            };
            await workProfileRepository.AddAsync(workProfile, cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return (user.Id, workProfile.Id);
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private static bool IsPersonalMembership(Membership membership)
    {
        return membership.Role == ERole.Organizer &&
               membership.Organization.MaxUsers == 1 &&
               membership.Organization.Description == PersonalWorkspaceDescription;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
