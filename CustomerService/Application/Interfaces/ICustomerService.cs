using CustomerService.Application.DTOs.Customers;
using CustomerService.Application.Models;

namespace CustomerService.Application.Interfaces;

public interface ICustomerService
{
    Task<PagedResponse<CustomerSummaryResponse>> GetAsync(CustomerQueryParameters query, CancellationToken cancellationToken);
    Task<CustomerDetailsResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CustomerDetailsResponse> GetByCustomerNumberAsync(string customerNumber, CancellationToken cancellationToken);
    Task<CustomerDetailsResponse> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<CustomerDetailsResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken);
    Task<CustomerDetailsResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken);
    Task<CustomerDetailsResponse> UpdateStatusAsync(Guid id, UpdateCustomerStatusRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid concurrencyToken, CancellationToken cancellationToken);
    Task<CustomerEligibilityResponse> GetEligibilityAsync(Guid id, CancellationToken cancellationToken);
    Task<BatchCustomerEligibilityResponse> GetBatchEligibilityAsync(BatchCustomerEligibilityRequest request, CancellationToken cancellationToken);
}
