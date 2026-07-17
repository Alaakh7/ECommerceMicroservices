using CartService.Application.DTOs.ExternalServices;
using CartService.Application.Interfaces;
using CartService.Common.Exceptions;

namespace CartService.Infrastructure.ExternalServices;

public sealed class CustomerServiceClient(HttpClient httpClient) : ICustomerServiceClient
{
    public async Task<CustomerEligibilityExternalResponse> GetEligibilityAsync(Guid customerId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/customers/{customerId}/eligibility");
        var result = await ExternalClientSupport.SendAsync<CustomerEligibilityExternalResponse>(httpClient, request, "CustomerService", "Customer", customerId, cancellationToken);
        if (result.CustomerId != customerId) throw new InvalidExternalResponseException("CustomerService", "customerId did not match the request");
        return result;
    }
}
