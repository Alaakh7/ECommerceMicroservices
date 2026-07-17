using Microsoft.EntityFrameworkCore;
using ProductService.Application.Interfaces;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Data;

namespace ProductService.Infrastructure.Repositories;

public sealed class ProductRepository(ProductDbContext dbContext) : IProductRepository
{
    public IQueryable<Product> Query() => dbContext.Products.AsNoTracking();

    public Task<Product?> GetTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Products.Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Query().Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Query().Include(x => x.Category).SingleOrDefaultAsync(x => x.Sku == sku, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.Products.IgnoreQueryFilters().AnyAsync(x => x.Sku == sku && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);

    public Task AddAsync(Product product, CancellationToken cancellationToken) => dbContext.Products.AddAsync(product, cancellationToken).AsTask();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
