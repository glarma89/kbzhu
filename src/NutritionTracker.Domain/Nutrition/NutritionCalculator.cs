using NutritionTracker.Domain.Common;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Domain.Nutrition;

public static class NutritionCalculator
{
    public const int BoundaryDecimalPlaces = 4;

    public static NutritionValues CalculateProduct(FoodProduct product, decimal weightGrams)
    {
        ArgumentNullException.ThrowIfNull(product);
        var validatedWeight = DomainGuard.Positive(weightGrams, nameof(weightGrams));
        var values = product.NutritionPer100g;

        return new NutritionValues(
            values.Calories * validatedWeight / 100m,
            values.ProteinGrams * validatedWeight / 100m,
            values.FatGrams * validatedWeight / 100m,
            values.CarbohydrateGrams * validatedWeight / 100m);
    }

    public static NutritionValues CalculateRecipe(
        Recipe recipe,
        IReadOnlyDictionary<Guid, FoodProduct> products)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(products);
        recipe.EnsureCanBeUsed();

        var calories = 0m;
        var proteinGrams = 0m;
        var fatGrams = 0m;
        var carbohydrateGrams = 0m;

        foreach (var ingredient in recipe.Ingredients)
        {
            if (!products.TryGetValue(ingredient.FoodProductId, out var product) ||
                product is null ||
                product.Id != ingredient.FoodProductId)
            {
                throw new InvalidOperationException(
                    $"Food product '{ingredient.FoodProductId}' required by the recipe is unavailable.");
            }

            var ingredientNutrition = CalculateProduct(product, ingredient.WeightGrams);
            calories += ingredientNutrition.Calories;
            proteinGrams += ingredientNutrition.ProteinGrams;
            fatGrams += ingredientNutrition.FatGrams;
            carbohydrateGrams += ingredientNutrition.CarbohydrateGrams;
        }

        return new NutritionValues(calories, proteinGrams, fatGrams, carbohydrateGrams);
    }

    public static NutritionValues CalculateRecipePer100Grams(
        Recipe recipe,
        IReadOnlyDictionary<Guid, FoodProduct> products)
    {
        var preparedWeight = GetPreparedWeight(recipe);
        var total = CalculateRecipe(recipe, products);

        return new NutritionValues(
            total.Calories / preparedWeight * 100m,
            total.ProteinGrams / preparedWeight * 100m,
            total.FatGrams / preparedWeight * 100m,
            total.CarbohydrateGrams / preparedWeight * 100m);
    }

    public static NutritionValues CalculateRecipePortion(
        Recipe recipe,
        IReadOnlyDictionary<Guid, FoodProduct> products,
        decimal consumedWeightGrams)
    {
        var validatedConsumedWeight = DomainGuard.Positive(consumedWeightGrams, nameof(consumedWeightGrams));
        var preparedWeight = GetPreparedWeight(recipe);
        var total = CalculateRecipe(recipe, products);

        return new NutritionValues(
            total.Calories * validatedConsumedWeight / preparedWeight,
            total.ProteinGrams * validatedConsumedWeight / preparedWeight,
            total.FatGrams * validatedConsumedWeight / preparedWeight,
            total.CarbohydrateGrams * validatedConsumedWeight / preparedWeight);
    }

    public static NutritionValues CalculateRecipeFraction(
        Recipe recipe,
        IReadOnlyDictionary<Guid, FoodProduct> products,
        decimal fraction)
    {
        var validatedFraction = DomainGuard.Positive(fraction, nameof(fraction));
        var total = CalculateRecipe(recipe, products);

        return new NutritionValues(
            total.Calories * validatedFraction,
            total.ProteinGrams * validatedFraction,
            total.FatGrams * validatedFraction,
            total.CarbohydrateGrams * validatedFraction);
    }

    public static NutritionValues RoundForBoundary(NutritionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new NutritionValues(
            Round(values.Calories),
            Round(values.ProteinGrams),
            Round(values.FatGrams),
            Round(values.CarbohydrateGrams));
    }

    private static decimal GetPreparedWeight(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        return recipe.TotalPreparedWeightGrams
            ?? throw new InvalidOperationException(
                "Total prepared recipe weight is required for gram-based calculations.");
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, BoundaryDecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
