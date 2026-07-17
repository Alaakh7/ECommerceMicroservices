using System.Net.Http.Json;
using OrderService.Application.DTOs.ExternalServices;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.ExternalServices;

public sealed class CartServiceClient(HttpClient http) : ICartServiceClient
{
    public Task<CartExternalResponse> GetCartAsync(Guid cartId, CancellationToken cancellationToken) =>
        SendAsync<CartExternalResponse>(HttpMethod.Get, $"api/v1/carts/{cartId}", null, cancellationToken);
    public Task<CartPrepareCheckoutExternalResponse> PrepareCheckoutAsync(Guid cartId, CartPrepareCheckoutExternalRequest request, CancellationToken cancellationToken) =>
        SendAsync<CartPrepareCheckoutExternalResponse>(HttpMethod.Post, $"api/v1/carts/{cartId}/checkout/prepare", request, cancellationToken);
    public Task<CartCompleteCheckoutExternalResponse> CompleteCheckoutAsync(Guid cartId, CartCompleteCheckoutExternalRequest request, CancellationToken cancellationToken) =>
        SendAsync<CartCompleteCheckoutExternalResponse>(HttpMethod.Post, $"api/v1/carts/{cartId}/checkout/complete", request, cancellationToken);
    public Task<CartExternalResponse> CancelCheckoutAsync(Guid cartId, CartCancelCheckoutExternalRequest request, CancellationToken cancellationToken) =>
        SendAsync<CartExternalResponse>(HttpMethod.Post, $"api/v1/carts/{cartId}/checkout/cancel", request, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, path);
        if (body is not null) message.Content = JsonContent.Create(body);
        return await ExternalClientSupport.SendAsync<T>(http, message, "CartService", cancellationToken);
    }
}
