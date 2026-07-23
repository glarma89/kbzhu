using NutritionTracker.Application.Chat;
using NutritionTracker.Application.Tools;
using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Application.Tests.Chat;

public sealed class UserMessageProcessingServiceTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid FoodId = Guid.Parse("a7a7758a-068d-45c9-b9b2-e7b7683d4631");
    private static readonly Guid RecipeId = Guid.Parse("d2261083-359d-42fd-84c5-c1586d1ec4a4");

    [Fact]
    public async Task ExactFoodWithSingleMatchExecutesWithoutQuestion()
    {
        var interpretation = ReadyFood(180m, "exact");
        var harness = CreateHarness(_ => interpretation);
        var received = await ReceiveAsync(harness, "Добавь 180 г картошки", "delivery-1");

        var result = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.Completed, result.State);
        Assert.Null(result.PendingQuestion);
        Assert.StartsWith($"message:{received.MessageId:N}:", result.IdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(1, harness.Executor.MutationCount);
        Assert.Equal(180m, GetDecimal(harness.Executor.LastRequest!.ArgumentsJson, "weight_grams"));
    }

    [Theory]
    [InlineData("multiple_matches", "Какую именно картошку выбрать?")]
    [InlineData("not_found", "Продукт не найден. Создать новый?")]
    [InlineData("missing_weight", "Сколько граммов добавить?")]
    public async Task CriticalAmbiguityWaitsForClarificationWithoutExecuting(
        string reason,
        string question)
    {
        var harness = CreateHarness(_ => new MessageInterpretation(
            InterpretationDisposition.NeedsClarification,
            $"{{\"reason\":\"{reason}\"}}",
            UserQuestion: question));
        var received = await ReceiveAsync(harness, "Неоднозначное сообщение", $"delivery-{reason}");

        var result = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.AwaitingClarification, result.State);
        Assert.Equal(question, result.PendingQuestion);
        Assert.Equal(0, harness.Executor.CallCount);
        Assert.Null(result.ExecutionResultJson);
    }

    [Fact]
    public async Task NumericApproximateWeightIsEnoughAndDoesNotAskExtraQuestion()
    {
        var harness = CreateHarness(_ => ReadyFood(180m, "approximate"));
        var received = await ReceiveAsync(harness, "Добавь примерно 180 г картошки", "delivery-approximate");

        var result = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.Completed, result.State);
        Assert.Null(result.PendingQuestion);
        Assert.Equal(1, harness.Executor.MutationCount);
    }

    [Fact]
    public async Task SameMorningPortionExecutesWithResolvedPersistedWeight()
    {
        var harness = CreateHarness(_ => ReadyFood(140m, "same_as_morning"));
        var received = await ReceiveAsync(
            harness, "Добавь такую же порцию, как утром", "delivery-same-portion");

        var result = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.Completed, result.State);
        Assert.Equal(140m, GetDecimal(harness.Executor.LastRequest!.ArgumentsJson, "weight_grams"));
    }

    [Fact]
    public async Task HalfYesterdaySaladExecutesOnlyAfterReferenceWasResolved()
    {
        var arguments = $$"""
            {
              "recipe_id": "{{RecipeId}}",
              "weight_grams": 90,
              "occurred_at": "2026-07-23T12:00:00Z",
              "meal_type": "lunch",
              "user_intent": "Половина вчерашней порции салата."
            }
            """;
        var harness = CreateHarness(_ => new MessageInterpretation(
            InterpretationDisposition.ReadyToExecute,
            "{\"intent\":\"resolved_previous_recipe_portion\",\"fraction\":0.5}",
            "add_recipe_to_diary",
            arguments));
        var received = await ReceiveAsync(
            harness, "Добавь половину вчерашнего салата", "delivery-half-salad");

        var result = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.Completed, result.State);
        Assert.Equal(90m, GetDecimal(harness.Executor.LastRequest!.ArgumentsJson, "weight_grams"));
    }

    [Fact]
    public async Task RecipeIngredientReplacementRequiresConfirmationBeforeExecution()
    {
        var arguments = $$"""
            {
              "recipe_id": "{{RecipeId}}",
              "expected_version": 2,
              "name": "Салат",
              "ingredients": [
                { "food_product_id": "{{FoodId}}", "weight_grams": 100 }
              ],
              "change_reason": "Заменить майонез на йогурт",
              "user_intent": "Пользователь попросил заменить ингредиент."
            }
            """;
        var harness = CreateHarness(_ => new MessageInterpretation(
            InterpretationDisposition.ReadyToExecute,
            "{\"intent\":\"replace_recipe_ingredient\"}",
            "update_recipe",
            arguments,
            "Подтвердить замену майонеза на йогурт?"));
        var received = await ReceiveAsync(
            harness, "Замени майонез в рецепте на йогурт", "delivery-replace");

        var awaiting = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.AwaitingConfirmation, awaiting.State);
        Assert.Equal(0, harness.Executor.CallCount);
        Assert.NotNull(awaiting.IdempotencyKey);

        var completed = await harness.Service.ConfirmAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.Completed, completed.State);
        Assert.NotNull(harness.Executor.LastRequest!.Context.Confirmation);
        Assert.Equal(1, harness.Executor.MutationCount);
    }

    [Fact]
    public async Task UserCancellationCompletesWithoutMutation()
    {
        var harness = CreateHarness(_ => new MessageInterpretation(
            InterpretationDisposition.NeedsClarification,
            "{\"reason\":\"multiple_matches\"}",
            UserQuestion: "Какой продукт?"));
        var received = await ReceiveAsync(harness, "Добавь картошку", "delivery-cancel");
        _ = await harness.Service.ProcessAsync(received.MessageId, harness.UserId, CancellationToken.None);

        var cancelled = await harness.Service.CancelAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.Completed, cancelled.State);
        Assert.Equal("{\"status\":\"cancelled\"}", cancelled.ExecutionResultJson);
        Assert.Equal(0, harness.Executor.CallCount);
    }

    [Fact]
    public async Task DuplicateDeliveryReturnsOriginalProcessingAndDoesNotCreateSecondItem()
    {
        var harness = CreateHarness(_ => ReadyFood(180m, "exact"));
        var first = await ReceiveAsync(harness, "Добавь 180 г картошки", "same-delivery");
        _ = await harness.Service.ProcessAsync(first.MessageId, harness.UserId, CancellationToken.None);

        var duplicate = await harness.Service.ReceiveAsync(
            new ReceiveUserMessageCommand(
                Guid.NewGuid(), harness.UserId, "same-delivery", "Добавь 180 г картошки"),
            CancellationToken.None);
        var replay = await harness.Service.ProcessAsync(
            duplicate.MessageId, harness.UserId, CancellationToken.None);

        Assert.True(duplicate.IsDuplicateDelivery);
        Assert.Equal(first.MessageId, duplicate.MessageId);
        Assert.Equal(MessageProcessingState.Completed, replay.State);
        Assert.Single(harness.Repository.Messages);
        Assert.Equal(1, harness.Executor.CallCount);
        Assert.Equal(1, harness.Executor.MutationCount);
    }

    [Fact]
    public async Task CompletedToolResultCanBeDeliveredAfterResponseFailureWithoutReexecution()
    {
        var harness = CreateHarness(_ => ReadyFood(180m, "exact"));
        var received = await ReceiveAsync(harness, "Добавь 180 г картошки", "delivery-recovery");
        var completed = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        var recovered = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.True(completed.HasUndeliveredResult);
        Assert.Equal(completed.ExecutionResultJson, recovered.ExecutionResultJson);
        Assert.Equal(1, harness.Executor.CallCount);

        var delivered = await harness.Service.MarkResponseDeliveredAsync(
            received.MessageId, harness.UserId, CancellationToken.None);
        Assert.False(delivered.HasUndeliveredResult);
    }

    [Fact]
    public async Task RetryAfterExecutorFailureReusesTheSameIdempotencyKey()
    {
        var harness = CreateHarness(_ => ReadyFood(180m, "exact"));
        harness.Executor.FailNextCall = true;
        var received = await ReceiveAsync(harness, "Добавь 180 г картошки", "delivery-retry");

        var failed = await harness.Service.ProcessAsync(
            received.MessageId, harness.UserId, CancellationToken.None);
        var retried = await harness.Service.RetryAsync(
            received.MessageId, harness.UserId, CancellationToken.None);

        Assert.Equal(MessageProcessingState.Failed, failed.State);
        Assert.Equal(MessageProcessingState.Completed, retried.State);
        Assert.Equal(failed.IdempotencyKey, retried.IdempotencyKey);
        Assert.Equal(2, harness.Executor.CallCount);
        Assert.Equal(1, harness.Executor.MutationCount);
    }

    private static MessageInterpretation ReadyFood(decimal weightGrams, string resolution) => new(
        InterpretationDisposition.ReadyToExecute,
        $"{{\"intent\":\"add_food\",\"resolution\":\"{resolution}\"}}",
        "add_food_to_diary",
        $$"""
          {
            "food_product_id": "{{FoodId}}",
            "weight_grams": {{weightGrams}},
            "occurred_at": "2026-07-23T12:00:00Z",
            "meal_type": "lunch",
            "user_intent": "Пользователь попросил добавить продукт."
          }
          """);

    private static TestHarness CreateHarness(
        Func<MessageInterpretationRequest, MessageInterpretation> interpretation)
    {
        var repository = new FakeRepository();
        var executor = new IdempotentExecutor();
        var userId = Guid.NewGuid();
        var service = new UserMessageProcessingService(
            repository,
            new StubInterpreter(interpretation),
            executor,
            new FixedTimeProvider(UtcNow));
        return new TestHarness(userId, service, repository, executor);
    }

    private static Task<MessageProcessingResult> ReceiveAsync(
        TestHarness harness,
        string content,
        string deliveryKey)
    {
        return harness.Service.ReceiveAsync(
            new ReceiveUserMessageCommand(Guid.NewGuid(), harness.UserId, deliveryKey, content),
            CancellationToken.None);
    }

    private static decimal GetDecimal(string json, string property)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.GetProperty(property).GetDecimal();
    }

    private sealed record TestHarness(
        Guid UserId,
        UserMessageProcessingService Service,
        FakeRepository Repository,
        IdempotentExecutor Executor);

    private sealed class StubInterpreter(
        Func<MessageInterpretationRequest, MessageInterpretation> interpretation)
        : IUserMessageInterpreter
    {
        public Task<MessageInterpretation> InterpretAsync(
            MessageInterpretationRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(interpretation(request));
        }
    }

    private sealed class IdempotentExecutor : IMessageToolExecutor
    {
        private readonly Dictionary<string, MessageToolExecutionOutcome> _results = [];

        public int CallCount { get; private set; }

        public int MutationCount => _results.Count;

        public bool FailNextCall { get; set; }

        public MessageToolExecutionRequest? LastRequest { get; private set; }

        public Task<MessageToolExecutionOutcome> ExecuteAsync(
            MessageToolExecutionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            var key = request.Context.IdempotencyKey ?? $"read:{CallCount}";
            if (_results.TryGetValue(key, out var existing))
            {
                return Task.FromResult(existing);
            }

            var result = new MessageToolExecutionOutcome(
                true,
                "{\"diary_item_id\":\"db91ddec-06c7-4c53-85f2-e01124bb680f\"}");
            _results.Add(key, result);
            if (FailNextCall)
            {
                FailNextCall = false;
                throw new InvalidOperationException("Response lost after the idempotent tool committed.");
            }

            return Task.FromResult(result);
        }
    }

    private sealed class FakeRepository : IUserMessageProcessingRepository
    {
        public Dictionary<Guid, StoredUserMessage> Messages { get; } = [];

        public Task<StoredUserMessage?> GetByMessageIdAsync(
            Guid messageId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            Messages.TryGetValue(messageId, out var stored);
            return Task.FromResult(stored?.Message.UserId == userId ? stored : null);
        }

        public Task<StoredUserMessage?> GetByDeliveryKeyAsync(
            Guid userId,
            string deliveryKey,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Messages.Values.SingleOrDefault(item =>
                item.Message.UserId == userId && item.Processing.DeliveryKey == deliveryKey));
        }

        public Task<StoredUserMessage> AddOrGetByDeliveryKeyAsync(
            StoredUserMessage message,
            CancellationToken cancellationToken)
        {
            var existing = Messages.Values.SingleOrDefault(item =>
                item.Message.UserId == message.Message.UserId &&
                item.Processing.DeliveryKey == message.Processing.DeliveryKey);
            if (existing is not null)
            {
                return Task.FromResult(existing);
            }

            Messages.Add(message.Message.Id, message);
            return Task.FromResult(message);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
