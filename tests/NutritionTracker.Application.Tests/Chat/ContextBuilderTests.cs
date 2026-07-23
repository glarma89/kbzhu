using NutritionTracker.Application.Chat;
using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Application.Tests.Chat;

public sealed class ContextBuilderTests
{
    private static readonly Guid CurrentMessageId =
        Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildIncludesRequiredContextAndSelectsRecentRelevantMessagesDeterministically()
    {
        var pendingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshot = new ChatContextSnapshot(
            new ContextUserSettings(
                "Asia/Jerusalem",
                "grams",
                new ContextNutritionTarget(new DateOnly(2026, 7, 1), 2100m, 140m, 70m, 220m)),
            [
                Message("Old banana detail", -6),
                Message("Apple was discussed earlier", -5),
                Message("Unrelated exercise", -4),
                Message("Another unrelated note", -3),
                Message("Latest assistant question", -2, ChatRole.Assistant),
                Message("Latest user answer", -1)
            ],
            new ContextPendingState(
                pendingId,
                MessageProcessingState.AwaitingClarification,
                "Добавь яблоко",
                "Какой вес?",
                null,
                null),
            new ContextSummary("Target was once 9999 calories.", UtcNow.AddDays(-1)));
        var request = new ContextBuildRequest(
            "SYSTEM",
            CurrentMessageId,
            "Add the apple now",
            snapshot,
            [new LanguageModelToolOutput("call-1", "{\"result\":\"current database value\"}")],
            IncludeConversationMessages: true);
        var builder = new ContextBuilder(new ContextBuilderSettings(10_000, 3, 2));

        var first = builder.Build(request);
        var second = builder.Build(request);

        Assert.Equal(first.Instructions, second.Instructions);
        Assert.Equal(first.Messages, second.Messages);
        Assert.Equal(first.ToolOutputs, second.ToolOutputs);
        Assert.Equal(first.CharacterCount, second.CharacterCount);
        Assert.Contains("SYSTEM", first.Instructions, StringComparison.Ordinal);
        Assert.Contains("Asia/Jerusalem", first.Instructions, StringComparison.Ordinal);
        Assert.Contains("2100", first.Instructions, StringComparison.Ordinal);
        Assert.Contains("AwaitingClarification", first.Instructions, StringComparison.Ordinal);
        Assert.Contains("non-authoritative", first.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(request.ToolOutputs, first.ToolOutputs);
        Assert.Equal("Add the apple now", first.Messages[^1].Content);
        Assert.Contains(first.Messages, message => message.Content == "Apple was discussed earlier");
        Assert.Contains(first.Messages, message => message.Content == "Latest assistant question");
        Assert.Contains(first.Messages, message => message.Content == "Latest user answer");
        Assert.DoesNotContain(first.Messages, message => message.Content == "Old banana detail");
        Assert.DoesNotContain(first.Messages, message => message.Content == "Unrelated exercise");
        Assert.True(first.CharacterCount <= 10_000);
    }

    [Fact]
    public void BuildDropsOptionalHistoryBeforeExceedingSizeLimit()
    {
        var snapshotWithoutHistory = Snapshot([]);
        var unlimited = new ContextBuilder(new ContextBuilderSettings(10_000, 4, 1));
        var essential = unlimited.Build(Request(snapshotWithoutHistory));
        var snapshotWithHistory = Snapshot(
            [Message("This optional history is too long for the remaining budget", -1)]);
        var constrained = new ContextBuilder(
            new ContextBuilderSettings(essential.CharacterCount + 5, 4, 1));

        var result = constrained.Build(Request(snapshotWithHistory));

        Assert.Single(result.Messages);
        Assert.Equal("current", result.Messages[0].Content);
        Assert.True(result.CharacterCount <= essential.CharacterCount + 5);
    }

    [Fact]
    public void BuildRejectsAContextWhenRequiredDataAloneExceedsTheLimit()
    {
        var builder = new ContextBuilder(new ContextBuilderSettings(10, 0, 0));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Build(Request(Snapshot([]))));

        Assert.Contains("exceed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ContextBuildRequest Request(ChatContextSnapshot snapshot) => new(
        "SYSTEM",
        CurrentMessageId,
        "current",
        snapshot,
        [],
        IncludeConversationMessages: true);

    private static ChatContextSnapshot Snapshot(
        IReadOnlyList<ContextConversationMessage> messages) => new(
        new ContextUserSettings("UTC", "grams", null),
        messages,
        null,
        null);

    private static ContextConversationMessage Message(
        string content,
        int minuteOffset,
        ChatRole role = ChatRole.User) => new(
        Guid.NewGuid(),
        role,
        content,
        UtcNow.AddMinutes(minuteOffset));
}
