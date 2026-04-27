namespace TechnoartSDK.Patterns;

/// <summary>
/// Generic repository interface providing common CRUD operations for all entities.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
