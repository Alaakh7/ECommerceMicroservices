using System.ComponentModel.DataAnnotations;
using CustomerService.Application.DTOs.Addresses;
using CustomerService.Application.Validation;
using CustomerService.Domain.Enums;

namespace CustomerService.Application.DTOs.Customers;

public sealed class CreateCustomerRequest
{
    [Required, StringLength(100)] public string FirstName { get; init; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; init; } = string.Empty;
    [Required, StringLength(320), EmailAddress] public string Email { get; init; } = string.Empty;
    [StringLength(30)] public string? PhoneNumber { get; init; }
    public CreateCustomerAddressRequest? InitialAddress { get; init; }
}

public sealed class UpdateCustomerRequest
{
    [Required, StringLength(100)] public string FirstName { get; init; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; init; } = string.Empty;
    [Required, StringLength(320), EmailAddress] public string Email { get; init; } = string.Empty;
    [StringLength(30)] public string? PhoneNumber { get; init; }
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed class UpdateCustomerStatusRequest
{
    [EnumDataType(typeof(CustomerStatus))] public CustomerStatus Status { get; init; }
    [StringLength(500)] public string? Reason { get; init; }
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed record CustomerResponse(
    Guid Id,
    string CustomerNumber,
    string FullName,
    string Email,
    string? PhoneNumber,
    CustomerStatus Status,
    DateTimeOffset CreatedAtUtc,
    Guid ConcurrencyToken);

public sealed record CustomerSummaryResponse(
    Guid Id,
    string CustomerNumber,
    string FullName,
    string Email,
    string? PhoneNumber,
    CustomerStatus Status,
    int AddressCount,
    DateTimeOffset CreatedAtUtc);

public sealed record CustomerDetailsResponse(
    Guid Id,
    string CustomerNumber,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? PhoneNumber,
    CustomerStatus Status,
    bool CanCreateCart,
    bool CanPlaceOrder,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    Guid ConcurrencyToken,
    IReadOnlyList<CustomerAddressResponse> Addresses);

public sealed class CustomerQueryParameters : IValidatableObject
{
    [Range(1, int.MaxValue)] public int PageNumber { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    [EnumDataType(typeof(CustomerStatus))] public CustomerStatus? Status { get; init; }
    public bool? HasAddresses { get; init; }
    public DateTimeOffset? CreatedFromUtc { get; init; }
    public DateTimeOffset? CreatedToUtc { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public string SortDirection { get; init; } = "desc";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var fields = new[] { "customerNumber", "firstName", "lastName", "email", "status", "createdAt", "updatedAt" };
        if (!fields.Contains(SortBy, StringComparer.OrdinalIgnoreCase))
            yield return new("sortBy must be one of: customerNumber, firstName, lastName, email, status, createdAt, updatedAt.", [nameof(SortBy)]);
        if (!string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) && !string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            yield return new("sortDirection must be asc or desc.", [nameof(SortDirection)]);
        if (CreatedFromUtc.HasValue && CreatedToUtc.HasValue && CreatedFromUtc > CreatedToUtc)
            yield return new("createdFromUtc cannot be greater than createdToUtc.", [nameof(CreatedFromUtc), nameof(CreatedToUtc)]);
    }
}

public sealed record CustomerEligibilityResponse(
    Guid CustomerId,
    bool Exists,
    CustomerStatus? Status,
    bool CanCreateCart,
    bool CanPlaceOrder,
    bool HasDefaultShippingAddress,
    bool HasDefaultBillingAddress);

public sealed class BatchCustomerEligibilityRequest : IValidatableObject
{
    [Required, MinLength(1), MaxLength(100)] public IReadOnlyList<Guid> CustomerIds { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerIds.Any(x => x == Guid.Empty)) yield return new("customerIds cannot contain an empty GUID.", [nameof(CustomerIds)]);
        if (CustomerIds.Distinct().Count() != CustomerIds.Count) yield return new("Duplicate customer IDs are not allowed.", [nameof(CustomerIds)]);
    }
}

public sealed record BatchCustomerEligibilityItemResponse(
    Guid CustomerId,
    bool Exists,
    CustomerStatus? Status,
    bool CanCreateCart,
    bool CanPlaceOrder,
    bool HasDefaultShippingAddress,
    bool HasDefaultBillingAddress);

public sealed record BatchCustomerEligibilityResponse(IReadOnlyList<BatchCustomerEligibilityItemResponse> Items);
