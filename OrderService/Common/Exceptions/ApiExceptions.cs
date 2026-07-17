namespace OrderService.Common.Exceptions;

public abstract class ApiException(string message, Exception? inner = null) : Exception(message, inner);
public class ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null) : ApiException(message) { public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>(); }
public class NotFoundException(string message) : ApiException(message);
public class ConflictException(string message) : ApiException(message);
public sealed class DuplicateOrderException(string message) : ConflictException(message);
public sealed class InvalidOrderStateException(string message) : ConflictException(message);
public sealed class OrderAlreadyExistsException(string message) : ConflictException(message);
public sealed class ConcurrencyConflictException(string message) : ConflictException(message);
public sealed class CustomerNotEligibleException(string message) : ConflictException(message);
public sealed class InvalidCustomerAddressException(string message) : ConflictException(message);
public sealed class CartNotReadyException(string message) : ConflictException(message);
public sealed class CartCheckoutExpiredException(string message) : ConflictException(message);
public sealed class InsufficientStockException(Guid productId, string message) : ConflictException(message) { public Guid ProductId { get; } = productId; }
public sealed class InventoryCompensationException(string message) : ApiException(message);
public sealed class ExternalServiceUnavailableException(string service, Exception? inner = null) : ApiException($"{service} is unavailable.", inner);
public sealed class ExternalServiceTimeoutException(string service, Exception? inner = null) : ApiException($"{service} timed out.", inner);
public sealed class InvalidExternalResponseException(string service, string detail, Exception? inner = null) : ApiException($"{service} returned an invalid response: {detail}", inner);
public sealed class OrderProcessingException(string message) : ApiException(message);
public sealed class ExternalResourceNotFoundException(string resource, Guid id) : NotFoundException($"{resource} '{id}' was not found.");
public sealed class ExternalConflictException(string service, string message) : ConflictException($"{service} rejected the operation: {message}");
