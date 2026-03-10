namespace GdtCreator.Core.Validation;

public sealed class ValidationResult
{
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Success()
    {
        return new ValidationResult();
    }

    public static ValidationResult Failure(IEnumerable<string> errors)
    {
        return new ValidationResult { Errors = errors.ToArray() };
    }
}
