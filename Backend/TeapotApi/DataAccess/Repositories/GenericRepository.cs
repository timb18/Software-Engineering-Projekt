using System.Linq.Expressions;
using DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories;

public class GenericRepository<T>(TeapotDbContext context) : IGenericRepository<T>, IAsyncDisposable where T : class
{
    protected readonly TeapotDbContext Context = context;
    private readonly DbSet<T> _dbSet = context.Set<T>();

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<T>> GetManyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id], cancellationToken: cancellationToken);
    }

    public async Task<T?> GetByFirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<int> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        return await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> UpdateRangeAsync(IEnumerable<T> entity, CancellationToken cancellationToken = default)
    {
        _dbSet.UpdateRange(entity);
        return await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        return await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteRangeAsync(IEnumerable<T> entity, CancellationToken cancellationToken = default)
    {
        _dbSet.RemoveRange(entity);
        return await Context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        Context.Dispose();
        Context.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}