using System.ComponentModel.DataAnnotations;

namespace CartService.Application.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is Guid guid && guid != Guid.Empty;
}
