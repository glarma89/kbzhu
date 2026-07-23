using NutritionTracker.Application.Tools;
using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Application.Chat;

public enum InterpretationDisposition
{
    ReadyToExecute = 1,
    NeedsClarification = 2,
    Cancelled = 3
}

public sealed record ReceiveUserMessageCommand(
    Guid MessageId,
    Guid UserId,
    string DeliveryKey,
    string Content);

public sealed record MessageInterpretationRequest(
    Guid MessageId,
    Guid UserId,
    string OriginalMessage,
    string? PreviousInterpretationJson,
    string? ClarificationResponse);

public sealed record MessageInterpretation(
    InterpretationDisposition Disposition,
    string InterpretationJson,
    string? ToolName = null,
    string? ToolArgumentsJson = null,
    string? UserQuestion = null);

public sealed record MessageToolExecutionRequest(
    string ToolName,
    string ArgumentsJson,
    ToolInvocationContext Context);

public sealed record MessageToolExecutionOutcome(
    bool IsSuccess,
    string ResultJson,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record MessageProcessingResult(
    Guid MessageId,
    MessageProcessingState State,
    bool IsDuplicateDelivery,
    string? PendingQuestion,
    string? ToolName,
    string? IdempotencyKey,
    string? ExecutionResultJson,
    string? FailureCode,
    string? FailureMessage,
    bool HasUndeliveredResult);

public sealed record StoredUserMessage(ChatMessage Message, UserMessageProcessing Processing);
