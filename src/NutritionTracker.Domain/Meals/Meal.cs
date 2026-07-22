using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Meals;

public sealed class Meal
{
    private Meal()
    {
    }

    public Meal(
        Guid id,
        Guid userId,
        DateTimeOffset occurredAt,
        MealType mealType,
        string? notes,
        DateTimeOffset createdAtUtc)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        UserId = DomainGuard.NotEmpty(userId, nameof(userId));
        OccurredAt = occurredAt;

        if (!Enum.IsDefined(mealType))
        {
            throw new ArgumentOutOfRangeException(nameof(mealType), mealType, "The meal type is invalid.");
        }

        MealType = mealType;
        Notes = DomainGuard.OptionalText(notes);
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public DateTimeOffset OccurredAt { get; }

    public MealType MealType { get; }

    public string? Notes { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}
