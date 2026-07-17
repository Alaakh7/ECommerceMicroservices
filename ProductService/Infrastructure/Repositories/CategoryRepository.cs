using Microsoft.EntityFrameworkCore;
using ProductService.Application.Interfaces;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Data;

namespace ProductService.Infrastructure.Repositories;

public sealed class CategoryRepository(ProductDbContext dbContext) : ICategoryRepository
{
    public IQueryable<Category> Query() => dbContext.Categories.AsNoTracking();

    public Task<Category?> GetTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Query().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Query().SingleOrDefaultAsync(x => x.Slug == slug, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => dbContext.Categories.AnyAsync(x => x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.Categories.IgnoreQueryFilters().AnyAsync(x => x.Name.ToLower() == name.ToLower() && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.Categories.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);

    public Task<bool> HasActiveProductsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Products.AnyAsync(x => x.CategoryId == id && x.IsActive, cancellationToken);

    public Task AddAsync(Category category, CancellationToken cancellationToken) => dbContext.Categories.AddAsync(category, cancellationToken).AsTask();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
