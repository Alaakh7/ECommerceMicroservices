using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CartService.Common.Exceptions;

namespace CartService.Infrastructure.ExternalServices;

internal static class ExternalClientSupport
{
    public static async Task<T> SendAsync<T>(HttpClient client, HttpRequestMessage request, string service, string resource, Guid resourceId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new ExternalResourceNotFoundException(resource, resourceId);
            if ((int)response.StatusCode >= 500) throw new ExternalServiceUnavailableException(service);
            if (!response.IsSuccessStatusCode)
                throw new InvalidExternalResponseException(service, $"unexpected HTTP status {(int)response.StatusCode}");
            try
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                    ?? throw new InvalidExternalResponseException(service, "empty JSON body");
            }
            catch (JsonException exception)
            {
                throw new InvalidExternalResponseException(service, $"malformed JSON ({exception.Message})");
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceTimeoutException(service, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalServiceUnavailableException(service, exception);
        }
    }
}
