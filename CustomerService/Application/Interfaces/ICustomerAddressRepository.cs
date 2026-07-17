using CustomerService.Domain.Entities;

namespace CustomerService.Application.Interfaces;

public interface ICustomerAddressRepository
{
    Task<CustomerAddress?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CustomerAddress?> GetTrackedAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAddress>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);
    Task<List<CustomerAddress>> GetTrackedByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken);
    Task<CustomerAddress?> GetDefaultShippingAsync(Guid customerId, CancellationToken cancellationToken);
    Task<CustomerAddress?> GetDefaultBillingAsync(Guid customerId, CancellationToken cancellationToken);
    Task AddAsync(CustomerAddress address, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
