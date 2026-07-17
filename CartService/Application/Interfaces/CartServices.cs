using CartService.Application.DTOs.CartItems;
using CartService.Application.DTOs.Carts;
using CartService.Application.DTOs.Checkout;
using CartService.Application.Models;

namespace CartService.Application.Interfaces;

public interface ICartService
{
    Task<CreateCartResponse> CreateOrGetAsync(CreateCartRequest request, CancellationToken cancellationToken);
    Task<CartResponse> GetByIdAsync(Guid cartId, CancellationToken cancellationToken);
    Task<CartResponse> GetActiveByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);
    Task<PagedResponse<CartSummaryResponse>> GetCustomerHistoryAsync(Guid customerId, CartQueryParameters query, CancellationToken cancellationToken);
    Task<CartResponse> AbandonAsync(Guid cartId, AbandonCartRequest request, CancellationToken cancellationToken);
}

public interface ICartItemService
{
    Task<CartResponse> AddAsync(Guid cartId, AddCartItemRequest request, CancellationToken cancellationToken);
    Task<CartResponse> UpdateQuantityAsync(Guid cartId, Guid productId, UpdateCartItemQuantityRequest request, CancellationToken cancellationToken);
    Task RemoveAsync(Guid cartId, Guid productId, Guid concurrencyToken, CancellationToken cancellationToken);
    Task ClearAsync(Guid cartId, Guid concurrencyToken, CancellationToken cancellationToken);
    Task<RefreshCartResponse> RefreshAsync(Guid cartId, RefreshCartRequest request, CancellationToken cancellationToken);
    Task<ValidateCartResponse> ValidateAsync(Guid cartId, ValidateCartRequest request, CancellationToken cancellationToken);
}

public interface ICartCheckoutService
{
    Task<PrepareCheckoutResponse> PrepareAsync(Guid cartId, PrepareCheckoutRequest request, CancellationToken cancellationToken);
    Task<CompleteCheckoutResponse> CompleteAsync(Guid cartId, CompleteCheckoutRequest request, CancellationToken cancellationToken);
    Task<CartResponse> CancelAsync(Guid cartId, CancelCheckoutRequest request, CancellationToken cancellationToken);
}

public interface ICartExpirationProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);
}
