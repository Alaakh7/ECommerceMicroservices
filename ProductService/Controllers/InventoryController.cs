using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs.Inventory;
using ProductService.Application.Interfaces;
using ProductService.Application.Models;
using ProductService.Domain.Enums;

namespace ProductService.Controllers;

[ApiController]
[Route("api/v1/products")]
[Produces("application/json")]
public sealed class InventoryController(IInventoryService inventory) : ControllerBase
{
    [HttpGet("{id:guid}/availability")]
    public Task<ProductAvailabilityResponse> GetAvailability(Guid id, [FromQuery] int quantity, CancellationToken cancellationToken) => inventory.GetAvailabilityAsync(id, quantity, cancellationToken);

    [HttpPost("availability/batch")]
    public Task<BatchAvailabilityResponse> GetBatchAvailability(BatchAvailabilityRequest request, CancellationToken cancellationToken) => inventory.GetBatchAvailabilityAsync(request, cancellationToken);

    [HttpPost("{id:guid}/stock/increase")]
    public Task<StockAdjustmentResponse> Increase(Guid id, AdjustStockRequest request, CancellationToken cancellationToken) => inventory.AdjustAsync(id, InventoryOperationType.Increase, request, cancellationToken);

    [HttpPost("{id:guid}/stock/decrease")]
    public Task<StockAdjustmentResponse> Decrease(Guid id, AdjustStockRequest request, CancellationToken cancellationToken) => inventory.AdjustAsync(id, InventoryOperationType.Decrease, request, cancellationToken);

    [HttpGet("{id:guid}/stock/transactions")]
    public Task<PagedResponse<InventoryTransactionResponse>> GetTransactions(Guid id, [FromQuery] InventoryTransactionQueryParameters query, CancellationToken cancellationToken) => inventory.GetTransactionsAsync(id, query, cancellationToken);
}
