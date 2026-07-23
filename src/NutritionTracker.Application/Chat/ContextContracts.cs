using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Application.Chat;

public sealed record ContextNutritionTarget(
    DateOnly ValidFrom,
    decimal Calories,
    decimal ProteinGrams,
    decimal FatGrams,
    decimal CarbohydrateGrams);

public sealed record ContextUserSettings(
    string TimeZone,
    string MeasurementUnits,
    ContextNutritionTarget? DailyTarget);

public sealed record ContextConversationMessage(
    Guid SourceMessageId,
    ChatRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc);

public sealed record ContextPendingState(
    Guid SourceMessageId,
    MessageProcessingState State,
    string OriginalMessage,
    string PendingQuestion,
    string? ToolName,
    string? ToolArgumentsJson);

public sealed record ContextSummary(string Content, DateTimeOffset CreatedAtUtc);

public sealed record ChatContextSnapshot(
    ContextUserSettings UserSettings,
    IReadOnlyList<ContextConversationMessage> RecentMessages,
    ContextPendingState? PendingState,
    ContextSummary? Summary);

public sealed record ChatContextSourceRequest(
    Guid UserId,
    Guid CurrentMessageId,
    DateTimeOffset TrustedRequestTime);

public interface IChatContextSource
{
    Task<ChatContextSnapshot> GetAsync(
        ChatContextSourceRequest request,
        CancellationToken cancellationToken);
}

public sealed record ContextBuildRequest(
    string SystemInstruction,
    Guid CurrentMessageId,
    string CurrentUserMessage,
    ChatContextSnapshot Snapshot,
    IReadOnlyList<LanguageModelToolOutput> ToolOutputs,
    bool IncludeConversationMessages);

public sealed record ContextBuildResult(
    string Instructions,
    IReadOnlyList<LanguageModelInputMessage> Messages,
    IReadOnlyList<LanguageModelToolOutput> ToolOutputs,
    int CharacterCount);

public sealed record ContextBuilderSettings(
    int MaximumCharacters,
    int MaximumRecentMessages,
    int MinimumRecentMessages)
{
    public static ContextBuilderSettings Default { get; } = new(32_000, 6, 2);
}

public interface IContextBuilder
{
    ContextBuildResult Build(ContextBuildRequest request);
}
