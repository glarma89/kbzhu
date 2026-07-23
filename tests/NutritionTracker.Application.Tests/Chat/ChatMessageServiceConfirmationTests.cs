using NutritionTracker.Application.Chat;
using NutritionTracker.Application.Common;
using NutritionTracker.Application.Tools;
using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Application.Tests.Chat;

public sealed class ChatMessageServiceConfirmationTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid FoodId =
        Guid.Parse("a7a7758a-068d-45c9-b9b2-e7b7683d4631");

    [Fact]
    public async Task ConfirmationExecutesPreparedToolOnceAndReplaysPersistedResponse()
    {
        var harness = CreateHarness();
        var pending = await SendAsync(harness, "confirm-1");

        var first = await harness.Service.ContinueConfirmationAsync(
            new ContinueChatConfirmationCommand(pending.MessageId, harness.UserId, true),
            CancellationToken.None);
        var repeated = await harness.Service.ContinueConfirmationAsync(
            new ContinueChatConfirmationCommand(pending.MessageId, harness.UserId, true),
            CancellationToken.None);

        Assert.Single(first.ExecutedActions);
        Assert.Equal(first.MessageId, repeated.MessageId);
        Assert.Equal(first.AssistantMessage, repeated.AssistantMessage);
        Assert.Equal(first.ExecutedActions, repeated.ExecutedActions);
        Assert.Equal(1, harness.Executor.CallCount);
        Assert.Equal(harness.Repository.Stored!.Processing.ToolArgumentsHash,
            harness.Executor.LastRequest!.Context.Confirmation?.CanonicalArgumentsHash);
        Assert.Equal(harness.Repository.Stored.Processing.IdempotencyKey,
            harness.Executor.LastRequest.Context.IdempotencyKey);
    }

    [Fact]
    public async Task CancellationIsTerminalIdempotentAndDoesNotExecuteTool()
    {
        var harness = CreateHarness();
        var pending = await SendAsync(harness, "cancel-1");

        var first = await harness.Service.ContinueConfirmationAsync(
            new ContinueChatConfirmationCommand(pending.MessageId, harness.UserId, false),
            CancellationToken.None);
        var repeated = await harness.Service.ContinueConfirmationAsync(
            new ContinueChatConfirmationCommand(pending.MessageId, harness.UserId, false),
            CancellationToken.None);

        Assert.Equal(first.MessageId, repeated.MessageId);
        Assert.Equal(first.AssistantMessage, repeated.AssistantMessage);
        Assert.Empty(repeated.ExecutedActions);
        Assert.Equal("Operation cancelled.", first.AssistantMessage);
        Assert.Equal(0, harness.Executor.CallCount);
        Assert.Equal(MessageProcessingState.Completed, harness.Repository.Stored!.Processing.State);
    }

    [Fact]
    public async Task ConfirmationCannotReadAnotherUsersMessage()
    {
        var harness = CreateHarness();
        var pending = await SendAsync(harness, "owner-1");

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            harness.Service.ContinueConfirmationAsync(
                new ContinueChatConfirmationCommand(pending.MessageId, Guid.NewGuid(), true),
                CancellationToken.None));
        Assert.Equal(0, harness.Executor.CallCount);
    }

    private static async Task<ChatMessageResult> SendAsync(TestHarness harness, string deliveryKey)
    {
        var result = await harness.Service.SendAsync(
            new SendChatMessageCommand(harness.UserId, "Update the food", deliveryKey, UtcNow),
            CancellationToken.None);
        Assert.NotNull(result.PendingConfirmation);
        return result;
    }

    private static TestHarness CreateHarness()
    {
        var arguments = ToolJson.Serialize(new UpdateFoodArguments(
            FoodId, "Food", null, 100m, 10m, 5m, 20m, null, "Update label"));
        var repository = new FakeRepository();
        var executor = new RecordingExecutor();
        var service = new ChatMessageService(
            repository,
            new SingleResponseLanguageModelClient(new LanguageModelResponse(
                "response-1",
                null,
                [new LanguageModelToolCall("call-1", "update_food", arguments)])),
            executor,
            new StaticContextSource(),
            new ContextBuilder(ContextBuilderSettings.Default),
            ChatAgentSettings.Default,
            new FixedTimeProvider(UtcNow));
        return new TestHarness(Guid.NewGuid(), service, repository, executor);
    }

    private sealed record TestHarness(
        Guid UserId,
        ChatMessageService Service,
        FakeRepository Repository,
        RecordingExecutor Executor);

    private sealed class SingleResponseLanguageModelClient(LanguageModelResponse response)
        : ILanguageModelClient
    {
        public Task<LanguageModelResponse> CreateResponseAsync(
            LanguageModelRequest request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }

    private sealed class RecordingExecutor : IMessageToolExecutor
    {
        public int CallCount { get; private set; }
        public MessageToolExecutionRequest? LastRequest { get; private set; }

        public Task<MessageToolExecutionOutcome> ExecuteAsync(
            MessageToolExecutionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(new MessageToolExecutionOutcome(true, "{}"));
        }
    }

    private sealed class StaticContextSource : IChatContextSource
    {
        public Task<ChatContextSnapshot> GetAsync(
            ChatContextSourceRequest request,
            CancellationToken cancellationToken) => Task.FromResult(new ChatContextSnapshot(
                new ContextUserSettings("UTC", "grams", null),
                [],
                null,
                null));
    }

    private sealed class FakeRepository : IUserMessageProcessingRepository
    {
        public StoredUserMessage? Stored { get; private set; }

        public Task<StoredUserMessage?> GetByMessageIdAsync(
            Guid messageId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Stored is not null && Stored.Message.Id == messageId &&
                Stored.Message.UserId == userId ? Stored : null);

        public Task<StoredUserMessage?> GetByDeliveryKeyAsync(
            Guid userId, string deliveryKey, CancellationToken cancellationToken) =>
            Task.FromResult(Stored is not null && Stored.Message.UserId == userId &&
                Stored.Processing.DeliveryKey == deliveryKey ? Stored : null);

        public Task<StoredUserMessage> AddOrGetByDeliveryKeyAsync(
            StoredUserMessage message, CancellationToken cancellationToken)
        {
            Stored ??= message;
            return Task.FromResult(Stored);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
