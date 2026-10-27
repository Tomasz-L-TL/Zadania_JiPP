namespace Week03.Api.Validation;

public interface IRequestValidator
{
    Dictionary<string, string[]> Validate<TRequest>(TRequest request) where TRequest : class;
}
