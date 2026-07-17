namespace CustomerService.Domain.Entities;

public sealed class CustomerAddress
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string? Label { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateOrProvince { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsDefaultShipping { get; set; }
    public bool IsDefaultBilling { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
