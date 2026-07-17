using CustomerService.Application.DTOs.Addresses;
using CustomerService.Application.Validation;
using CustomerService.Common.Exceptions;
using CustomerService.Domain.Entities;

namespace CustomerService.Application.Services;

internal static class AddressRules
{
    public static void Validate(CreateCustomerAddressRequest request) => ValidateValues(request.RecipientName, request.AddressLine1, request.City, request.CountryCode);
    public static void Validate(UpdateCustomerAddressRequest request) => ValidateValues(request.RecipientName, request.AddressLine1, request.City, request.CountryCode);

    public static CustomerAddress Create(Guid customerId, Customer customer, CreateCustomerAddressRequest request, DateTimeOffset now, bool forceDefaults = false) => new()
    {
        Id = Guid.NewGuid(), CustomerId = customerId, Customer = customer, Label = Clean(request.Label), RecipientName = request.RecipientName.Trim(),
        AddressLine1 = request.AddressLine1.Trim(), AddressLine2 = Clean(request.AddressLine2), City = request.City.Trim(),
        StateOrProvince = Clean(request.StateOrProvince), PostalCode = Clean(request.PostalCode), CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
        PhoneNumber = PhoneNumberNormalizer.Normalize(request.PhoneNumber), IsDefaultShipping = forceDefaults || request.IsDefaultShipping,
        IsDefaultBilling = forceDefaults || request.IsDefaultBilling, CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid()
    };

    public static CustomerAddressResponse Map(CustomerAddress x) => new(x.Id, x.CustomerId, x.Label, x.RecipientName, x.AddressLine1, x.AddressLine2,
        x.City, x.StateOrProvince, x.PostalCode, x.CountryCode, x.PhoneNumber, x.IsDefaultShipping, x.IsDefaultBilling, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyToken);

    public static void Apply(CustomerAddress address, UpdateCustomerAddressRequest request)
    {
        Validate(request);
        address.Label = Clean(request.Label);
        address.RecipientName = request.RecipientName.Trim();
        address.AddressLine1 = request.AddressLine1.Trim();
        address.AddressLine2 = Clean(request.AddressLine2);
        address.City = request.City.Trim();
        address.StateOrProvince = Clean(request.StateOrProvince);
        address.PostalCode = Clean(request.PostalCode);
        address.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        address.PhoneNumber = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
    }

    private static void ValidateValues(string recipient, string line1, string city, string country)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(recipient) || recipient.Trim().Length > 200) errors["recipientName"] = ["recipientName is required and cannot exceed 200 characters."];
        if (string.IsNullOrWhiteSpace(line1) || line1.Trim().Length > 250) errors["addressLine1"] = ["addressLine1 is required and cannot exceed 250 characters."];
        if (string.IsNullOrWhiteSpace(city) || city.Trim().Length > 100) errors["city"] = ["city is required and cannot exceed 100 characters."];
        if (string.IsNullOrWhiteSpace(country) || country.Trim().Length != 2 || !country.Trim().All(char.IsLetter)) errors["countryCode"] = ["countryCode must contain exactly two letters."];
        if (errors.Count > 0) throw new Common.Exceptions.ValidationException("Validation failed.", errors);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
