using CartService.Application.DTOs.ExternalServices;

namespace CartService.Application.Interfaces;

public interface ICustomerServiceClient
{
    Task<CustomerEligibilityExternalResponse> GetEligibilityAsync(Guid customerId, CancellationToken cancellationToken);
}

public interface IProductServiceClient
{
    Task<ProductExternalResponse> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken);
    Task<ProductAvailabilityExternalResponse> CheckAvailabilityAsync(Guid productId, int quantity, CancellationToken cancellationToken);
    Task<BatchProductAvailabilityExternalResponse> CheckBatchAvailabilityAsync(BatchProductAvailabilityExternalRequest request, CancellationToken cancellationToken);
}
