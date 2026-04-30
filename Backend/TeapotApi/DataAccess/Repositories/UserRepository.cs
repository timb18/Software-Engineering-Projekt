using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class UserRepository(TeapotDbContext context) : IUserRepository
{
    public Task<User?> FindByAuthProviderSubjectAsync(string subject, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.AuthProviderSubject == subject, cancellationToken);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Users.FindAsync([id], cancellationToken);

    public Task<bool> IsEmailTakenByOtherAsync(Guid userId, string normalizedEmail, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(u => u.Id != userId && u.Email.ToLowerInvariant() == normalizedEmail.ToLowerInvariant(), cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
