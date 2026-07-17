using CustomerService.Application.Interfaces;
using CustomerService.Domain.Entities;
using CustomerService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Infrastructure.Repositories;

public sealed class CustomerAddressRepository(CustomerDbContext db) : ICustomerAddressRepository
{
    public Task<CustomerAddress?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.CustomerAddresses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<CustomerAddress?> GetTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        db.CustomerAddresses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task<IReadOnlyList<CustomerAddress>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        await db.CustomerAddresses.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.IsDefaultShipping).ThenByDescending(x => x.IsDefaultBilling).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    public Task<List<CustomerAddress>> GetTrackedByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.CustomerAddresses.Where(x => x.CustomerId == customerId).ToListAsync(cancellationToken);
    public Task<CustomerAddress?> GetDefaultShippingAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.CustomerAddresses.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customerId && x.IsDefaultShipping, cancellationToken);
    public Task<CustomerAddress?> GetDefaultBillingAsync(Guid customerId, CancellationToken cancellationToken) =>
        db.CustomerAddresses.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == customerId && x.IsDefaultBilling, cancellationToken);
    public Task AddAsync(CustomerAddress address, CancellationToken cancellationToken) => db.CustomerAddresses.AddAsync(address, cancellationToken).AsTask();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
