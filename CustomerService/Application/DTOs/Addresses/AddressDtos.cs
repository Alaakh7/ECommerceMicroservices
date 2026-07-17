using System.ComponentModel.DataAnnotations;
using CustomerService.Application.Validation;
using CustomerService.Domain.Enums;

namespace CustomerService.Application.DTOs.Addresses;

public sealed class CreateCustomerAddressRequest
{
    [StringLength(50)] public string? Label { get; init; }
    [Required, StringLength(200)] public string RecipientName { get; init; } = string.Empty;
    [Required, StringLength(250)] public string AddressLine1 { get; init; } = string.Empty;
    [StringLength(250)] public string? AddressLine2 { get; init; }
    [Required, StringLength(100)] public string City { get; init; } = string.Empty;
    [StringLength(100)] public string? StateOrProvince { get; init; }
    [StringLength(20)] public string? PostalCode { get; init; }
    [Required, RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "countryCode must contain exactly two letters.")] public string CountryCode { get; init; } = string.Empty;
    [StringLength(30)] public string? PhoneNumber { get; init; }
    public bool IsDefaultShipping { get; init; }
    public bool IsDefaultBilling { get; init; }
}

public sealed class UpdateCustomerAddressRequest
{
    [StringLength(50)] public string? Label { get; init; }
    [Required, StringLength(200)] public string RecipientName { get; init; } = string.Empty;
    [Required, StringLength(250)] public string AddressLine1 { get; init; } = string.Empty;
    [StringLength(250)] public string? AddressLine2 { get; init; }
    [Required, StringLength(100)] public string City { get; init; } = string.Empty;
    [StringLength(100)] public string? StateOrProvince { get; init; }
    [StringLength(20)] public string? PostalCode { get; init; }
    [Required, RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "countryCode must contain exactly two letters.")] public string CountryCode { get; init; } = string.Empty;
    [StringLength(30)] public string? PhoneNumber { get; init; }
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed class SetDefaultAddressRequest
{
    [EnumDataType(typeof(AddressDefaultUsage))] public AddressDefaultUsage DefaultFor { get; init; }
    [NotEmptyGuid(ErrorMessage = "concurrencyToken is required.")] public Guid ConcurrencyToken { get; init; }
}

public sealed record CustomerAddressResponse(
    Guid Id,
    Guid CustomerId,
    string? Label,
    string RecipientName,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? StateOrProvince,
    string? PostalCode,
    string CountryCode,
    string? PhoneNumber,
    bool IsDefaultShipping,
    bool IsDefaultBilling,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    Guid ConcurrencyToken);

public sealed record CustomerAddressSummaryResponse(
    Guid Id,
    string? Label,
    string RecipientName,
    string AddressLine1,
    string City,
    string CountryCode,
    bool IsDefaultShipping,
    bool IsDefaultBilling,
    Guid ConcurrencyToken);
