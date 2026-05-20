using DataAccess.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace DataAccess;

/// <summary>
///     Provides transaction management capabilities for the database context.
///     Allows coordinating multiple operations within a single database transaction.
/// </summary>
/// <remarks>
///     Used for operations that require atomicity (all-or-nothing execution).
///     Example: creating an organization and its initial membership must both succeed or both fail.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    ///     Begins a new database transaction.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>A transaction object that must be committed or rolled back</returns>
    /// <remarks>
    ///     The transaction should be disposed using "using" statement to ensure proper cleanup.
    ///     Multiple commands within the transaction will all commit together or all rollback on error.
    /// </remarks>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Implementation of IUnitOfWork using Entity Framework Core database transactions.
/// </summary>
public class UnitOfWork(TeapotDbContext context) : IUnitOfWork
{
    /// <summary>
    ///     Begins a new database transaction.
    /// </summary>
    /// <param name="cancellationToken">Token to observe for cancellation requests</param>
    /// <returns>An Entity Framework Core database transaction</returns>
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return context.Database.BeginTransactionAsync(cancellationToken);
    }
}