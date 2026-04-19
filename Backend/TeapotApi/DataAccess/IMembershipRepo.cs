using Model;

namespace DataAccess;

public interface IMembershipRepo
{
    Task<Membership> CreateAsync(Membership membership, CancellationToken cancellationToken = default);
}
