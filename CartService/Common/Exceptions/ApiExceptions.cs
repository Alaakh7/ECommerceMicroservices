using CartService.Application.DTOs.CartItems;

namespace CartService.Common.Exceptions;

public abstract class ApiException(string message, Exception? innerException = null) : Exception(message, innerException);
public class NotFoundException(string message) : ApiException(message);
public class ConflictException(string message) : ApiException(message);
public sealed class ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null) : ApiException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}

public sealed class CartNotActiveException(string message) : ConflictException(message);
public sealed class CartEmptyException(string message) : ConflictException(message);
public sealed class CartLockedException(string message) : ConflictException(message);
public sealed class DuplicateActiveCartException(string message) : ConflictException(message);
public sealed class ProductUnavailableException(string message) : ConflictException(message);
public sealed class InsufficientStockException(Guid productId, int requestedQuantity, int availableQuantity)
    : ConflictException($"Insufficient stock for product '{productId}'.")
{
    public Guid ProductId { get; } = productId;
    public int RequestedQuantity { get; } = requestedQuantity;
    public int AvailableQuantity { get; } = availableQuantity;
}
public sealed class CustomerNotEligibleException(string message) : ConflictException(message);
public sealed class CheckoutExpiredException(string message) : ConflictException(message);
public sealed class InvalidCheckoutTokenException(string message) : ConflictException(message);
public sealed class ConcurrencyConflictException(string message) : ConflictException(message);
public sealed class PriceChangedException(IReadOnlyList<CartPriceChangeResponse> priceChanges)
    : ConflictException("One or more product prices changed. Accept the changes and retry checkout.")
{
    public IReadOnlyList<CartPriceChangeResponse> PriceChanges { get; } = priceChanges;
}

public sealed class ExternalResourceNotFoundException(string resource, Guid id) : NotFoundException($"{resource} '{id}' was not found.");
public sealed class ExternalServiceUnavailableException(string service, Exception? innerException = null)
    : ApiException($"{service} is unavailable.", innerException);
public sealed class ExternalServiceTimeoutException(string service, Exception? innerException = null)
    : ApiException($"{service} timed out.", innerException);
public sealed class InvalidExternalResponseException(string service, string detail)
    : ApiException($"{service} returned an invalid response: {detail}");
