using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

/// <summary>
///     Entity Framework Core implementation of IUserRepository.
///     Provides database access for user account operations.
/// </summary>
public class UserRepository(TeapotDbContext context) : IUserRepository
{
    /// <summary>
    ///     Finds a user by their Auth0 authentication provider subject.
    /// </summary>
    public Task<User?> FindByAuthProviderSubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
        return context.Users.FirstOrDefaultAsync(u => u.AuthProviderSubject == subject, cancellationToken);
    }

    /// <summary>
    ///     Finds a user by their email address.
    /// </summary>
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        return context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    /// <summary>
    ///     Finds a user by their unique identifier using EF Core's optimized Find method.
    /// </summary>
    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Users.FindAsync([id], cancellationToken);
    }

    /// <summary>
    ///     Checks if another user already has the given email.
    /// </summary>
    public Task<bool> IsEmailTakenByOtherAsync(Guid userId, string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        return context.Users.AnyAsync(u => u.Id != userId && u.Email == normalizedEmail, cancellationToken);
    }

    /// <summary>
    ///     Creates a new user account and immediately saves to database.
    /// </summary>
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     Updates an existing user account and saves changes to database.
    /// </summary>
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}