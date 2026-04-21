using System.Collections.Concurrent;
using Model;

namespace DataAccess;

/// <summary>
/// Thread-safe in-memory implementation of IWorkProfileRepository.
/// Intended for development and testing until a database-backed implementation is provided.
/// </summary>
public class InMemoryWorkProfileRepository : IWorkProfileRepository
{
    private readonly ConcurrentDictionary<string, WorkProfile> _store = new();

    public Task<WorkProfile?> GetByUserIdAsync(string userId)
    {
        _store.TryGetValue(userId, out var profile);
        return Task.FromResult(profile);
    }

    public Task<WorkProfile> UpsertAsync(WorkProfile profile)
    {
        var saved = profile with { UserId = profile.UserId };
        _store[profile.UserId] = saved;
        return Task.FromResult(saved);
    }
}
