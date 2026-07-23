namespace NutritionTracker.Application.Chat;

public interface IUserMessageInterpreter
{
    Task<MessageInterpretation> InterpretAsync(
        MessageInterpretationRequest request,
        CancellationToken cancellationToken);
}

public interface IMessageToolExecutor
{
    Task<MessageToolExecutionOutcome> ExecuteAsync(
        MessageToolExecutionRequest request,
        CancellationToken cancellationToken);
}

public interface IUserMessageProcessingRepository
{
    Task<StoredUserMessage?> GetByMessageIdAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<StoredUserMessage?> GetByDeliveryKeyAsync(
        Guid userId,
        string deliveryKey,
        CancellationToken cancellationToken);

    Task<StoredUserMessage> AddOrGetByDeliveryKeyAsync(
        StoredUserMessage message,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IUserMessageProcessingService
{
    Task<MessageProcessingResult> ReceiveAsync(
        ReceiveUserMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageProcessingResult> ProcessAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<MessageProcessingResult> SupplyClarificationAsync(
        Guid messageId,
        Guid userId,
        string response,
        CancellationToken cancellationToken);

    Task<MessageProcessingResult> ConfirmAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<MessageProcessingResult> CancelAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<MessageProcessingResult> RetryAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<MessageProcessingResult> MarkResponseDeliveredAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken);
}
