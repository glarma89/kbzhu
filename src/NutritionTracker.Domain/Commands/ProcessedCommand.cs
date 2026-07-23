using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Commands;

public sealed class ProcessedCommand
{
    private ProcessedCommand()
    {
        IdempotencyKey = null!;
        CommandType = null!;
    }

    public ProcessedCommand(
        Guid id,
        Guid userId,
        string idempotencyKey,
        string commandType,
        Guid resultEntityId,
        DateOnly resultDate,
        DateTimeOffset createdAtUtc)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        UserId = DomainGuard.NotEmpty(userId, nameof(userId));
        IdempotencyKey = DomainGuard.RequiredText(idempotencyKey, nameof(idempotencyKey));
        CommandType = DomainGuard.RequiredText(commandType, nameof(commandType));
        ResultEntityId = DomainGuard.NotEmpty(resultEntityId, nameof(resultEntityId));
        ResultDate = resultDate;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string IdempotencyKey { get; }

    public string CommandType { get; }

    public Guid ResultEntityId { get; }

    public DateOnly ResultDate { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}
