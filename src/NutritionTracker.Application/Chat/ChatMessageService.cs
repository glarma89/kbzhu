using System.Globalization;
using System.Text.Json;
using NutritionTracker.Application.Common;
using NutritionTracker.Application.Tools;
using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Application.Chat;

public sealed class ChatMessageService(
    IUserMessageProcessingRepository repository,
    ILanguageModelClient languageModelClient,
    IMessageToolExecutor toolExecutor,
    ChatAgentSettings settings,
    TimeProvider timeProvider) : IChatMessageService
{
    private const int MaximumMessageLength = 10_000;
    private const int MaximumClientMessageIdLength = 200;
    private const string DefaultFailureMessage = "Не удалось обработать сообщение. Попробуйте ещё раз.";

    private static readonly IReadOnlyList<LanguageModelToolDefinition> Tools = ToolCatalog.All
        .Select(definition => new LanguageModelToolDefinition(
            definition.Name,
            definition.Purpose,
            StrictToolSchemaFactory.Create(definition.ArgumentsJsonSchema),
            Strict: true))
        .ToArray();

    public async Task<ChatMessageResult> SendAsync(
        SendChatMessageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        var stored = await GetOrCreateAsync(command, cancellationToken);

        if (stored.Processing.State == MessageProcessingState.Completed)
        {
            return ToolJson.Deserialize<ChatMessageResult>(
                RequireResult(stored.Processing.ExecutionResultJson));
        }

        if (stored.Processing.State == MessageProcessingState.AwaitingClarification)
        {
            return PendingResult(stored.Processing.PendingQuestion, null);
        }

        if (stored.Processing.State == MessageProcessingState.AwaitingConfirmation)
        {
            return PendingResult(null, stored.Processing.PendingQuestion);
        }

        if (stored.Processing.State == MessageProcessingState.Failed)
        {
            return new ChatMessageResult(DefaultFailureMessage, [], null, null, null);
        }

        if (stored.Processing.State == MessageProcessingState.Received)
        {
            stored.Processing.StartInterpreting(timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
        }

        return await RunAgentLoopAsync(stored, command.OccurredAt, cancellationToken);
    }

    private async Task<StoredUserMessage> GetOrCreateAsync(
        SendChatMessageCommand command,
        CancellationToken cancellationToken)
    {
        var deliveryKey = command.ClientMessageId.Trim();
        var existing = await repository.GetByDeliveryKeyAsync(
            command.UserId, deliveryKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = timeProvider.GetUtcNow();
        var message = new ChatMessage(
            Guid.NewGuid(), command.UserId, ChatRole.User, command.Message, now);
        return await repository.AddOrGetByDeliveryKeyAsync(
            new StoredUserMessage(
                message,
                new UserMessageProcessing(message.Id, command.UserId, deliveryKey, now)),
            cancellationToken);
    }

    private async Task<ChatMessageResult> RunAgentLoopAsync(
        StoredUserMessage stored,
        DateTimeOffset? occurredAt,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(settings.Timeout);
        var agentCancellationToken = timeoutSource.Token;
        var actions = new List<ExecutedActionResult>();
        DailySummaryToolResult? dailySummary = null;
        string? previousResponseId = null;
        IReadOnlyList<LanguageModelToolOutput> toolOutputs = [];
        var executionStarted = stored.Processing.State == MessageProcessingState.Executing;
        var toolOrdinal = 0;

        try
        {
            for (var iteration = 0; iteration < settings.MaximumIterations; iteration++)
            {
                var response = await languageModelClient.CreateResponseAsync(
                    new LanguageModelRequest(
                        BuildInstructions(occurredAt ?? stored.Message.CreatedAtUtc),
                        previousResponseId is null ? stored.Message.Content : null,
                        previousResponseId,
                        toolOutputs,
                        Tools),
                    agentCancellationToken);

                if (response.ToolCalls.Count == 0)
                {
                    var assistantMessage = RequireAssistantMessage(response.OutputText);
                    if (dailySummary is not null)
                    {
                        assistantMessage = BuildAuthoritativeSummaryMessage(dailySummary);
                    }

                    var completed = new ChatMessageResult(
                        assistantMessage,
                        actions,
                        null,
                        null,
                        dailySummary);
                    Complete(stored.Processing, completed, executionStarted);
                    await repository.SaveChangesAsync(agentCancellationToken);
                    return completed;
                }

                var preparedCalls = PrepareCalls(response.ToolCalls);
                var confirmationCall = preparedCalls.FirstOrDefault(item =>
                    item.Definition.ConfirmationRequirement == ToolConfirmationRequirement.Required);
                if (confirmationCall is not null)
                {
                    var idempotencyKey = CreateIdempotencyKey(
                        stored.Message.Id, toolOrdinal, confirmationCall.Definition);
                    stored.Processing.AwaitConfirmation(
                        ToolJson.Serialize(new { response_id = response.ResponseId }),
                        confirmationCall.Definition.Name,
                        confirmationCall.Call.ArgumentsJson,
                        idempotencyKey,
                        confirmationCall.Definition.ConfirmationDescription,
                        timeProvider.GetUtcNow());
                    await repository.SaveChangesAsync(agentCancellationToken);
                    return PendingResult(null, confirmationCall.Definition.ConfirmationDescription);
                }

                if (!executionStarted)
                {
                    var first = preparedCalls[0];
                    stored.Processing.BeginExecution(
                        ToolJson.Serialize(new { response_id = response.ResponseId }),
                        first.Definition.Name,
                        first.Call.ArgumentsJson,
                        CreateIdempotencyKey(stored.Message.Id, toolOrdinal, first.Definition),
                        timeProvider.GetUtcNow());
                    await repository.SaveChangesAsync(agentCancellationToken);
                    executionStarted = true;
                }

                var outputs = new List<LanguageModelToolOutput>(preparedCalls.Count);
                foreach (var prepared in preparedCalls)
                {
                    var idempotencyKey = CreateIdempotencyKey(
                        stored.Message.Id, toolOrdinal++, prepared.Definition);
                    var outcome = await toolExecutor.ExecuteAsync(
                        new MessageToolExecutionRequest(
                            prepared.Definition.Name,
                            prepared.Call.ArgumentsJson,
                            new ToolInvocationContext(
                                stored.Message.UserId,
                                idempotencyKey,
                                stored.Message.Id,
                                null)),
                        agentCancellationToken);
                    actions.Add(new ExecutedActionResult(
                        prepared.Definition.Name,
                        outcome.IsSuccess,
                        outcome.ErrorCode));
                    outputs.Add(new LanguageModelToolOutput(prepared.Call.CallId, outcome.ResultJson));
                    dailySummary = TryGetDailySummary(prepared.Definition.Name, outcome, dailySummary);
                }

                previousResponseId = response.ResponseId;
                toolOutputs = outputs;
            }

            throw new LanguageModelUnavailableException(
                $"The language model exceeded the maximum of {settings.MaximumIterations} iterations.");
        }
        catch (ToolArgumentsValidationException exception)
        {
            var question = BuildValidationQuestion(exception.Errors);
            stored.Processing.AwaitClarification(
                ToolJson.Serialize(new { reason = "invalid_tool_arguments" }),
                question,
                timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            return PendingResult(question, null);
        }
        catch (JsonException)
        {
            const string question = "Уточните данные запроса: модель сформировала некорректные аргументы операции.";
            stored.Processing.AwaitClarification(
                ToolJson.Serialize(new { reason = "invalid_tool_json" }),
                question,
                timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            return PendingResult(question, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Fail(stored.Processing, "language_model_timeout", "Language model processing timed out.");
            await repository.SaveChangesAsync(CancellationToken.None);
            throw new LanguageModelUnavailableException("Language model processing timed out.");
        }
        catch (LanguageModelUnavailableException)
        {
            Fail(stored.Processing, "language_model_unavailable", "The language model is temporarily unavailable.");
            await repository.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static List<PreparedToolCall> PrepareCalls(
        IReadOnlyList<LanguageModelToolCall> calls)
    {
        var result = new List<PreparedToolCall>(calls.Count);
        foreach (var call in calls)
        {
            ToolDefinition definition;
            try
            {
                definition = ToolCatalog.GetRequired(call.Name);
            }
            catch (InvalidOperationException exception)
            {
                throw new LanguageModelUnavailableException(
                    $"The language model requested unregistered tool '{call.Name}'.", exception);
            }
            _ = ToolJson.DeserializeArguments(call.ArgumentsJson, definition.ArgumentsType);
            result.Add(new PreparedToolCall(call, definition));
        }

        return result;
    }

    private static string? CreateIdempotencyKey(
        Guid messageId,
        int ordinal,
        ToolDefinition definition)
    {
        return definition.Idempotency.Requirement == ToolIdempotencyRequirement.Required
            ? $"chat:{messageId:N}:{ordinal}:{definition.Name}"
            : null;
    }

    private static DailySummaryToolResult? TryGetDailySummary(
        string toolName,
        MessageToolExecutionOutcome outcome,
        DailySummaryToolResult? current)
    {
        if (!outcome.IsSuccess)
        {
            return current;
        }

        if (toolName is "add_food_to_diary" or "add_recipe_to_diary" or
            "update_diary_item" or "delete_diary_item")
        {
            return ToolJson.Deserialize<ToolExecutionResult<DiaryMutationToolResult>>(
                outcome.ResultJson).Result?.DailySummary ?? current;
        }

        if (toolName == "get_daily_summary")
        {
            return ToolJson.Deserialize<ToolExecutionResult<DailySummaryToolResult>>(
                outcome.ResultJson).Result ?? current;
        }

        return current;
    }

    private static string BuildInstructions(DateTimeOffset occurredAt)
    {
        return "You interpret one NutritionTracker user message. Use only the registered function tools. " +
            "Never invent entity identifiers, nutrition values, or backend operations. Never calculate calories or macros. " +
            "Use search tools before selecting an entity when no explicit ID is available, and never select among multiple " +
            "matches without user clarification. Treat tool output as authoritative. Keep the final answer concise and in " +
            "the user's language. Do not claim that a mutation succeeded unless its tool result says it succeeded. " +
            $"The trusted request time is {occurredAt.ToUniversalTime():O}.";
    }

    private static string BuildAuthoritativeSummaryMessage(DailySummaryToolResult summary)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Готово. Итог за {summary.Date:yyyy-MM-dd}: {summary.Consumed.Calories} ккал, " +
            $"белки {summary.Consumed.ProteinGrams} г, жиры {summary.Consumed.FatGrams} г, " +
            $"углеводы {summary.Consumed.CarbohydrateGrams} г.");
    }

    private static string BuildValidationQuestion(IReadOnlyList<ToolValidationError> errors)
    {
        var fields = string.Join(", ", errors.Select(error => error.Field).Distinct(StringComparer.Ordinal));
        return $"Уточните значения полей: {fields}.";
    }

    private static string RequireAssistantMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LanguageModelUnavailableException(
                "The language model returned neither tool calls nor assistant text.");
        }

        return value.Trim();
    }

    private static string RequireResult(string? value) =>
        value ?? throw new InvalidOperationException("The completed message has no persisted result.");

    private void Complete(
        UserMessageProcessing processing,
        ChatMessageResult result,
        bool executionStarted)
    {
        var json = ToolJson.Serialize(result);
        if (executionStarted)
        {
            processing.CompleteExecution(json, timeProvider.GetUtcNow());
        }
        else
        {
            processing.CompleteInterpretation(json, timeProvider.GetUtcNow());
        }
    }

    private void Fail(UserMessageProcessing processing, string code, string message)
    {
        if (processing.State is not MessageProcessingState.Completed and not MessageProcessingState.Failed)
        {
            processing.Fail(code, message, timeProvider.GetUtcNow());
        }
    }

    private static ChatMessageResult PendingResult(
        string? clarification,
        string? confirmation) =>
        new(clarification ?? confirmation ?? string.Empty, [], clarification, confirmation, null);

    private static void Validate(SendChatMessageCommand command)
    {
        if (command.UserId == Guid.Empty)
        {
            throw new ApplicationValidationException("The user identifier cannot be empty.", nameof(command.UserId));
        }

        ValidateText(command.Message, MaximumMessageLength, nameof(command.Message));
        ValidateText(command.ClientMessageId, MaximumClientMessageIdLength, nameof(command.ClientMessageId));
    }

    private static void ValidateText(string? value, int maximumLength, string parameterName)
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

    private sealed record PreparedToolCall(LanguageModelToolCall Call, ToolDefinition Definition);
}
