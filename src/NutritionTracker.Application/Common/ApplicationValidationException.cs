namespace NutritionTracker.Application.Common;

public sealed class ApplicationValidationException(string message, string? parameterName = null)
    : Exception(message)
{
    public string? ParameterName { get; } = parameterName;
}
