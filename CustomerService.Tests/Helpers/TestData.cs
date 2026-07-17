using CustomerService.Application.DTOs.Addresses;
using CustomerService.Application.DTOs.Customers;
using CustomerService.Domain.Enums;

namespace CustomerService.Tests.Helpers;

public static class TestData
{
    public static CreateCustomerRequest Customer(string suffix = "one", bool withAddress = false) => new()
    {
        FirstName = "Omar", LastName = "Hassan", Email = $"omar.{suffix}@example.com", PhoneNumber = "+966 (50) 123-4567",
        InitialAddress = withAddress ? Address() : null
    };

    public static CreateCustomerAddressRequest Address(bool shipping = false, bool billing = false) => new()
    {
        Label = "Home", RecipientName = "Omar Hassan", AddressLine1 = "Example Street 1", City = "Riyadh", CountryCode = "sa",
        PhoneNumber = "+966-50-123-4567", IsDefaultShipping = shipping, IsDefaultBilling = billing
    };

    public static UpdateCustomerRequest Update(Guid token, string email = "omar.updated@example.com") => new()
    {
        FirstName = "Omar", LastName = "Updated", Email = email, PhoneNumber = "+966501234568", ConcurrencyToken = token
    };

    public static UpdateCustomerStatusRequest Status(CustomerStatus status, Guid token) => new() { Status = status, ConcurrencyToken = token, Reason = "Test" };

    public static UpdateCustomerAddressRequest UpdateAddress(Guid token) => new()
    {
        Label = "Work", RecipientName = "Omar H", AddressLine1 = "Business Road 2", City = "Jeddah", CountryCode = "SA", ConcurrencyToken = token
    };
}
