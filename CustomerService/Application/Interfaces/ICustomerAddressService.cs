using CustomerService.Application.DTOs.Addresses;

namespace CustomerService.Application.Interfaces;

public interface ICustomerAddressService
{
    Task<IReadOnlyList<CustomerAddressResponse>> GetAsync(Guid customerId, CancellationToken cancellationToken);
    Task<CustomerAddressResponse> GetByIdAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken);
    Task<CustomerAddressResponse> CreateAsync(Guid customerId, CreateCustomerAddressRequest request, CancellationToken cancellationToken);
    Task<CustomerAddressResponse> UpdateAsync(Guid customerId, Guid addressId, UpdateCustomerAddressRequest request, CancellationToken cancellationToken);
    Task<CustomerAddressResponse> SetDefaultAsync(Guid customerId, Guid addressId, SetDefaultAddressRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid customerId, Guid addressId, Guid concurrencyToken, CancellationToken cancellationToken);
}
