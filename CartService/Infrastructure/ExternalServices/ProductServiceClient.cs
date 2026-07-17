using System.Net.Http.Json;
using CartService.Application.DTOs.ExternalServices;
using CartService.Application.Interfaces;
using CartService.Common.Exceptions;

namespace CartService.Infrastructure.ExternalServices;

public sealed class ProductServiceClient(HttpClient httpClient) : IProductServiceClient
{
    public async Task<ProductExternalResponse> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/products/{productId}");
        var result = await ExternalClientSupport.SendAsync<ProductExternalResponse>(httpClient, request, "ProductService", "Product", productId, cancellationToken);
        if (result.Id != productId || string.IsNullOrWhiteSpace(result.Sku) || string.IsNullOrWhiteSpace(result.Name) || result.Price <= 0)
            throw new InvalidExternalResponseException("ProductService", "required product fields were missing or invalid");
        return result;
    }

    public async Task<ProductAvailabilityExternalResponse> CheckAvailabilityAsync(Guid productId, int quantity, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/products/{productId}/availability?quantity={quantity}");
        var result = await ExternalClientSupport.SendAsync<ProductAvailabilityExternalResponse>(httpClient, request, "ProductService", "Product", productId, cancellationToken);
        if (result.ProductId != productId || result.RequestedQuantity != quantity || result.AvailableQuantity < 0)
            throw new InvalidExternalResponseException("ProductService", "availability fields did not match the request");
        return result;
    }

    public async Task<BatchProductAvailabilityExternalResponse> CheckBatchAvailabilityAsync(BatchProductAvailabilityExternalRequest body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/products/availability/batch") { Content = JsonContent.Create(body) };
        var result = await ExternalClientSupport.SendAsync<BatchProductAvailabilityExternalResponse>(httpClient, request, "ProductService", "Product", Guid.Empty, cancellationToken);
        if (result.Items is null || result.Items.Count != body.Items.Count)
            throw new InvalidExternalResponseException("ProductService", "batch response did not contain every requested product");
        return result;
    }
}
