namespace NutritionTracker.Infrastructure.LanguageModels;

public sealed record OpenAiLanguageModelOptions(
    string? ApiKey,
    string Model,
    TimeSpan RequestTimeout,
    int MaximumRetries,
    TimeSpan InitialRetryDelay);
