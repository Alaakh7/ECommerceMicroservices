using OrderService.Application.DTOs.ExternalServices;

namespace OrderService.Application.Interfaces;

public interface ICartServiceClient
{
    Task<CartExternalResponse> GetCartAsync(Guid cartId, CancellationToken cancellationToken);
    Task<CartPrepareCheckoutExternalResponse> PrepareCheckoutAsync(Guid cartId, CartPrepareCheckoutExternalRequest request, CancellationToken cancellationToken);
    Task<CartCompleteCheckoutExternalResponse> CompleteCheckoutAsync(Guid cartId, CartCompleteCheckoutExternalRequest request, CancellationToken cancellationToken);
    Task<CartExternalResponse> CancelCheckoutAsync(Guid cartId, CartCancelCheckoutExternalRequest request, CancellationToken cancellationToken);
}

public interface ICustomerServiceClient
{
    Task<CustomerEligibilityExternalResponse> GetEligibilityAsync(Guid customerId, CancellationToken cancellationToken);
    Task<CustomerDetailsExternalResponse> GetCustomerDetailsAsync(Guid customerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAddressExternalResponse>> GetCustomerAddressesAsync(Guid customerId, CancellationToken cancellationToken);
    Task<CustomerAddressExternalResponse> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken);
}

public interface IProductServiceClient
{
    Task<StockAdjustmentExternalResponse> DecreaseStockAsync(Guid productId, StockAdjustmentExternalRequest request, CancellationToken cancellationToken);
    Task<StockAdjustmentExternalResponse> IncreaseStockAsync(Guid productId, StockAdjustmentExternalRequest request, CancellationToken cancellationToken);
    Task<ProductAvailabilityExternalResponse> CheckAvailabilityAsync(Guid productId, int quantity, CancellationToken cancellationToken);
}
