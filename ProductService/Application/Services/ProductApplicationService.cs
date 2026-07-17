using Microsoft.EntityFrameworkCore;
using ProductService.Application.DTOs.Products;
using ProductService.Application.Interfaces;
using ProductService.Application.Models;
using ProductService.Common.Exceptions;
using ProductService.Domain.Entities;
using ProductService.Domain.Enums;

namespace ProductService.Application.Services;

public sealed class ProductApplicationService(
    IProductRepository products,
    ICategoryRepository categories,
    IInventoryTransactionRepository inventoryTransactions,
    TimeProvider timeProvider,
    ILogger<ProductApplicationService> logger) : IProductService
{
    public async Task<PagedResponse<ProductSummaryResponse>> GetAsync(ProductQueryParameters query, CancellationToken cancellationToken)
    {
        IQueryable<Product> source = products.Query().Include(x => x.Category);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(x => x.Name.ToLower().Contains(search) || x.Sku.ToLower().Contains(search) || (x.Description != null && x.Description.ToLower().Contains(search)));
        }
        if (query.CategoryId.HasValue) source = source.Where(x => x.CategoryId == query.CategoryId);
        if (!string.IsNullOrWhiteSpace(query.CategorySlug)) source = source.Where(x => x.Category.Slug == query.CategorySlug.Trim().ToLowerInvariant());
        if (query.MinPrice.HasValue) source = source.Where(x => x.Price >= query.MinPrice);
        if (query.MaxPrice.HasValue) source = source.Where(x => x.Price <= query.MaxPrice);
        if (query.InStock.HasValue) source = query.InStock.Value ? source.Where(x => x.StockQuantity > 0) : source.Where(x => x.StockQuantity == 0);
        if (query.IsActive.HasValue) source = source.Where(x => x.IsActive == query.IsActive);

        var descending = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        source = query.SortBy.ToLowerInvariant() switch
        {
            "name" => descending ? source.OrderByDescending(x => x.Name) : source.OrderBy(x => x.Name),
            "price" => descending ? source.OrderByDescending(x => x.Price) : source.OrderBy(x => x.Price),
            "stock" => descending ? source.OrderByDescending(x => x.StockQuantity) : source.OrderBy(x => x.StockQuantity),
            "updatedat" => descending ? source.OrderByDescending(x => x.UpdatedAtUtc) : source.OrderBy(x => x.UpdatedAtUtc),
            "sku" => descending ? source.OrderByDescending(x => x.Sku) : source.OrderBy(x => x.Sku),
            _ => descending ? source.OrderByDescending(x => x.CreatedAtUtc) : source.OrderBy(x => x.CreatedAtUtc)
        };

        var total = await source.LongCountAsync(cancellationToken);
        var items = await source.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new ProductSummaryResponse(x.Id, x.Sku, x.Name, x.Price, x.StockQuantity, x.ReorderLevel, x.CategoryId, x.Category.Name, x.ImageUrl, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return PagedResponse<ProductSummaryResponse>.Create(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Map(await products.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException($"Product '{id}' was not found."));

    public async Task<ProductResponse> GetBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Map(await products.GetBySkuAsync(NormalizeSku(sku), cancellationToken) ?? throw new NotFoundException($"Product with SKU '{sku}' was not found."));

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var sku = NormalizeSku(request.Sku);
        if (await products.SkuExistsAsync(sku, null, cancellationToken)) throw new ConflictException($"SKU '{sku}' already exists.");
        if (!await categories.ExistsAsync(request.CategoryId, cancellationToken)) throw new ValidationException("The selected category does not exist.", new Dictionary<string, string[]> { ["categoryId"] = ["The selected category does not exist."] });

        var now = timeProvider.GetUtcNow();
        var product = new Product
        {
            Id = Guid.NewGuid(), Sku = sku, Name = request.Name.Trim(), Description = Clean(request.Description), Price = request.Price,
            StockQuantity = request.StockQuantity, ReorderLevel = request.ReorderLevel, CategoryId = request.CategoryId, Category = null!,
            ImageUrl = Clean(request.ImageUrl), IsActive = request.IsActive, CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid()
        };
        await products.AddAsync(product, cancellationToken);
        if (request.StockQuantity > 0)
        {
            await inventoryTransactions.AddAsync(new InventoryTransaction
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Product = product, OperationId = $"initial-stock:{product.Id:N}",
                OperationType = InventoryOperationType.Increase, Quantity = request.StockQuantity, StockBefore = 0,
                StockAfter = request.StockQuantity, Reason = "Initial stock", CreatedAtUtc = now
            }, cancellationToken);
        }
        await SaveAsync(cancellationToken);
        logger.LogInformation("Created product {ProductId} with SKU {Sku}", product.Id, product.Sku);
        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await products.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Product '{id}' was not found.");
        EnsureConcurrency(product.ConcurrencyToken, request.ConcurrencyToken);
        var sku = NormalizeSku(request.Sku);
        if (await products.SkuExistsAsync(sku, id, cancellationToken)) throw new ConflictException($"SKU '{sku}' already exists.");
        if (!await categories.ExistsAsync(request.CategoryId, cancellationToken)) throw new ValidationException("The selected category does not exist.", new Dictionary<string, string[]> { ["categoryId"] = ["The selected category does not exist."] });

        product.Sku = sku; product.Name = request.Name.Trim(); product.Description = Clean(request.Description); product.Price = request.Price;
        product.ReorderLevel = request.ReorderLevel; product.CategoryId = request.CategoryId; product.ImageUrl = Clean(request.ImageUrl); product.IsActive = request.IsActive;
        Touch(product);
        await SaveAsync(cancellationToken);
        logger.LogInformation("Updated product {ProductId}", id);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ProductResponse> UpdateStatusAsync(Guid id, UpdateProductStatusRequest request, CancellationToken cancellationToken)
    {
        var product = await products.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Product '{id}' was not found.");
        EnsureConcurrency(product.ConcurrencyToken, request.ConcurrencyToken);
        product.IsActive = request.IsActive;
        Touch(product);
        await SaveAsync(cancellationToken);
        logger.LogInformation("Changed status for product {ProductId} to {IsActive}", id, request.IsActive);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid concurrencyToken, CancellationToken cancellationToken)
    {
        var product = await products.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Product '{id}' was not found.");
        EnsureConcurrency(product.ConcurrencyToken, concurrencyToken);
        product.IsDeleted = true; product.IsActive = false; Touch(product);
        await SaveAsync(cancellationToken);
        logger.LogInformation("Soft-deleted product {ProductId}", id);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await products.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("The product was changed by another request. Refresh it and retry."); }
    }

    private void Touch(Product product) { product.UpdatedAtUtc = timeProvider.GetUtcNow(); product.ConcurrencyToken = Guid.NewGuid(); }
    private static void EnsureConcurrency(Guid actual, Guid supplied) { if (supplied == Guid.Empty || actual != supplied) throw new ConcurrencyConflictException("The supplied concurrency token is stale or missing."); }
    private static string NormalizeSku(string sku) => string.Join(' ', sku.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ProductResponse Map(Product x) => new(x.Id, x.Sku, x.Name, x.Description, x.Price, x.StockQuantity, x.ReorderLevel, x.CategoryId, x.Category.Name, x.Category.Slug, x.ImageUrl, x.IsActive, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyToken);
}
