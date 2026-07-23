using NutritionTracker.Domain.Common;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Recipes;

public sealed class RecipeVersionIngredient
{
    private RecipeVersionIngredient()
    {
        NutritionPer100gSnapshot = null!;
    }

    internal RecipeVersionIngredient(
        Guid recipeId,
        int recipeVersion,
        Guid foodProductId,
        decimal weightGrams,
        NutritionValues nutritionPer100gSnapshot)
    {
        RecipeId = DomainGuard.NotEmpty(recipeId, nameof(recipeId));
        if (recipeVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recipeVersion), recipeVersion, "The recipe version must be positive.");
        }

        RecipeVersion = recipeVersion;
        FoodProductId = DomainGuard.NotEmpty(foodProductId, nameof(foodProductId));
        WeightGrams = DomainGuard.Positive(weightGrams, nameof(weightGrams));
        NutritionPer100gSnapshot = nutritionPer100gSnapshot
            ?? throw new ArgumentNullException(nameof(nutritionPer100gSnapshot));
    }

    public Guid RecipeId { get; }

    public int RecipeVersion { get; }

    public Guid FoodProductId { get; }

    public decimal WeightGrams { get; }

    public NutritionValues NutritionPer100gSnapshot { get; }
}
