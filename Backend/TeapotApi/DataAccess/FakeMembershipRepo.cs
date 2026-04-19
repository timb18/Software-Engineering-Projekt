using Model;

namespace DataAccess;

public class FakeMembershipRepo : IMembershipRepo
{
    private readonly List<Membership> _fakeMembershipsDb = [];
    private readonly object _sync = new();

    public Task<Membership> CreateAsync(Membership membership, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _fakeMembershipsDb.Add(membership);
        }

        return Task.FromResult(membership);
    }
}
