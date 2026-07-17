using Microsoft.EntityFrameworkCore;
using ProductService.Application.DTOs.Categories;
using ProductService.Application.Interfaces;
using ProductService.Application.Models;
using ProductService.Common.Exceptions;
using ProductService.Domain.Entities;

namespace ProductService.Application.Services;

public sealed class CategoryApplicationService(
    ICategoryRepository categories,
    TimeProvider timeProvider,
    ILogger<CategoryApplicationService> logger) : ICategoryService
{
    public async Task<PagedResponse<CategorySummaryResponse>> GetAsync(CategoryQueryParameters query, CancellationToken cancellationToken)
    {
        var source = categories.Query();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(x => x.Name.ToLower().Contains(search) || x.Slug.Contains(search) || (x.Description != null && x.Description.ToLower().Contains(search)));
        }
        if (query.IsActive.HasValue) source = source.Where(x => x.IsActive == query.IsActive);
        var desc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        source = query.SortBy.ToLowerInvariant() switch
        {
            "slug" => desc ? source.OrderByDescending(x => x.Slug) : source.OrderBy(x => x.Slug),
            "createdat" => desc ? source.OrderByDescending(x => x.CreatedAtUtc) : source.OrderBy(x => x.CreatedAtUtc),
            "updatedat" => desc ? source.OrderByDescending(x => x.UpdatedAtUtc) : source.OrderBy(x => x.UpdatedAtUtc),
            _ => desc ? source.OrderByDescending(x => x.Name) : source.OrderBy(x => x.Name)
        };
        var total = await source.LongCountAsync(cancellationToken);
        var items = await source.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CategorySummaryResponse(x.Id, x.Name, x.Slug, x.IsActive)).ToListAsync(cancellationToken);
        return PagedResponse<CategorySummaryResponse>.Create(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<CategoryResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await categories.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException($"Category '{id}' was not found."));

    public async Task<CategoryResponse> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Map(await categories.GetBySlugAsync(NormalizeSlug(slug), cancellationToken) ?? throw new NotFoundException($"Category with slug '{slug}' was not found."));

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim(); var slug = NormalizeSlug(request.Slug);
        await EnsureUniqueAsync(name, slug, null, cancellationToken);
        var category = new Category
        {
            Id = Guid.NewGuid(), Name = name, Slug = slug, Description = Clean(request.Description), IsActive = request.IsActive,
            CreatedAtUtc = timeProvider.GetUtcNow(), ConcurrencyToken = Guid.NewGuid()
        };
        await categories.AddAsync(category, cancellationToken);
        await SaveAsync(cancellationToken);
        logger.LogInformation("Created category {CategoryId}", category.Id);
        return Map(category);
    }

    public async Task<CategoryResponse> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categories.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Category '{id}' was not found.");
        EnsureConcurrency(category.ConcurrencyToken, request.ConcurrencyToken);
        var name = request.Name.Trim(); var slug = NormalizeSlug(request.Slug);
        await EnsureUniqueAsync(name, slug, id, cancellationToken);
        category.Name = name; category.Slug = slug; category.Description = Clean(request.Description); category.IsActive = request.IsActive;
        Touch(category); await SaveAsync(cancellationToken);
        logger.LogInformation("Updated category {CategoryId}", id);
        return Map(category);
    }

    public async Task<CategoryResponse> UpdateStatusAsync(Guid id, bool isActive, Guid concurrencyToken, CancellationToken cancellationToken)
    {
        var category = await categories.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Category '{id}' was not found.");
        EnsureConcurrency(category.ConcurrencyToken, concurrencyToken);
        category.IsActive = isActive; Touch(category); await SaveAsync(cancellationToken);
        return Map(category);
    }

    public async Task DeleteAsync(Guid id, Guid concurrencyToken, CancellationToken cancellationToken)
    {
        var category = await categories.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Category '{id}' was not found.");
        EnsureConcurrency(category.ConcurrencyToken, concurrencyToken);
        if (await categories.HasActiveProductsAsync(id, cancellationToken)) throw new ConflictException("A category containing active products cannot be deleted.");
        category.IsDeleted = true; category.IsActive = false; Touch(category); await SaveAsync(cancellationToken);
        logger.LogInformation("Soft-deleted category {CategoryId}", id);
    }

    private async Task EnsureUniqueAsync(string name, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await categories.NameExistsAsync(name, exceptId, cancellationToken)) throw new ConflictException($"Category name '{name}' already exists.");
        if (await categories.SlugExistsAsync(slug, exceptId, cancellationToken)) throw new ConflictException($"Category slug '{slug}' already exists.");
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await categories.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("The category was changed by another request. Refresh it and retry."); }
    }

    private void Touch(Category category) { category.UpdatedAtUtc = timeProvider.GetUtcNow(); category.ConcurrencyToken = Guid.NewGuid(); }
    private static void EnsureConcurrency(Guid actual, Guid supplied) { if (supplied == Guid.Empty || actual != supplied) throw new ConcurrencyConflictException("The supplied concurrency token is stale or missing."); }
    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static CategoryResponse Map(Category x) => new(x.Id, x.Name, x.Slug, x.Description, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyToken);
}
