using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Chat;

public sealed class UserMessageProcessing
{
    private UserMessageProcessing()
    {
        DeliveryKey = null!;
    }

    public UserMessageProcessing(
        Guid messageId,
        Guid userId,
        string deliveryKey,
        DateTimeOffset createdAtUtc)
    {
        MessageId = DomainGuard.NotEmpty(messageId, nameof(messageId));
        UserId = DomainGuard.NotEmpty(userId, nameof(userId));
        DeliveryKey = DomainGuard.RequiredText(deliveryKey, nameof(deliveryKey));
        State = MessageProcessingState.Received;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid MessageId { get; }

    public Guid UserId { get; }

    public string DeliveryKey { get; }

    public MessageProcessingState State { get; private set; }

    public string? InterpretationJson { get; private set; }

    public string? PendingQuestion { get; private set; }

    public string? ClarificationResponse { get; private set; }

    public string? ToolName { get; private set; }

    public string? ToolArgumentsJson { get; private set; }

    public string? ToolArgumentsHash { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public string? ExecutionResultJson { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    public MessageProcessingState? RetryFromState { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    public DateTimeOffset? ResponseDeliveredAtUtc { get; private set; }

    public bool HasUndeliveredResult =>
        State == MessageProcessingState.Completed && ResponseDeliveredAtUtc is null;

    public void StartInterpreting(DateTimeOffset changedAtUtc)
    {
        EnsureState(MessageProcessingState.Received);
        SetState(MessageProcessingState.Interpreting, changedAtUtc);
    }

    public void AwaitClarification(
        string interpretationJson,
        string question,
        DateTimeOffset changedAtUtc)
    {
        EnsureOneOf(MessageProcessingState.Interpreting, MessageProcessingState.Executing);
        InterpretationJson = DomainGuard.RequiredText(interpretationJson, nameof(interpretationJson));
        PendingQuestion = DomainGuard.RequiredText(question, nameof(question));
        SetState(MessageProcessingState.AwaitingClarification, changedAtUtc);
    }

    public void SupplyClarification(string response, DateTimeOffset changedAtUtc)
    {
        EnsureState(MessageProcessingState.AwaitingClarification);
        ClarificationResponse = DomainGuard.RequiredText(response, nameof(response));
        PendingQuestion = null;
        SetState(MessageProcessingState.Interpreting, changedAtUtc);
    }

    public void AwaitConfirmation(
        string interpretationJson,
        string toolName,
        string toolArgumentsJson,
        string toolArgumentsHash,
        string? idempotencyKey,
        string question,
        DateTimeOffset changedAtUtc)
    {
        EnsureOneOf(MessageProcessingState.Interpreting, MessageProcessingState.Executing);
        SetPreparedOperation(
            interpretationJson, toolName, toolArgumentsJson, toolArgumentsHash, idempotencyKey);
        PendingQuestion = DomainGuard.RequiredText(question, nameof(question));
        SetState(MessageProcessingState.AwaitingConfirmation, changedAtUtc);
    }

    public void Confirm(DateTimeOffset changedAtUtc)
    {
        EnsureState(MessageProcessingState.AwaitingConfirmation);
        PendingQuestion = null;
        SetState(MessageProcessingState.Executing, changedAtUtc);
        ConfirmedAtUtc = UpdatedAtUtc;
    }

    public void BeginExecution(
        string interpretationJson,
        string toolName,
        string toolArgumentsJson,
        string toolArgumentsHash,
        string? idempotencyKey,
        DateTimeOffset changedAtUtc)
    {
        EnsureState(MessageProcessingState.Interpreting);
        SetPreparedOperation(
            interpretationJson, toolName, toolArgumentsJson, toolArgumentsHash, idempotencyKey);
        SetState(MessageProcessingState.Executing, changedAtUtc);
    }

    public void CompleteExecution(string executionResultJson, DateTimeOffset changedAtUtc)
    {
        EnsureState(MessageProcessingState.Executing);
        ExecutionResultJson = DomainGuard.RequiredText(executionResultJson, nameof(executionResultJson));
        Complete(changedAtUtc);
    }

    public void CompleteInterpretation(string executionResultJson, DateTimeOffset changedAtUtc)
    {
        EnsureState(MessageProcessingState.Interpreting);
        ExecutionResultJson = DomainGuard.RequiredText(executionResultJson, nameof(executionResultJson));
        Complete(changedAtUtc);
    }

    public void Cancel(string cancellationResultJson, DateTimeOffset changedAtUtc)
    {
        if (State is MessageProcessingState.Executing or
            MessageProcessingState.Completed or
            MessageProcessingState.Failed)
        {
            throw new InvalidOperationException($"Cannot cancel a message in state {State}.");
        }

        ExecutionResultJson = DomainGuard.RequiredText(
            cancellationResultJson, nameof(cancellationResultJson));
        PendingQuestion = null;
        Complete(changedAtUtc);
    }

    public void Fail(
        string code,
        string message,
        DateTimeOffset changedAtUtc,
        string? executionResultJson = null)
    {
        if (State is MessageProcessingState.Completed or MessageProcessingState.Failed)
        {
            throw new InvalidOperationException($"Cannot fail a message in state {State}.");
        }

        FailureCode = DomainGuard.RequiredText(code, nameof(code));
        FailureMessage = DomainGuard.RequiredText(message, nameof(message));
        if (executionResultJson is not null)
        {
            ExecutionResultJson = DomainGuard.RequiredText(
                executionResultJson, nameof(executionResultJson));
        }

        RetryFromState = State;
        SetState(MessageProcessingState.Failed, changedAtUtc);
    }

    public void Retry(DateTimeOffset changedAtUtc)
    {
        EnsureState(MessageProcessingState.Failed);
        var retryState = RetryFromState
            ?? throw new InvalidOperationException("The failed message has no retry state.");
        State = retryState;
        FailureCode = null;
        FailureMessage = null;
        RetryFromState = null;
        SetUpdatedAt(changedAtUtc);
    }

    public void MarkResponseDelivered(DateTimeOffset changedAtUtc)
    {
        EnsureState(MessageProcessingState.Completed);
        if (ResponseDeliveredAtUtc is not null)
        {
            return;
        }

        ResponseDeliveredAtUtc = ValidateTimestamp(changedAtUtc);
        UpdatedAtUtc = ResponseDeliveredAtUtc.Value;
    }

    private void SetPreparedOperation(
        string interpretationJson,
        string toolName,
        string toolArgumentsJson,
        string toolArgumentsHash,
        string? idempotencyKey)
    {
        InterpretationJson = DomainGuard.RequiredText(interpretationJson, nameof(interpretationJson));
        ToolName = DomainGuard.RequiredText(toolName, nameof(toolName));
        ToolArgumentsJson = DomainGuard.RequiredText(toolArgumentsJson, nameof(toolArgumentsJson));
        ToolArgumentsHash = DomainGuard.RequiredText(toolArgumentsHash, nameof(toolArgumentsHash));
        IdempotencyKey = DomainGuard.OptionalText(idempotencyKey);
    }

    private void Complete(DateTimeOffset changedAtUtc)
    {
        SetState(MessageProcessingState.Completed, changedAtUtc);
        CompletedAtUtc = UpdatedAtUtc;
        FailureCode = null;
        FailureMessage = null;
        RetryFromState = null;
    }

    private void SetState(MessageProcessingState state, DateTimeOffset changedAtUtc)
    {
        State = state;
        SetUpdatedAt(changedAtUtc);
    }

    private void SetUpdatedAt(DateTimeOffset changedAtUtc)
    {
        UpdatedAtUtc = ValidateTimestamp(changedAtUtc);
    }

    private DateTimeOffset ValidateTimestamp(DateTimeOffset value)
    {
        var utc = DomainGuard.Utc(value, nameof(value));
        if (utc < UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Timestamps cannot move backwards.");
        }

        return utc;
    }

    private void EnsureState(MessageProcessingState required)
    {
        if (State != required)
        {
            throw new InvalidOperationException($"Expected state {required}, but the message is {State}.");
        }
    }

    private void EnsureOneOf(MessageProcessingState first, MessageProcessingState second)
    {
        if (State != first && State != second)
        {
            throw new InvalidOperationException(
                $"Expected state {first} or {second}, but the message is {State}.");
        }
    }
}
