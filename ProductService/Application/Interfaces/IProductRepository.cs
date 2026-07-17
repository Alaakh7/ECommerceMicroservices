using ProductService.Domain.Entities;

namespace ProductService.Application.Interfaces;

public interface IProductRepository
{
    IQueryable<Product> Query();
    Task<Product?> GetTrackedAsync(Guid id, CancellationToken cancellationToken);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken);
    Task<bool> SkuExistsAsync(string sku, Guid? exceptId, CancellationToken cancellationToken);
    Task AddAsync(Product product, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
