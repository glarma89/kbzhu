using NutritionTracker.Application.Tools;

namespace NutritionTracker.Application.Chat;

public sealed record SendChatMessageCommand(
    Guid UserId,
    string Message,
    string ClientMessageId,
    DateTimeOffset? OccurredAt);

public sealed record ExecutedActionResult(
    string ToolName,
    bool IsSuccess,
    string? ErrorCode);

public sealed record ChatMessageResult(
    string AssistantMessage,
    IReadOnlyList<ExecutedActionResult> ExecutedActions,
    string? PendingClarification,
    string? PendingConfirmation,
    DailySummaryToolResult? DailySummary);

public sealed record ChatAgentSettings(
    int MaximumIterations,
    TimeSpan Timeout)
{
    public static ChatAgentSettings Default { get; } = new(8, TimeSpan.FromSeconds(90));
}

public interface IChatMessageService
{
    Task<ChatMessageResult> SendAsync(
        SendChatMessageCommand command,
        CancellationToken cancellationToken);
}
