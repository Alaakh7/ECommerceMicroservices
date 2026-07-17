namespace CustomerService.Common.Exceptions;

public class NotFoundException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);
public sealed class DuplicateEmailException(string message) : ConflictException(message);
public sealed class ConcurrencyConflictException(string message) : ConflictException(message);
public sealed class InvalidCustomerStateException(string message) : ConflictException(message);
public sealed class AddressOwnershipException(string message) : ConflictException(message);

public sealed class ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}
