using System.ComponentModel.DataAnnotations;

namespace CustomerService.Application.Validation;

public static class EmailNormalizer
{
    public static (string Email, string NormalizedEmail) Normalize(string email)
    {
        var display = email.Trim();
        if (display.Length > 320 || !new EmailAddressAttribute().IsValid(display))
            throw new Common.Exceptions.ValidationException("The email address is invalid.", new Dictionary<string, string[]> { ["email"] = ["A valid email address is required."] });
        return (display, display.ToUpperInvariant());
    }
}
