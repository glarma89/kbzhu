namespace NutritionTracker.Domain.Chat;

public enum MessageProcessingState
{
    Received = 1,
    Interpreting = 2,
    AwaitingClarification = 3,
    AwaitingConfirmation = 4,
    Executing = 5,
    Completed = 6,
    Failed = 7
}
