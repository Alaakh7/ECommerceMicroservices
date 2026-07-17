using CustomerService.Application.DTOs.Addresses;
using CustomerService.Application.DTOs.Customers;
using CustomerService.Application.Validation;
using CustomerService.Common.Exceptions;
using CustomerService.Domain.Enums;
using CustomerService.Tests.Fixtures;
using CustomerService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Tests.Unit;

public sealed class CustomerApplicationServiceTests
{
    [Fact]
    public async Task Create_valid_customer()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer(), default);
        Assert.Equal("Omar Hassan", customer.FullName);
        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public async Task Customer_numbers_are_unique_and_well_formed()
    {
        await using var fixture = new ServiceFixture();
        var first = await fixture.Customers.CreateAsync(TestData.Customer("a"), default);
        var second = await fixture.Customers.CreateAsync(TestData.Customer("b"), default);
        Assert.StartsWith("CUS-", first.CustomerNumber);
        Assert.Equal(16, first.CustomerNumber.Length);
        Assert.NotEqual(first.CustomerNumber, second.CustomerNumber);
    }

    [Fact]
    public void Email_is_trimmed_and_normalized()
    {
        var result = EmailNormalizer.Normalize(" User@Example.com ");
        Assert.Equal("User@Example.com", result.Email);
        Assert.Equal("USER@EXAMPLE.COM", result.NormalizedEmail);
    }

    [Fact]
    public void Invalid_email_is_rejected() => Assert.Throws<ValidationException>(() => EmailNormalizer.Normalize("not-an-email"));

    [Fact]
    public async Task Duplicate_email_is_rejected_case_insensitively()
    {
        await using var fixture = new ServiceFixture();
        await fixture.Customers.CreateAsync(TestData.Customer("duplicate"), default);
        var request = TestData.Customer("DUPLICATE");
        await Assert.ThrowsAsync<DuplicateEmailException>(() => fixture.Customers.CreateAsync(request, default));
    }

    [Fact]
    public void Phone_is_normalized() => Assert.Equal("+966501234567", PhoneNumberNormalizer.Normalize("+966 (50) 123-4567"));

    [Fact]
    public async Task Customer_can_be_updated()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("update"), default);
        var updated = await fixture.Customers.UpdateAsync(customer.Id, TestData.Update(customer.ConcurrencyToken), default);
        Assert.Equal("Updated", updated.LastName);
        Assert.NotEqual(customer.ConcurrencyToken, updated.ConcurrencyToken);
    }

    [Fact]
    public async Task Updating_missing_customer_is_rejected()
    {
        await using var fixture = new ServiceFixture();
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Customers.UpdateAsync(Guid.NewGuid(), TestData.Update(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Stale_customer_token_is_rejected()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("stale"), default);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => fixture.Customers.UpdateAsync(customer.Id, TestData.Update(Guid.NewGuid()), default));
    }

    [Theory]
    [InlineData(CustomerStatus.Active)]
    [InlineData(CustomerStatus.Suspended)]
    [InlineData(CustomerStatus.Deactivated)]
    public async Task Customer_status_can_be_changed(CustomerStatus status)
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer(status.ToString()), default);
        var changed = await fixture.Customers.UpdateStatusAsync(customer.Id, TestData.Status(status, customer.ConcurrencyToken), default);
        Assert.Equal(status, changed.Status);
    }

    [Fact]
    public async Task Customer_delete_is_soft_and_hidden()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("delete"), default);
        await fixture.Customers.DeleteAsync(customer.Id, customer.ConcurrencyToken, default);
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Customers.GetByIdAsync(customer.Id, default));
        Assert.True(await fixture.Db.Customers.IgnoreQueryFilters().AnyAsync(x => x.Id == customer.Id && x.IsDeleted));
    }

    [Theory]
    [InlineData(CustomerStatus.Active, true, true)]
    [InlineData(CustomerStatus.Suspended, true, false)]
    [InlineData(CustomerStatus.Deactivated, false, false)]
    public async Task Eligibility_follows_customer_status(CustomerStatus status, bool canCart, bool canOrder)
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("eligibility" + status), default);
        if (status != CustomerStatus.Active) customer = await fixture.Customers.UpdateStatusAsync(customer.Id, TestData.Status(status, customer.ConcurrencyToken), default);
        var result = await fixture.Customers.GetEligibilityAsync(customer.Id, default);
        Assert.Equal(canCart, result.CanCreateCart);
        Assert.Equal(canOrder, result.CanPlaceOrder);
    }

    [Fact]
    public async Task First_address_becomes_both_defaults()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("first-address"), default);
        var address = await fixture.Addresses.CreateAsync(customer.Id, TestData.Address(), default);
        Assert.True(address.IsDefaultShipping);
        Assert.True(address.IsDefaultBilling);
    }

    [Fact]
    public async Task New_default_shipping_clears_previous()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("shipping", true), default);
        var second = await fixture.Addresses.CreateAsync(customer.Id, TestData.Address(shipping: true), default);
        var all = await fixture.Addresses.GetAsync(customer.Id, default);
        Assert.Single(all.Where(x => x.IsDefaultShipping));
        Assert.Equal(second.Id, all.Single(x => x.IsDefaultShipping).Id);
    }

    [Fact]
    public async Task New_default_billing_clears_previous()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("billing", true), default);
        var second = await fixture.Addresses.CreateAsync(customer.Id, TestData.Address(billing: true), default);
        var all = await fixture.Addresses.GetAsync(customer.Id, default);
        Assert.Single(all.Where(x => x.IsDefaultBilling));
        Assert.Equal(second.Id, all.Single(x => x.IsDefaultBilling).Id);
    }

    [Fact]
    public async Task Address_can_be_set_for_shipping_and_billing()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("both", true), default);
        var second = await fixture.Addresses.CreateAsync(customer.Id, TestData.Address(), default);
        var result = await fixture.Addresses.SetDefaultAsync(customer.Id, second.Id, new() { DefaultFor = AddressDefaultUsage.ShippingAndBilling, ConcurrencyToken = second.ConcurrencyToken }, default);
        Assert.True(result.IsDefaultShipping && result.IsDefaultBilling);
    }

    [Fact]
    public async Task Address_owned_by_another_customer_is_rejected()
    {
        await using var fixture = new ServiceFixture();
        var first = await fixture.Customers.CreateAsync(TestData.Customer("owner1", true), default);
        var second = await fixture.Customers.CreateAsync(TestData.Customer("owner2"), default);
        var address = first.Addresses.Single();
        await Assert.ThrowsAsync<AddressOwnershipException>(() => fixture.Addresses.UpdateAsync(second.Id, address.Id, TestData.UpdateAddress(address.ConcurrencyToken), default));
    }

    [Fact]
    public async Task Address_can_be_updated()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("address-update", true), default);
        var address = customer.Addresses.Single();
        var updated = await fixture.Addresses.UpdateAsync(customer.Id, address.Id, TestData.UpdateAddress(address.ConcurrencyToken), default);
        Assert.Equal("Jeddah", updated.City);
    }

    [Fact]
    public async Task Address_delete_is_soft()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("address-delete", true), default);
        var address = customer.Addresses.Single();
        await fixture.Addresses.DeleteAsync(customer.Id, address.Id, address.ConcurrencyToken, default);
        Assert.Empty(await fixture.Addresses.GetAsync(customer.Id, default));
        Assert.True(await fixture.Db.CustomerAddresses.IgnoreQueryFilters().AnyAsync(x => x.Id == address.Id && x.IsDeleted));
    }

    [Fact]
    public async Task Deactivated_customer_cannot_add_address()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("deactivated"), default);
        customer = await fixture.Customers.UpdateStatusAsync(customer.Id, TestData.Status(CustomerStatus.Deactivated, customer.ConcurrencyToken), default);
        await Assert.ThrowsAsync<InvalidCustomerStateException>(() => fixture.Addresses.CreateAsync(customer.Id, TestData.Address(), default));
    }

    [Fact]
    public async Task Batch_eligibility_returns_every_requested_id()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("batch"), default);
        var missing = Guid.NewGuid();
        var result = await fixture.Customers.GetBatchEligibilityAsync(new() { CustomerIds = [customer.Id, missing] }, default);
        Assert.Equal(2, result.Items.Count);
        Assert.False(result.Items.Single(x => x.CustomerId == missing).Exists);
    }

    [Fact]
    public async Task Query_pagination_occurs_in_database()
    {
        await using var fixture = new ServiceFixture();
        for (var i = 0; i < 3; i++) await fixture.Customers.CreateAsync(TestData.Customer("page" + i), default);
        var result = await fixture.Customers.GetAsync(new() { PageNumber = 2, PageSize = 2 }, default);
        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalItems);
    }

    [Fact]
    public async Task Unknown_sort_field_is_rejected()
    {
        await using var fixture = new ServiceFixture();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Customers.GetAsync(new() { SortBy = "unknown" }, default));
    }

    [Fact]
    public async Task Invalid_country_code_is_rejected()
    {
        await using var fixture = new ServiceFixture();
        var customer = await fixture.Customers.CreateAsync(TestData.Customer("country"), default);
        var invalid = new CreateCustomerAddressRequest { RecipientName = "Test", AddressLine1 = "Street", City = "City", CountryCode = "SAU" };
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Addresses.CreateAsync(customer.Id, invalid, default));
    }
}
