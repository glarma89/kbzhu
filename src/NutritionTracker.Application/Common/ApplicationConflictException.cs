namespace NutritionTracker.Application.Common;

public sealed class ApplicationConflictException(string message) : Exception(message);
