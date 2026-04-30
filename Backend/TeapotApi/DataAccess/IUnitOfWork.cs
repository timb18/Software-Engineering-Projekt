using Microsoft.EntityFrameworkCore.Storage;
using DataAccess.Models;

namespace DataAccess;

public interface IUnitOfWork
{
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public class UnitOfWork(TeapotDbContext context) : IUnitOfWork
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        context.Database.BeginTransactionAsync(cancellationToken);
}
