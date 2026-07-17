using System.Data;
using Microsoft.EntityFrameworkCore;
using ProductService.Application.DTOs.Inventory;
using ProductService.Application.Interfaces;
using ProductService.Application.Models;
using ProductService.Common.Exceptions;
using ProductService.Domain.Entities;
using ProductService.Domain.Enums;
using ProductService.Infrastructure.Data;

namespace ProductService.Application.Services;

public sealed class InventoryApplicationService(
    ProductDbContext dbContext,
    IProductRepository products,
    IInventoryTransactionRepository transactions,
    TimeProvider timeProvider,
    ILogger<InventoryApplicationService> logger) : IInventoryService
{
    public async Task<ProductAvailabilityResponse> GetAvailabilityAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0) throw new ValidationException("Quantity must be greater than zero.", new Dictionary<string, string[]> { ["quantity"] = ["Quantity must be greater than zero."] });
        var product = await products.GetByIdAsync(productId, cancellationToken) ?? throw new NotFoundException($"Product '{productId}' was not found.");
        return new(product.Id, quantity, product.StockQuantity, product.IsActive && product.StockQuantity >= quantity, product.IsActive);
    }

    public async Task<BatchAvailabilityResponse> GetBatchAvailabilityAsync(BatchAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Items.Select(x => x.ProductId).ToArray();
        var found = await products.Query().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.StockQuantity, x.IsActive }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var items = request.Items.Select(item =>
        {
            if (!found.TryGetValue(item.ProductId, out var product)) return new BatchAvailabilityItemResponse(item.ProductId, item.Quantity, 0, false, false, false);
            return new BatchAvailabilityItemResponse(item.ProductId, item.Quantity, product.StockQuantity, product.IsActive && product.StockQuantity >= item.Quantity, product.IsActive, true);
        }).ToList();
        return new(items, items.All(x => x.IsAvailable));
    }

    public async Task<StockAdjustmentResponse> AdjustAsync(Guid productId, InventoryOperationType operationType, AdjustStockRequest request, CancellationToken cancellationToken)
    {
        var operationId = request.OperationId.Trim();
        var existing = await transactions.GetByOperationIdAsync(operationId, cancellationToken);
        if (existing is not null) return ExistingOrConflict(existing, productId, operationType, request);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            existing = await transactions.GetByOperationIdAsync(operationId, cancellationToken);
            if (existing is not null) return ExistingOrConflict(existing, productId, operationType, request);

            var product = await products.GetTrackedAsync(productId, cancellationToken) ?? throw new NotFoundException($"Product '{productId}' was not found.");
            var before = product.StockQuantity;
            if (operationType == InventoryOperationType.Decrease && before < request.Quantity) throw new InsufficientStockException(productId, request.Quantity, before);
            product.StockQuantity = operationType == InventoryOperationType.Increase ? checked(before + request.Quantity) : before - request.Quantity;
            product.UpdatedAtUtc = timeProvider.GetUtcNow(); product.ConcurrencyToken = Guid.NewGuid();
            var entity = new InventoryTransaction
            {
                Id = Guid.NewGuid(), ProductId = productId, Product = product, OperationId = operationId, OperationType = operationType,
                Quantity = request.Quantity, StockBefore = before, StockAfter = product.StockQuantity, Reason = Clean(request.Reason), CreatedAtUtc = timeProvider.GetUtcNow()
            };
            await transactions.AddAsync(entity, cancellationToken);
            await products.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Adjusted stock for {ProductId} using operation {OperationId}; {StockBefore} -> {StockAfter}", productId, operationId, before, product.StockQuantity);
            return Map(entity, false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException("The product stock changed concurrently. Retry with the same operationId.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            existing = await transactions.GetByOperationIdAsync(operationId, cancellationToken);
            if (existing is not null) return ExistingOrConflict(existing, productId, operationType, request);
            throw;
        }
    }

    public async Task<PagedResponse<InventoryTransactionResponse>> GetTransactionsAsync(Guid productId, InventoryTransactionQueryParameters query, CancellationToken cancellationToken)
    {
        if (!await products.Query().AnyAsync(x => x.Id == productId, cancellationToken)) throw new NotFoundException($"Product '{productId}' was not found.");
        var source = transactions.Query().Where(x => x.ProductId == productId);
        if (query.OperationType.HasValue) source = source.Where(x => x.OperationType == query.OperationType);
        if (query.FromDateUtc.HasValue) source = source.Where(x => x.CreatedAtUtc >= query.FromDateUtc);
        if (query.ToDateUtc.HasValue) source = source.Where(x => x.CreatedAtUtc <= query.ToDateUtc);
        source = source.OrderByDescending(x => x.CreatedAtUtc);
        var total = await source.LongCountAsync(cancellationToken);
        var items = await source.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new InventoryTransactionResponse(x.Id, x.ProductId, x.OperationId, x.OperationType, x.Quantity, x.StockBefore, x.StockAfter, x.Reason, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return PagedResponse<InventoryTransactionResponse>.Create(items, query.PageNumber, query.PageSize, total);
    }

    private static StockAdjustmentResponse ExistingOrConflict(InventoryTransaction existing, Guid productId, InventoryOperationType type, AdjustStockRequest request)
    {
        if (existing.ProductId != productId || existing.OperationType != type || existing.Quantity != request.Quantity || !string.Equals(existing.Reason, Clean(request.Reason), StringComparison.Ordinal))
            throw new ConflictException($"OperationId '{existing.OperationId}' has already been used with different data.");
        return Map(existing, true);
    }

    private static StockAdjustmentResponse Map(InventoryTransaction x, bool alreadyProcessed) => new(x.ProductId, x.OperationId, x.OperationType, x.Quantity, x.StockBefore, x.StockAfter, x.CreatedAtUtc, alreadyProcessed);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
