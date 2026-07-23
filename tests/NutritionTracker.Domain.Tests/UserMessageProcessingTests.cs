using NutritionTracker.Domain.Chat;

namespace NutritionTracker.Domain.Tests;

public sealed class UserMessageProcessingTests
{
    [Fact]
    public void NewMessageMovesThroughClarificationAndExecution()
    {
        var processing = CreateProcessing();

        processing.StartInterpreting(At(1));
        processing.AwaitClarification("{\"missing\":\"weight\"}", "How many grams?", At(2));
        processing.SupplyClarification("180 grams", At(3));
        processing.BeginExecution(
            "{\"intent\":\"add_food\"}",
            "add_food_to_diary",
            "{\"weight_grams\":180}",
            "message:key",
            At(4));
        processing.CompleteExecution("{\"diary_item_id\":\"result\"}", At(5));

        Assert.Equal(MessageProcessingState.Completed, processing.State);
        Assert.Equal("message:key", processing.IdempotencyKey);
        Assert.NotNull(processing.ExecutionResultJson);
        Assert.True(processing.HasUndeliveredResult);
    }

    [Fact]
    public void DangerousOperationWaitsForConfirmation()
    {
        var processing = CreateProcessing();
        processing.StartInterpreting(At(1));

        processing.AwaitConfirmation(
            "{\"intent\":\"update_recipe\"}",
            "update_recipe",
            "{\"recipe_id\":\"result\"}",
            "message:key",
            "Confirm replacement?",
            At(2));

        Assert.Equal(MessageProcessingState.AwaitingConfirmation, processing.State);
        Assert.Null(processing.ExecutionResultJson);

        processing.Confirm(At(3));

        Assert.Equal(MessageProcessingState.Executing, processing.State);
        Assert.Equal(At(3), processing.ConfirmedAtUtc);
    }

    [Fact]
    public void CancellationCompletesWithoutEnteringExecution()
    {
        var processing = CreateProcessing();
        processing.StartInterpreting(At(1));
        processing.AwaitClarification("{\"ambiguous\":true}", "Which product?", At(2));

        processing.Cancel("{\"status\":\"cancelled\"}", At(3));

        Assert.Equal(MessageProcessingState.Completed, processing.State);
        Assert.Null(processing.ToolName);
        Assert.Equal("{\"status\":\"cancelled\"}", processing.ExecutionResultJson);
    }

    [Fact]
    public void FailureRemembersExactRetryState()
    {
        var processing = CreateProcessing();
        processing.StartInterpreting(At(1));
        processing.BeginExecution("{}", "add_food_to_diary", "{}", "message:key", At(2));
        processing.Fail("temporary", "Temporary failure", At(3));

        processing.Retry(At(4));

        Assert.Equal(MessageProcessingState.Executing, processing.State);
        Assert.Null(processing.FailureCode);
        Assert.Equal("message:key", processing.IdempotencyKey);
    }

    [Fact]
    public void CompletedMessageCannotExecuteAgain()
    {
        var processing = CreateProcessing();
        processing.StartInterpreting(At(1));
        processing.BeginExecution("{}", "add_food_to_diary", "{}", "message:key", At(2));
        processing.CompleteExecution("{}", At(3));

        Assert.Throws<InvalidOperationException>(() => processing.BeginExecution(
            "{}", "add_food_to_diary", "{}", "other-key", At(4)));
    }

    [Fact]
    public void DeliveredResultNoLongerRequiresResponseRecovery()
    {
        var processing = CreateProcessing();
        processing.StartInterpreting(At(1));
        processing.BeginExecution("{}", "get_daily_summary", "{}", null, At(2));
        processing.CompleteExecution("{\"date\":\"2026-07-23\"}", At(3));

        processing.MarkResponseDelivered(At(4));

        Assert.False(processing.HasUndeliveredResult);
        Assert.Equal(At(4), processing.ResponseDeliveredAtUtc);
    }

    private static UserMessageProcessing CreateProcessing()
    {
        return new UserMessageProcessing(
            Guid.NewGuid(), Guid.NewGuid(), "delivery-1", DomainTestData.UtcNow);
    }

    private static DateTimeOffset At(int minute) => DomainTestData.UtcNow.AddMinutes(minute);
}
