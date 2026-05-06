using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class InvitationRepository(TeapotDbContext context) : IInvitationRepository
{
    public async Task<Invitation?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Invitations.FindAsync([id], cancellationToken);

    public Task<Invitation?> FindOpenAsync(Guid organizationId, string normalizedEmail, CancellationToken cancellationToken = default) =>
        context.Invitations.FirstOrDefaultAsync(i =>
            i.OrganizationId == organizationId &&
            i.Email == normalizedEmail &&
            i.Status == EInvitationStatus.Open &&
            i.ExpiryDate > DateTime.UtcNow, cancellationToken);

    public async Task<IEnumerable<Invitation>> GetPendingForEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        await context.Invitations
            .Include(i => i.Organization)
            .Where(i => i.Email == normalizedEmail &&
                        i.Status == EInvitationStatus.Open &&
                        i.ExpiryDate > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<Invitation>> GetForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        await context.Invitations
            .Include(i => i.Organization)
            .Where(i => i.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<Invitation>> GetExpiredOpenAsync(CancellationToken cancellationToken = default) =>
        await context.Invitations
            .Where(i => i.ExpiryDate < DateTime.UtcNow && i.Status == EInvitationStatus.Open)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        context.Invitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        context.Invitations.Update(invitation);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IEnumerable<Invitation> invitations, CancellationToken cancellationToken = default)
    {
        context.Invitations.UpdateRange(invitations);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        context.Invitations.Remove(invitation);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> MarkExpiredInvitationsAsync(CancellationToken cancellationToken = default) =>
        context.Invitations
            .Where(i => i.ExpiryDate < DateTime.UtcNow && i.Status == EInvitationStatus.Open)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Status, EInvitationStatus.Expired),
                cancellationToken);
}
