using System.Text.Json.Serialization;

namespace CartService.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<CartStatus>))]
public enum CartStatus
{
    Active = 1,
    CheckoutPending = 2,
    CheckedOut = 3,
    Abandoned = 4,
    Expired = 5
}
