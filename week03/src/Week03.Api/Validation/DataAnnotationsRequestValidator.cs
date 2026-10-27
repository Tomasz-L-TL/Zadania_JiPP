using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Week03.Api.Validation;

public sealed class DataAnnotationsRequestValidator : IRequestValidator
{
    public Dictionary<string, string[]> Validate<TRequest>(TRequest request) where TRequest : class
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            // Nazwy pól zgodne z JSON (camelCase), a nie właściwościami C# (PascalCase).
            IEnumerable<string> names = result.MemberNames.Any() ? result.MemberNames : new[] { string.Empty };
            foreach (var name in names)
            {
                var key = string.IsNullOrEmpty(name) ? string.Empty : JsonNamingPolicy.CamelCase.ConvertName(name);
                if (!errors.TryGetValue(key, out var messages))
                {
                    messages = [];
                    errors.Add(key, messages);
                }
                messages.Add(result.ErrorMessage ?? "Niepoprawna wartość.");
            }
        }

        return errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }
}
