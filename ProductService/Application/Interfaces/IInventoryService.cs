using ProductService.Application.DTOs.Inventory;
using ProductService.Application.Models;
using ProductService.Domain.Enums;

namespace ProductService.Application.Interfaces;

public interface IInventoryService
{
    Task<ProductAvailabilityResponse> GetAvailabilityAsync(Guid productId, int quantity, CancellationToken cancellationToken);
    Task<BatchAvailabilityResponse> GetBatchAvailabilityAsync(BatchAvailabilityRequest request, CancellationToken cancellationToken);
    Task<StockAdjustmentResponse> AdjustAsync(Guid productId, InventoryOperationType operationType, AdjustStockRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<InventoryTransactionResponse>> GetTransactionsAsync(Guid productId, InventoryTransactionQueryParameters query, CancellationToken cancellationToken);
}
