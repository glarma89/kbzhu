using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NutritionTracker.Application.Common;
using NutritionTracker.Application.Tools;
using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Application.Chat;

public sealed class UserMessageProcessingService(
    IUserMessageProcessingRepository repository,
    IUserMessageInterpreter interpreter,
    IMessageToolExecutor toolExecutor,
    TimeProvider timeProvider) : IUserMessageProcessingService
{
    private const int MaximumDeliveryKeyLength = 200;
    private const int MaximumMessageLength = 10_000;
    private const int MaximumClarificationLength = 2_000;
    private const string CancellationResult = "{\"status\":\"cancelled\"}";

    public async Task<MessageProcessingResult> ReceiveAsync(
        ReceiveUserMessageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.MessageId, nameof(command.MessageId));
        ValidateId(command.UserId, nameof(command.UserId));
        ValidateRequiredText(
            command.DeliveryKey, MaximumDeliveryKeyLength, nameof(command.DeliveryKey));
        ValidateRequiredText(command.Content, MaximumMessageLength, nameof(command.Content));

        var existing = await repository.GetByDeliveryKeyAsync(
            command.UserId, command.DeliveryKey.Trim(), cancellationToken);
        if (existing is not null)
        {
            return Map(existing.Processing, isDuplicateDelivery: true);
        }

        var now = timeProvider.GetUtcNow();
        var message = new ChatMessage(
            command.MessageId, command.UserId, ChatRole.User, command.Content, now);
        var processing = new UserMessageProcessing(
            message.Id, command.UserId, command.DeliveryKey, now);
        var stored = await repository.AddOrGetByDeliveryKeyAsync(
            new StoredUserMessage(message, processing), cancellationToken);
        return Map(
            stored.Processing,
            isDuplicateDelivery: stored.Message.Id != command.MessageId);
    }

    public async Task<MessageProcessingResult> ProcessAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stored = await GetRequiredAsync(messageId, userId, cancellationToken);
        return await ContinueAsync(stored, cancellationToken);
    }

    public async Task<MessageProcessingResult> SupplyClarificationAsync(
        Guid messageId,
        Guid userId,
        string response,
        CancellationToken cancellationToken)
    {
        ValidateRequiredText(response, MaximumClarificationLength, nameof(response));
        var stored = await GetRequiredAsync(messageId, userId, cancellationToken);
        stored.Processing.SupplyClarification(response, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return await ContinueAsync(stored, cancellationToken);
    }

    public async Task<MessageProcessingResult> ConfirmAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stored = await GetRequiredAsync(messageId, userId, cancellationToken);
        stored.Processing.Confirm(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return await ExecuteAsync(stored, cancellationToken);
    }

    public async Task<MessageProcessingResult> CancelAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stored = await GetRequiredAsync(messageId, userId, cancellationToken);
        stored.Processing.Cancel(CancellationResult, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(stored.Processing);
    }

    public async Task<MessageProcessingResult> RetryAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stored = await GetRequiredAsync(messageId, userId, cancellationToken);
        stored.Processing.Retry(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return await ContinueAsync(stored, cancellationToken);
    }

    public async Task<MessageProcessingResult> MarkResponseDeliveredAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stored = await GetRequiredAsync(messageId, userId, cancellationToken);
        stored.Processing.MarkResponseDelivered(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(stored.Processing);
    }

    private async Task<MessageProcessingResult> ContinueAsync(
        StoredUserMessage stored,
        CancellationToken cancellationToken)
    {
        if (stored.Processing.State == MessageProcessingState.Received)
        {
            stored.Processing.StartInterpreting(timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
        }

        if (stored.Processing.State == MessageProcessingState.Interpreting)
        {
            return await InterpretAsync(stored, cancellationToken);
        }

        if (stored.Processing.State == MessageProcessingState.Executing)
        {
            return await ExecuteAsync(stored, cancellationToken);
        }

        return Map(stored.Processing);
    }

    private async Task<MessageProcessingResult> InterpretAsync(
        StoredUserMessage stored,
        CancellationToken cancellationToken)
    {
        MessageInterpretation interpretation;
        try
        {
            interpretation = await interpreter.InterpretAsync(
                new MessageInterpretationRequest(
                    stored.Message.Id,
                    stored.Message.UserId,
                    stored.Message.Content,
                    stored.Processing.InterpretationJson,
                    stored.Processing.ClarificationResponse),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stored.Processing.Fail(
                "interpretation_failed", "The message could not be interpreted.", timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            return Map(stored.Processing);
        }

        try
        {
            ValidateJsonObject(interpretation.InterpretationJson, nameof(interpretation.InterpretationJson));
            if (interpretation.Disposition == InterpretationDisposition.NeedsClarification)
            {
                stored.Processing.AwaitClarification(
                    interpretation.InterpretationJson,
                    interpretation.UserQuestion ?? "Please clarify the requested action.",
                    timeProvider.GetUtcNow());
                await repository.SaveChangesAsync(cancellationToken);
                return Map(stored.Processing);
            }

            if (interpretation.Disposition == InterpretationDisposition.Cancelled)
            {
                stored.Processing.Cancel(CancellationResult, timeProvider.GetUtcNow());
                await repository.SaveChangesAsync(cancellationToken);
                return Map(stored.Processing);
            }

            if (interpretation.Disposition != InterpretationDisposition.ReadyToExecute)
            {
                throw new ApplicationValidationException("The interpretation disposition is invalid.");
            }

            var toolName = RequireText(interpretation.ToolName, nameof(interpretation.ToolName));
            var argumentsJson = RequireText(
                interpretation.ToolArgumentsJson, nameof(interpretation.ToolArgumentsJson));
            var definition = ToolCatalog.GetRequired(toolName);
            _ = ToolJson.DeserializeArguments(argumentsJson, definition.ArgumentsType);
            var idempotencyKey = definition.Idempotency.Requirement == ToolIdempotencyRequirement.Required
                ? CreateIdempotencyKey(stored.Message.Id, toolName)
                : null;

            if (definition.ConfirmationRequirement == ToolConfirmationRequirement.Required)
            {
                stored.Processing.AwaitConfirmation(
                    interpretation.InterpretationJson,
                    toolName,
                    argumentsJson,
                    idempotencyKey,
                    interpretation.UserQuestion ?? definition.ConfirmationDescription,
                    timeProvider.GetUtcNow());
                await repository.SaveChangesAsync(cancellationToken);
                return Map(stored.Processing);
            }

            stored.Processing.BeginExecution(
                interpretation.InterpretationJson,
                toolName,
                argumentsJson,
                idempotencyKey,
                timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            return await ExecuteAsync(stored, cancellationToken);
        }
        catch (Exception exception) when (
            exception is JsonException or
            ToolArgumentsValidationException or
            ApplicationValidationException or
            InvalidOperationException)
        {
            stored.Processing.Fail(
                "invalid_interpretation",
                "The interpreted operation failed backend validation.",
                timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            return Map(stored.Processing);
        }
    }

    private async Task<MessageProcessingResult> ExecuteAsync(
        StoredUserMessage stored,
        CancellationToken cancellationToken)
    {
        var processing = stored.Processing;
        var toolName = RequireText(processing.ToolName, nameof(processing.ToolName));
        var argumentsJson = RequireText(
            processing.ToolArgumentsJson, nameof(processing.ToolArgumentsJson));
        ToolConfirmationEvidence? confirmation = null;
        if (processing.ConfirmedAtUtc is not null)
        {
            confirmation = new ToolConfirmationEvidence(
                toolName,
                CreateArgumentsHash(argumentsJson),
                processing.ConfirmedAtUtc.Value);
        }

        try
        {
            var outcome = await toolExecutor.ExecuteAsync(
                new MessageToolExecutionRequest(
                    toolName,
                    argumentsJson,
                    new ToolInvocationContext(
                        stored.Message.UserId,
                        processing.IdempotencyKey,
                        stored.Message.Id,
                        confirmation)),
                cancellationToken);
            ValidateJsonObject(outcome.ResultJson, nameof(outcome.ResultJson));
            if (outcome.IsSuccess)
            {
                processing.CompleteExecution(outcome.ResultJson, timeProvider.GetUtcNow());
            }
            else
            {
                processing.Fail(
                    outcome.ErrorCode ?? "tool_failed",
                    outcome.ErrorMessage ?? "The tool execution failed.",
                    timeProvider.GetUtcNow(),
                    outcome.ResultJson);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            processing.Fail(
                "tool_execution_failed", "The tool execution did not complete.", timeProvider.GetUtcNow());
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Map(processing);
    }

    private async Task<StoredUserMessage> GetRequiredAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        ValidateId(messageId, nameof(messageId));
        ValidateId(userId, nameof(userId));
        return await repository.GetByMessageIdAsync(messageId, userId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(UserMessageProcessing), messageId);
    }

    private static MessageProcessingResult Map(
        UserMessageProcessing processing,
        bool isDuplicateDelivery = false)
    {
        return new MessageProcessingResult(
            processing.MessageId,
            processing.State,
            isDuplicateDelivery,
            processing.PendingQuestion,
            processing.ToolName,
            processing.IdempotencyKey,
            processing.ExecutionResultJson,
            processing.FailureCode,
            processing.FailureMessage,
            processing.HasUndeliveredResult);
    }

    private static string CreateIdempotencyKey(Guid messageId, string toolName) =>
        $"message:{messageId:N}:{toolName}";

    private static string CreateArgumentsHash(string argumentsJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(argumentsJson)));

    private static void ValidateJsonObject(string json, string parameterName)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ApplicationValidationException("The value must be a JSON object.", parameterName);
        }
    }

    private static string RequireText(string? value, string parameterName)
    {
        ValidateRequiredText(value, MaximumMessageLength, parameterName);
        return value!.Trim();
    }

    private static void ValidateRequiredText(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApplicationValidationException("The value is required.", parameterName);
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new ApplicationValidationException(
                $"The value cannot exceed {maximumLength} characters.", parameterName);
        }
    }

    private static void ValidateId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ApplicationValidationException("The identifier cannot be empty.", parameterName);
        }
    }
}
