using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Nutrition;

public sealed record NutritionValues
{
    private NutritionValues()
    {
    }

    public NutritionValues(
        decimal calories,
        decimal proteinGrams,
        decimal fatGrams,
        decimal carbohydrateGrams)
    {
        Calories = DomainGuard.NonNegative(calories, nameof(calories));
        ProteinGrams = DomainGuard.NonNegative(proteinGrams, nameof(proteinGrams));
        FatGrams = DomainGuard.NonNegative(fatGrams, nameof(fatGrams));
        CarbohydrateGrams = DomainGuard.NonNegative(carbohydrateGrams, nameof(carbohydrateGrams));
    }

    public static NutritionValues Zero { get; } = new(0, 0, 0, 0);

    public decimal Calories { get; }

    public decimal ProteinGrams { get; }

    public decimal FatGrams { get; }

    public decimal CarbohydrateGrams { get; }
}
