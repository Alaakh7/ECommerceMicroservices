namespace OrderService.Application.DTOs.ExternalServices;

public sealed record CartPrepareCheckoutExternalRequest(string OperationId, bool AcceptPriceChanges, Guid ConcurrencyToken);
public sealed record CartCheckoutItemExternalResponse(Guid ProductId, string Sku, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);
public sealed record CartPrepareCheckoutExternalResponse(Guid CartId, Guid CustomerId, Guid CheckoutToken, string CheckoutOperationId, DateTimeOffset CheckoutExpiresAtUtc, string Currency, decimal Subtotal, int TotalQuantity, IReadOnlyList<CartCheckoutItemExternalResponse> Items, bool WasAlreadyPrepared);
public sealed record CartCompleteCheckoutExternalRequest(Guid CheckoutToken, string OperationId, Guid OrderId);
public sealed record CartCompleteCheckoutExternalResponse(Guid CartId, Guid OrderId, DateTimeOffset CheckedOutAtUtc, bool WasAlreadyCompleted);
public sealed record CartCancelCheckoutExternalRequest(Guid CheckoutToken, string OperationId, string? Reason);
public sealed record CartItemExternalResponse(Guid Id, Guid ProductId, string Sku, string ProductName, string? ImageUrl, decimal UnitPrice, int Quantity, decimal LineTotal, Guid ConcurrencyToken);
public sealed record CartExternalResponse(Guid Id, Guid CustomerId, string Status, string Currency, decimal Subtotal, int TotalQuantity, int DistinctItemCount, Guid? CheckoutToken, string? CheckoutOperationId, Guid ConcurrencyToken, IReadOnlyList<CartItemExternalResponse> Items);

public sealed record CustomerEligibilityExternalResponse(Guid CustomerId, bool Exists, string? Status, bool CanCreateCart, bool CanPlaceOrder, bool HasDefaultShippingAddress, bool HasDefaultBillingAddress);
public sealed record CustomerDetailsExternalResponse(Guid Id, string CustomerNumber, string FirstName, string LastName, string FullName, string Email, string? PhoneNumber, string Status, bool CanCreateCart, bool CanPlaceOrder, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, Guid ConcurrencyToken, IReadOnlyList<CustomerAddressExternalResponse> Addresses);
public sealed record CustomerAddressExternalResponse(Guid Id, Guid CustomerId, string? Label, string RecipientName, string AddressLine1, string? AddressLine2, string City, string? StateOrProvince, string? PostalCode, string CountryCode, string? PhoneNumber, bool IsDefaultShipping, bool IsDefaultBilling, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, Guid ConcurrencyToken);

public sealed record StockAdjustmentExternalRequest(string OperationId, int Quantity, string Reason);
public sealed record StockAdjustmentExternalResponse(Guid ProductId, string OperationId, string OperationType, int Quantity, int StockBefore, int StockAfter, DateTimeOffset ProcessedAtUtc, bool WasAlreadyProcessed);
public sealed record ProductAvailabilityExternalResponse(Guid ProductId, int RequestedQuantity, int AvailableQuantity, bool IsAvailable, bool IsActive);
