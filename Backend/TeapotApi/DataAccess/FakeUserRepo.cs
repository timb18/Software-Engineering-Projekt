using Model;

namespace DataAccess;

public interface IUserRepo
{
	Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

	Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
}

public class FakeUserRepo : IUserRepo
{
	private readonly List<User> _fakeUsersDb = [];
	private readonly object _sync = new();

	public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		lock (_sync)
		{
			User? user = _fakeUsersDb.FirstOrDefault(x =>
				string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
			return Task.FromResult(user);
		}
	}

	public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		lock (_sync)
		{
			_fakeUsersDb.Add(user);
		}

		return Task.FromResult(user);
	}

}
