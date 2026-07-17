using CustomerService.Domain.Entities;

namespace CustomerService.Application.Interfaces;

public interface ICustomerRepository
{
    IQueryable<Customer> Query();
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Customer?> GetDetailsByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<Customer?> GetByCustomerNumberAsync(string customerNumber, CancellationToken cancellationToken);
    Task<Customer?> GetTrackedAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> CustomerNumberExistsAsync(string customerNumber, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
