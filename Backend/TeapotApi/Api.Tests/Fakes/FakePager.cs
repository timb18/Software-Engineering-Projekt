using Auth0.ManagementApi;
using Auth0.ManagementApi.Core;

namespace Api.Tests.Fakes;

public class FakePager<T>(List<T> content) : Pager<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<Page<T>> GetNextPageAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<Page<T>> AsPagesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Page<T> CurrentPage { get; } = new(content);
    public bool HasNextPage => false;
}