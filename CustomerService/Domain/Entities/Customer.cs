using CustomerService.Domain.Enums;

namespace CustomerService.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }
    public string CustomerNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public ICollection<CustomerAddress> Addresses { get; set; } = [];
}
