using Microsoft.EntityFrameworkCore;

namespace TechnoartSDK.Patterns;

/// <summary>
/// Extension methods for running actions inside an execution-strategy transaction on a <see cref="DbContext"/>.
/// </summary>
public static class DbContextTransactionExtensions
{
    #region Methods

    /// <summary>
    /// Executes the specified action inside an execution-strategy transaction.
    /// </summary>
    public static async Task ExecuteInTransactionAsync(this DbContext context, Func<Task> action, CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteInTransactionAsync(
            operation: async (cancellationToken) =>
            {
                await action();
            },
            verifySucceeded: (cancellationToken) => Task.FromResult(true),
            cancellationToken: ct);
    }

    /// <summary>
    /// Executes the specified function inside an execution-strategy transaction and returns a result.
    /// </summary>
    public static async Task<T> ExecuteInTransactionAsync<T>(this DbContext context, Func<Task<T>> action, CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteInTransactionAsync(
            operation: async (cancellationToken) =>
            {
                return await action();
            },
            verifySucceeded: (cancellationToken) => Task.FromResult(true),
            cancellationToken: ct);
    }

    #endregion Methods
}
