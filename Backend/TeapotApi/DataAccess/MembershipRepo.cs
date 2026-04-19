using Model;

namespace DataAccess;

public class MembershipRepo : IMembershipRepo
{
    private readonly List<Membership> _memberships = [];
    private readonly object _sync = new();

    public Task<Membership> CreateAsync(Membership membership, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _memberships.Add(membership);
        }

        return Task.FromResult(membership);
    }
}
