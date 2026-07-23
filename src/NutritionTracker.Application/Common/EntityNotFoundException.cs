namespace NutritionTracker.Application.Common;

public sealed class EntityNotFoundException(string entityName, Guid entityId)
    : Exception($"{entityName} '{entityId}' was not found.")
{
    public string EntityName { get; } = entityName;

    public Guid EntityId { get; } = entityId;
}
