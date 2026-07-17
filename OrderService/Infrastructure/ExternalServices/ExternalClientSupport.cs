using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrderService.Common.Exceptions;

namespace OrderService.Infrastructure.ExternalServices;

internal static class ExternalClientSupport
{
    public static async Task<T> SendAsync<T>(HttpClient http, HttpRequestMessage request, string service, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                try { return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken) ?? throw new InvalidExternalResponseException(service, "The response body was empty."); }
                catch (JsonException exception) { throw new InvalidExternalResponseException(service, "The response JSON was invalid.", exception); }
            }

            var detail = await ReadProblemDetailAsync(response, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) throw new ExternalResourceNotFoundException(service, ExtractId(request.RequestUri));
            if (response.StatusCode == HttpStatusCode.Conflict) throw new ExternalConflictException(service, detail);
            if ((int)response.StatusCode >= 500) throw new ExternalServiceUnavailableException(service);
            throw new InvalidExternalResponseException(service, $"HTTP {(int)response.StatusCode}: {detail}");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested) { throw new ExternalServiceTimeoutException(service, exception); }
        catch (HttpRequestException exception) { throw new ExternalServiceUnavailableException(service, exception); }
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemEnvelope>(cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(problem?.Detail) ? response.ReasonPhrase ?? "Request rejected." : problem.Detail;
        }
        catch (JsonException) { return response.ReasonPhrase ?? "Request rejected."; }
    }

    private static Guid ExtractId(Uri? uri) => uri is null ? Guid.Empty : uri.Segments.Select(x => Guid.TryParse(x.Trim('/'), out var id) ? id : Guid.Empty).FirstOrDefault(x => x != Guid.Empty);
    private sealed record ProblemEnvelope(string? Detail);
}
