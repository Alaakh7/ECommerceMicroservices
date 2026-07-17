using CustomerService.Application.Interfaces;
using CustomerService.Domain.Entities;
using CustomerService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Infrastructure.Repositories;

public sealed class CustomerRepository(CustomerDbContext db) : ICustomerRepository
{
    public IQueryable<Customer> Query() => db.Customers.AsNoTracking();
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Customer?> GetDetailsByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().Include(x => x.Addresses).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().Include(x => x.Addresses).SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
    public Task<Customer?> GetByCustomerNumberAsync(string customerNumber, CancellationToken cancellationToken) =>
        db.Customers.AsNoTracking().Include(x => x.Addresses).SingleOrDefaultAsync(x => x.CustomerNumber.ToUpper() == customerNumber.ToUpper(), cancellationToken);
    public Task<Customer?> GetTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        db.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(x => x.NormalizedEmail == normalizedEmail && (!exceptId.HasValue || x.Id != exceptId), cancellationToken);
    public Task<bool> CustomerNumberExistsAsync(string customerNumber, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(x => x.CustomerNumber == customerNumber, cancellationToken);
    public Task AddAsync(Customer customer, CancellationToken cancellationToken) => db.Customers.AddAsync(customer, cancellationToken).AsTask();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
