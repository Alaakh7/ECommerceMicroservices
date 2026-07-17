using System.Net.Http.Json;
using OrderService.Application.DTOs.ExternalServices;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.ExternalServices;

public sealed class ProductServiceClient(HttpClient http) : IProductServiceClient
{
    public Task<StockAdjustmentExternalResponse> DecreaseStockAsync(Guid productId, StockAdjustmentExternalRequest request, CancellationToken cancellationToken) => AdjustAsync(productId, "decrease", request, cancellationToken);
    public Task<StockAdjustmentExternalResponse> IncreaseStockAsync(Guid productId, StockAdjustmentExternalRequest request, CancellationToken cancellationToken) => AdjustAsync(productId, "increase", request, cancellationToken);
    public async Task<ProductAvailabilityExternalResponse> CheckAvailabilityAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"api/v1/products/{productId}/availability?quantity={quantity}");
        return await ExternalClientSupport.SendAsync<ProductAvailabilityExternalResponse>(http, message, "ProductService", cancellationToken);
    }
    private async Task<StockAdjustmentExternalResponse> AdjustAsync(Guid productId, string action, StockAdjustmentExternalRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"api/v1/products/{productId}/stock/{action}") { Content = JsonContent.Create(request) };
        return await ExternalClientSupport.SendAsync<StockAdjustmentExternalResponse>(http, message, "ProductService", cancellationToken);
    }
}
