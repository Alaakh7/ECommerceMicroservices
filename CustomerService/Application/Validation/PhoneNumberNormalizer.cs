using System.Text.RegularExpressions;

namespace CustomerService.Application.Validation;

public static partial class PhoneNumberNormalizer
{
    public static string? Normalize(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return null;
        var value = RemovableCharacters().Replace(phoneNumber.Trim(), string.Empty);
        if (!ValidPhone().IsMatch(value) || value.Length > 30)
            throw new Common.Exceptions.ValidationException("The phone number is invalid.", new Dictionary<string, string[]> { ["phoneNumber"] = ["Use digits with an optional leading +; spaces, parentheses, and hyphens are accepted."] });
        return value;
    }

    [GeneratedRegex(@"[\s()\-]")]
    private static partial Regex RemovableCharacters();

    [GeneratedRegex(@"^\+?[0-9]+$")]
    private static partial Regex ValidPhone();
}
