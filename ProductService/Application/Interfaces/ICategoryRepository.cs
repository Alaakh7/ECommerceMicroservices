using ProductService.Domain.Entities;

namespace ProductService.Application.Interfaces;

public interface ICategoryRepository
{
    IQueryable<Category> Query();
    Task<Category?> GetTrackedAsync(Guid id, CancellationToken cancellationToken);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> HasActiveProductsAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Category category, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
