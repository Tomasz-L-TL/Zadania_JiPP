using System.ComponentModel.DataAnnotations;

namespace Week05.Api.Validation;

public static class UserValidator
{
    private static readonly EmailAddressAttribute EmailAttribute = new();

    public static Dictionary<string, string[]> Validate(string? name, string? email, int? age)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var trimmedName = name?.Trim();
        var trimmedEmail = email?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            errors["name"] = ["Name is required."];
        else if (trimmedName.Length > 100)
            errors["name"] = ["Name cannot be longer than 100 characters."];

        if (string.IsNullOrWhiteSpace(trimmedEmail))
            errors["email"] = ["Email is required."];
        else if (trimmedEmail.Length > 200 || !EmailAttribute.IsValid(trimmedEmail))
            errors["email"] = ["Email must be valid and no longer than 200 characters."];

        if (age is null or < 18 or > 120)
            errors["age"] = ["Age must be between 18 and 120."];

        return errors;
    }
}
