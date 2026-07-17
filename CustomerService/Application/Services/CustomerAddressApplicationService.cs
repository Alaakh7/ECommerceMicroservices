using CustomerService.Application.DTOs.Addresses;
using CustomerService.Application.Interfaces;
using CustomerService.Common.Exceptions;
using CustomerService.Domain.Entities;
using CustomerService.Domain.Enums;
using CustomerService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Application.Services;

public sealed class CustomerAddressApplicationService(
    ICustomerRepository customers,
    ICustomerAddressRepository addresses,
    CustomerDbContext db,
    TimeProvider timeProvider,
    ILogger<CustomerAddressApplicationService> logger) : ICustomerAddressService
{
    public async Task<IReadOnlyList<CustomerAddressResponse>> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await EnsureCustomerExistsAsync(customerId, cancellationToken);
        return (await addresses.GetByCustomerIdAsync(customerId, cancellationToken)).Select(AddressRules.Map).ToList();
    }

    public async Task<CustomerAddressResponse> GetByIdAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        await EnsureCustomerExistsAsync(customerId, cancellationToken);
        var address = await addresses.GetByIdAsync(addressId, cancellationToken) ?? throw new NotFoundException($"Address '{addressId}' was not found.");
        EnsureOwnership(address, customerId);
        return AddressRules.Map(address);
    }

    public async Task<CustomerAddressResponse> CreateAsync(Guid customerId, CreateCustomerAddressRequest request, CancellationToken cancellationToken)
    {
        AddressRules.Validate(request);
        CustomerAddress? created = null;
        await ExecuteTransactionAsync(async () =>
        {
            var customer = await customers.GetTrackedAsync(customerId, cancellationToken) ?? throw new NotFoundException($"Customer '{customerId}' was not found.");
            if (customer.Status == CustomerStatus.Deactivated) throw new InvalidCustomerStateException("Addresses cannot be added to a deactivated customer.");
            var existing = await addresses.GetTrackedByCustomerIdAsync(customerId, cancellationToken);
            var first = existing.Count == 0;
            created = AddressRules.Create(customerId, customer, request, timeProvider.GetUtcNow(), first);
            if (created.IsDefaultShipping) ClearDefault(existing, shipping: true, billing: false);
            if (created.IsDefaultBilling) ClearDefault(existing, shipping: false, billing: true);
            await addresses.AddAsync(created, cancellationToken);
            await addresses.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Added address {AddressId} to customer {CustomerId}", created!.Id, customerId);
        return await GetByIdAsync(customerId, created.Id, cancellationToken);
    }

    public async Task<CustomerAddressResponse> UpdateAsync(Guid customerId, Guid addressId, UpdateCustomerAddressRequest request, CancellationToken cancellationToken)
    {
        var (_, address) = await LoadOwnedTrackedAsync(customerId, addressId, cancellationToken);
        CustomerApplicationService.EnsureConcurrency(address.ConcurrencyToken, request.ConcurrencyToken, "Address data was modified by another request.");
        AddressRules.Apply(address, request);
        Touch(address);
        await SaveAddressAsync(cancellationToken);
        logger.LogInformation("Updated address {AddressId} for customer {CustomerId}", addressId, customerId);
        return await GetByIdAsync(customerId, addressId, cancellationToken);
    }

    public async Task<CustomerAddressResponse> SetDefaultAsync(Guid customerId, Guid addressId, SetDefaultAddressRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.DefaultFor)) throw CustomerApplicationService.Validation("defaultFor", "The default address usage is invalid.");
        await ExecuteTransactionAsync(async () =>
        {
            var (_, target) = await LoadOwnedTrackedAsync(customerId, addressId, cancellationToken);
            CustomerApplicationService.EnsureConcurrency(target.ConcurrencyToken, request.ConcurrencyToken, "Address data was modified by another request.");
            var all = await addresses.GetTrackedByCustomerIdAsync(customerId, cancellationToken);
            var shipping = request.DefaultFor is AddressDefaultUsage.Shipping or AddressDefaultUsage.ShippingAndBilling;
            var billing = request.DefaultFor is AddressDefaultUsage.Billing or AddressDefaultUsage.ShippingAndBilling;
            ClearDefault(all.Where(x => x.Id != target.Id), shipping, billing);
            if (shipping) target.IsDefaultShipping = true;
            if (billing) target.IsDefaultBilling = true;
            Touch(target);
            await SaveAddressAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Set address {AddressId} as {DefaultFor} for customer {CustomerId}", addressId, request.DefaultFor, customerId);
        return await GetByIdAsync(customerId, addressId, cancellationToken);
    }

    public async Task DeleteAsync(Guid customerId, Guid addressId, Guid concurrencyToken, CancellationToken cancellationToken)
    {
        var removedDefaults = false;
        await ExecuteTransactionAsync(async () =>
        {
            var (_, address) = await LoadOwnedTrackedAsync(customerId, addressId, cancellationToken);
            CustomerApplicationService.EnsureConcurrency(address.ConcurrencyToken, concurrencyToken, "Address data was modified by another request.");
            removedDefaults = address.IsDefaultShipping || address.IsDefaultBilling;
            address.IsDefaultShipping = false;
            address.IsDefaultBilling = false;
            address.IsDeleted = true;
            address.DeletedAtUtc = timeProvider.GetUtcNow();
            Touch(address);
            await SaveAddressAsync(cancellationToken);
        }, cancellationToken);
        logger.LogInformation("Soft-deleted address {AddressId} for customer {CustomerId}; removed default assignment: {RemovedDefaults}", addressId, customerId, removedDefaults);
    }

    private async Task<(Customer Customer, CustomerAddress Address)> LoadOwnedTrackedAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var customer = await customers.GetTrackedAsync(customerId, cancellationToken) ?? throw new NotFoundException($"Customer '{customerId}' was not found.");
        var address = await addresses.GetTrackedAsync(addressId, cancellationToken) ?? throw new NotFoundException($"Address '{addressId}' was not found.");
        EnsureOwnership(address, customerId);
        return (customer, address);
    }

    private async Task EnsureCustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (await customers.GetByIdAsync(customerId, cancellationToken) is null) throw new NotFoundException($"Customer '{customerId}' was not found.");
    }

    private static void EnsureOwnership(CustomerAddress address, Guid customerId)
    {
        if (address.CustomerId != customerId) throw new AddressOwnershipException("The address does not belong to the customer in the route.");
    }

    private void ClearDefault(IEnumerable<CustomerAddress> values, bool shipping, bool billing)
    {
        foreach (var address in values)
        {
            var changed = false;
            if (shipping && address.IsDefaultShipping) { address.IsDefaultShipping = false; changed = true; }
            if (billing && address.IsDefaultBilling) { address.IsDefaultBilling = false; changed = true; }
            if (changed) Touch(address);
        }
    }

    private void Touch(CustomerAddress address)
    {
        address.UpdatedAtUtc = timeProvider.GetUtcNow();
        address.ConcurrencyToken = Guid.NewGuid();
    }

    private async Task SaveAddressAsync(CancellationToken cancellationToken)
    {
        try { await addresses.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("Address data was modified by another request."); }
        catch (DbUpdateException) { throw new ConflictException("The address operation conflicted with existing data."); }
    }

    private async Task ExecuteTransactionAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await operation();
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
