namespace ProductService.Common.Exceptions;

public abstract class ApiException(string message) : Exception(message);

public sealed class NotFoundException(string message) : ApiException(message);
public class ConflictException(string message) : ApiException(message);
public sealed class ConcurrencyConflictException(string message) : ApiException(message);

public sealed class InsufficientStockException(Guid productId, int requestedQuantity, int availableQuantity)
    : ConflictException($"Insufficient stock for product '{productId}'.")
{
    public Guid ProductId { get; } = productId;
    public int RequestedQuantity { get; } = requestedQuantity;
    public int AvailableQuantity { get; } = availableQuantity;
}

public sealed class ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null) : ApiException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}
