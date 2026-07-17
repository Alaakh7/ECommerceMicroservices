using OrderService.Application.DTOs.ExternalServices;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.ExternalServices;

public sealed class CustomerServiceClient(HttpClient http) : ICustomerServiceClient
{
    public Task<CustomerEligibilityExternalResponse> GetEligibilityAsync(Guid customerId, CancellationToken cancellationToken) => GetAsync<CustomerEligibilityExternalResponse>($"api/v1/customers/{customerId}/eligibility", cancellationToken);
    public Task<CustomerDetailsExternalResponse> GetCustomerDetailsAsync(Guid customerId, CancellationToken cancellationToken) => GetAsync<CustomerDetailsExternalResponse>($"api/v1/customers/{customerId}", cancellationToken);
    public Task<IReadOnlyList<CustomerAddressExternalResponse>> GetCustomerAddressesAsync(Guid customerId, CancellationToken cancellationToken) => GetAsync<IReadOnlyList<CustomerAddressExternalResponse>>($"api/v1/customers/{customerId}/addresses", cancellationToken);
    public Task<CustomerAddressExternalResponse> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken) => GetAsync<CustomerAddressExternalResponse>($"api/v1/customers/{customerId}/addresses/{addressId}", cancellationToken);
    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        return await ExternalClientSupport.SendAsync<T>(http, message, "CustomerService", cancellationToken);
    }
}
