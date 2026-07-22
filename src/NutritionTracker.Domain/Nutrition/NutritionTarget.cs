using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Nutrition;

public sealed class NutritionTarget
{
    private NutritionTarget()
    {
        NutritionValues = null!;
    }

    public NutritionTarget(
        Guid id,
        Guid userId,
        DateOnly validFrom,
        NutritionValues nutritionValues)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        UserId = DomainGuard.NotEmpty(userId, nameof(userId));
        ValidFrom = validFrom;
        NutritionValues = nutritionValues ?? throw new ArgumentNullException(nameof(nutritionValues));
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public DateOnly ValidFrom { get; }

    public NutritionValues NutritionValues { get; }

    public decimal Calories => NutritionValues.Calories;

    public decimal ProteinGrams => NutritionValues.ProteinGrams;

    public decimal FatGrams => NutritionValues.FatGrams;

    public decimal CarbohydrateGrams => NutritionValues.CarbohydrateGrams;
}
