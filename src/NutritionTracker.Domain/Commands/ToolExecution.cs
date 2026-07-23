using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Commands;

public sealed class ToolExecution
{
    private ToolExecution()
    {
        IdempotencyKey = null!;
        ToolName = null!;
        ArgumentsHash = null!;
        ResultJson = null!;
    }

    public ToolExecution(
        Guid id,
        Guid userId,
        string idempotencyKey,
        string toolName,
        string argumentsHash,
        string resultJson,
        DateTimeOffset createdAtUtc)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        UserId = DomainGuard.NotEmpty(userId, nameof(userId));
        IdempotencyKey = DomainGuard.RequiredText(idempotencyKey, nameof(idempotencyKey));
        ToolName = DomainGuard.RequiredText(toolName, nameof(toolName));
        ArgumentsHash = DomainGuard.RequiredText(argumentsHash, nameof(argumentsHash));
        ResultJson = DomainGuard.RequiredText(resultJson, nameof(resultJson));
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; }
    public Guid UserId { get; }
    public string IdempotencyKey { get; }
    public string ToolName { get; }
    public string ArgumentsHash { get; }
    public string ResultJson { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}
