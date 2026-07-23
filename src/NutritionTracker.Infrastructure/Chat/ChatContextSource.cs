using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Application.Chat;
using NutritionTracker.Application.Common;
using NutritionTracker.Domain.Chat;
using NutritionTracker.Infrastructure.Persistence;

namespace NutritionTracker.Infrastructure.Chat;

internal sealed class ChatContextSource(NutritionDbContext context) : IChatContextSource
{
    private const int MaximumCandidateMessages = 24;

    public async Task<ChatContextSnapshot> GetAsync(
        ChatContextSourceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.UserId,
            cancellationToken)
            ?? throw new EntityNotFoundException("UserProfile", request.UserId);
        var localDate = ResolveLocalDate(request.TrustedRequestTime, user.TimeZone);
        var target = await context.NutritionTargets.AsNoTracking()
            .Where(item => item.UserId == request.UserId && item.ValidFrom <= localDate)
            .OrderByDescending(item => item.ValidFrom)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var recentMessages = await context.ChatMessages.FromSqlInterpolated($$"""
                SELECT message.*
                FROM ChatMessages AS message
                WHERE message.UserId = {{request.UserId}}
                  AND message.Id <> {{request.CurrentMessageId}}
                ORDER BY message.CreatedAtUtc DESC, message.Id DESC
                LIMIT {{MaximumCandidateMessages}}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var recentMessageIds = recentMessages.Select(message => message.Id).ToArray();
        var processingByMessageId = await context.UserMessageProcessings.AsNoTracking()
            .Where(processing => recentMessageIds.Contains(processing.MessageId))
            .ToDictionaryAsync(processing => processing.MessageId, cancellationToken);
        var rows = recentMessages.Select(message =>
        {
            var processing = processingByMessageId[message.Id];
            return new ContextRow(
                message.Id,
                message.Content,
                message.CreatedAtUtc,
                processing.State,
                processing.PendingQuestion,
                processing.ToolName,
                processing.ToolArgumentsJson,
                processing.ExecutionResultJson);
        }).ToList();

        var pendingRow = rows.FirstOrDefault(row =>
            row.State is MessageProcessingState.AwaitingClarification or
                MessageProcessingState.AwaitingConfirmation);
        if (pendingRow is null)
        {
            var pendingMessage = await context.ChatMessages.FromSqlInterpolated($$"""
                    SELECT message.*
                    FROM ChatMessages AS message
                    INNER JOIN UserMessageProcessings AS processing
                        ON processing.MessageId = message.Id
                    WHERE message.UserId = {{request.UserId}}
                      AND message.Id <> {{request.CurrentMessageId}}
                      AND processing.State IN ('AwaitingClarification', 'AwaitingConfirmation')
                    ORDER BY message.CreatedAtUtc DESC, message.Id DESC
                    LIMIT 1
                    """)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
            if (pendingMessage is not null)
            {
                var pendingProcessing = await context.UserMessageProcessings.AsNoTracking()
                    .SingleAsync(
                        processing => processing.MessageId == pendingMessage.Id,
                        cancellationToken);
                pendingRow = new ContextRow(
                    pendingMessage.Id,
                    pendingMessage.Content,
                    pendingMessage.CreatedAtUtc,
                    pendingProcessing.State,
                    pendingProcessing.PendingQuestion,
                    pendingProcessing.ToolName,
                    pendingProcessing.ToolArgumentsJson,
                    pendingProcessing.ExecutionResultJson);
            }
        }

        var messages = rows
            .OrderBy(row => row.CreatedAtUtc)
            .ThenBy(row => row.MessageId)
            .SelectMany(MapConversationMessages)
            .ToArray();
        var settings = new ContextUserSettings(
            user.TimeZone,
            "grams",
            target is null
                ? null
                : new ContextNutritionTarget(
                    target.ValidFrom,
                    target.Calories,
                    target.ProteinGrams,
                    target.FatGrams,
                    target.CarbohydrateGrams));
        var pending = pendingRow is null
            ? null
            : new ContextPendingState(
                pendingRow.MessageId,
                pendingRow.State,
                pendingRow.Content,
                pendingRow.PendingQuestion!,
                pendingRow.ToolName,
                pendingRow.ToolArgumentsJson);

        return new ChatContextSnapshot(settings, messages, pending, null);
    }

    private static IEnumerable<ContextConversationMessage> MapConversationMessages(ContextRow row)
    {
        yield return new ContextConversationMessage(
            row.MessageId,
            ChatRole.User,
            row.Content,
            row.CreatedAtUtc);

        var assistantMessage = TryReadAssistantMessage(row.ExecutionResultJson);
        if (assistantMessage is not null)
        {
            yield return new ContextConversationMessage(
                row.MessageId,
                ChatRole.Assistant,
                assistantMessage,
                row.CreatedAtUtc);
        }
    }

    private static string? TryReadAssistantMessage(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(resultJson);
            return document.RootElement.TryGetProperty("assistant_message", out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString())
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateOnly ResolveLocalDate(DateTimeOffset requestTime, string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(requestTime, timeZone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ApplicationValidationException(
                $"The user's time zone '{timeZoneId}' is not available.", nameof(timeZoneId));
        }
        catch (InvalidTimeZoneException)
        {
            throw new ApplicationValidationException(
                $"The user's time zone '{timeZoneId}' is invalid.", nameof(timeZoneId));
        }
    }

    private sealed record ContextRow(
        Guid MessageId,
        string Content,
        DateTimeOffset CreatedAtUtc,
        MessageProcessingState State,
        string? PendingQuestion,
        string? ToolName,
        string? ToolArgumentsJson,
        string? ExecutionResultJson);
}
