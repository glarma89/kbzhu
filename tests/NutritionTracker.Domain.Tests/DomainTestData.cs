using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Domain.Tests;

internal static class DomainTestData
{
    public static DateTimeOffset UtcNow { get; } = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    public static NutritionValues CreateNutritionValues()
    {
        return new NutritionValues(120m, 5m, 3m, 20m);
    }

    public static FoodProduct CreateFoodProduct(Guid? userId = null)
    {
        return new FoodProduct(
            Guid.NewGuid(),
            userId,
            "Potato",
            null,
            CreateNutritionValues(),
            2.2m,
            "Test",
            true,
            UtcNow,
            UtcNow);
    }

    public static Recipe CreateRecipe(bool addIngredient)
    {
        var recipe = new Recipe(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Salad",
            null,
            500m,
            3,
            false,
            UtcNow,
            UtcNow);

        if (addIngredient)
        {
            recipe.AddIngredient(Guid.NewGuid(), Guid.NewGuid(), 100m, UtcNow);
        }

        return recipe;
    }
}
