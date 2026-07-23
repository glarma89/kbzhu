using NutritionTracker.Domain.Meals;

namespace NutritionTracker.Application.Tools;

internal static class ToolArgumentValidation
{
    public const decimal MaximumWeightGrams = 1_000_000m;

    public static void RequiredText(
        ICollection<ToolValidationError> errors,
        string field,
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ToolValidationError(field, "required", "The value is required."));
        }
        else if (value.Trim().Length > maximumLength)
        {
            errors.Add(new ToolValidationError(
                field, "max_length", $"The value cannot exceed {maximumLength} characters."));
        }
    }

    public static void OptionalText(
        ICollection<ToolValidationError> errors,
        string field,
        string? value,
        int maximumLength)
    {
        if (value is not null && value.Trim().Length > maximumLength)
        {
            errors.Add(new ToolValidationError(
                field, "max_length", $"The value cannot exceed {maximumLength} characters."));
        }
    }

    public static void Id(ICollection<ToolValidationError> errors, string field, Guid value)
    {
        if (value == Guid.Empty)
        {
            errors.Add(new ToolValidationError(field, "invalid_guid", "A non-empty UUID is required."));
        }
    }

    public static void PositiveWeight(ICollection<ToolValidationError> errors, string field, decimal value)
    {
        if (value is <= 0 or > MaximumWeightGrams)
        {
            errors.Add(new ToolValidationError(
                field, "out_of_range", $"The value must be greater than 0 and at most {MaximumWeightGrams}."));
        }
    }

    public static void OptionalPositiveWeight(
        ICollection<ToolValidationError> errors,
        string field,
        decimal? value)
    {
        if (value is not null)
        {
            PositiveWeight(errors, field, value.Value);
        }
    }

    public static void Range(
        ICollection<ToolValidationError> errors,
        string field,
        decimal value,
        decimal minimum,
        decimal maximum)
    {
        if (value < minimum || value > maximum)
        {
            errors.Add(new ToolValidationError(
                field, "out_of_range", $"The value must be between {minimum} and {maximum}."));
        }
    }

    public static void Limit(ICollection<ToolValidationError> errors, string field, int value, int maximum)
    {
        if (value is < 1 || value > maximum)
        {
            errors.Add(new ToolValidationError(
                field, "out_of_range", $"The value must be between 1 and {maximum}."));
        }
    }

    public static void Version(ICollection<ToolValidationError> errors, string field, int? value)
    {
        if (value is <= 0)
        {
            errors.Add(new ToolValidationError(field, "out_of_range", "The version must be positive."));
        }
    }

    public static void OccurredAt(
        ICollection<ToolValidationError> errors,
        string field,
        DateTimeOffset value)
    {
        if (value == default)
        {
            errors.Add(new ToolValidationError(field, "required", "A timestamp with an offset is required."));
        }
    }

    public static void MealType(
        ICollection<ToolValidationError> errors,
        string field,
        MealType? value)
    {
        if (value is null)
        {
            errors.Add(new ToolValidationError(field, "required", "A meal type is required."));
        }
        else if (!Enum.IsDefined(value.Value))
        {
            errors.Add(new ToolValidationError(field, "invalid_enum", "The meal type is not supported."));
        }
    }
}
