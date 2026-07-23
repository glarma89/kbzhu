using NutritionTracker.Domain.Common;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Recipes;

public sealed record RecipeIngredientDefinition
{
    public RecipeIngredientDefinition(
        Guid id,
        Guid foodProductId,
        decimal weightGrams,
        NutritionValues nutritionPer100gSnapshot)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        FoodProductId = DomainGuard.NotEmpty(foodProductId, nameof(foodProductId));
        WeightGrams = DomainGuard.Positive(weightGrams, nameof(weightGrams));
        NutritionPer100gSnapshot = nutritionPer100gSnapshot
            ?? throw new ArgumentNullException(nameof(nutritionPer100gSnapshot));
    }

    public Guid Id { get; }

    public Guid FoodProductId { get; }

    public decimal WeightGrams { get; }

    public NutritionValues NutritionPer100gSnapshot { get; }
}
