using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Week09.Api.Contracts;

public static class RequestValidation
{
    public static Dictionary<string, string[]> Check<T>(T request) where T : class
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors,
            validateAllProperties: true);

        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var error in errors)
        {
            IEnumerable<string> members = error.MemberNames.Any()
                ? error.MemberNames : ["body"];

            foreach (var member in members)
            {
                string key = JsonNamingPolicy.CamelCase.ConvertName(member);
                if (!result.TryGetValue(key, out var messages))
                {
                    messages = [];
                    result[key] = messages;
                }
                messages.Add(error.ErrorMessage ?? "Niepoprawna wartość.");
            }
        }
        return result.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }
}
