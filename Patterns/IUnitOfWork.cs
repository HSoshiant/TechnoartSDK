namespace TechnoartSDK.Patterns;

/// <summary>
/// Unit of Work interface for transactional consistency across multiple repositories.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
