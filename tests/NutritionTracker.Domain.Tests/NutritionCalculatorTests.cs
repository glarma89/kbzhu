using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Domain.Tests;

public sealed class NutritionCalculatorTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProductAt100GramsEqualsStoredValues()
    {
        var product = CreateProduct(new NutritionValues(80m, 2m, 0.1m, 17m));

        var result = NutritionCalculator.CalculateProduct(product, 100m);

        Assert.Equal(product.NutritionPer100g, result);
    }

    [Fact]
    public void ProductAt180GramsUsesProportionalValues()
    {
        var product = CreateProduct(new NutritionValues(80m, 2m, 0.1m, 17m));

        var result = NutritionCalculator.CalculateProduct(product, 180m);

        Assert.Equal(new NutritionValues(144m, 3.6m, 0.18m, 30.6m), result);
    }

    [Fact]
    public void ProductAtHalfGramPreservesPrecision()
    {
        var product = CreateProduct(new NutritionValues(80m, 2m, 0.1m, 17m));

        var result = NutritionCalculator.CalculateProduct(product, 0.5m);

        Assert.Equal(new NutritionValues(0.4m, 0.01m, 0.0005m, 0.085m), result);
    }

    [Fact]
    public void RecipeSumsAllIngredientValues()
    {
        var (recipe, products) = CreateRecipe(320m);

        var result = NutritionCalculator.CalculateRecipe(recipe, products);

        Assert.Equal(new NutritionValues(230m, 10m, 1.75m, 44.5m), result);
    }

    [Fact]
    public void RecipePer100GramsUsesPreparedWeight()
    {
        var (recipe, products) = CreateRecipe(320m);

        var result = NutritionCalculator.CalculateRecipePer100Grams(recipe, products);

        Assert.Equal(new NutritionValues(71.875m, 3.125m, 0.546875m, 13.90625m), result);
    }

    [Fact]
    public void RecipePortionUsesConsumedWeight()
    {
        var (recipe, products) = CreateRecipe(320m);

        var result = NutritionCalculator.CalculateRecipePortion(recipe, products, 160m);

        Assert.Equal(new NutritionValues(115m, 5m, 0.875m, 22.25m), result);
    }

    [Fact]
    public void HalfRecipeUsesHalfOfTotalNutrition()
    {
        var (recipe, products) = CreateRecipe(320m);

        var result = NutritionCalculator.CalculateRecipeFraction(recipe, products, 0.5m);

        Assert.Equal(new NutritionValues(115m, 5m, 0.875m, 22.25m), result);
    }

    [Fact]
    public void QuarterRecipeUsesQuarterOfTotalNutrition()
    {
        var (recipe, products) = CreateRecipe(320m);

        var result = NutritionCalculator.CalculateRecipeFraction(recipe, products, 0.25m);

        Assert.Equal(new NutritionValues(57.5m, 2.5m, 0.4375m, 11.125m), result);
    }

    [Fact]
    public void PersistedRecipeVersionFractionUsesIngredientSnapshots()
    {
        var (recipe, _) = CreateRecipe(320m);

        var result = NutritionCalculator.CalculateRecipeFraction(recipe.CurrentVersion, 0.5m);

        Assert.Equal(new NutritionValues(115m, 5m, 0.875m, 22.25m), result);
    }

    [Fact]
    public void SnapshotWeightRecalculationPreservesOriginalNutritionRatio()
    {
        var snapshot = new NutritionValues(80m, 2m, 0.1m, 17m);

        var result = NutritionCalculator.RecalculateSnapshotWeight(snapshot, 100m, 150m);

        Assert.Equal(new NutritionValues(120m, 3m, 0.15m, 25.5m), result);
    }

    [Fact]
    public void GramBasedRecipeCalculationsRequirePreparedWeight()
    {
        var (recipe, products) = CreateRecipe(null);

        Assert.Throws<InvalidOperationException>(
            () => NutritionCalculator.CalculateRecipePer100Grams(recipe, products));
        Assert.Throws<InvalidOperationException>(
            () => NutritionCalculator.CalculateRecipePortion(recipe, products, 100m));
    }

    [Fact]
    public void BoundaryRoundingUsesFourPlacesAwayFromZero()
    {
        var values = new NutritionValues(1.23445m, 2.34555m, 0.00005m, 9.87654m);

        var result = NutritionCalculator.RoundForBoundary(values);

        Assert.Equal(new NutritionValues(1.2345m, 2.3456m, 0.0001m, 9.8765m), result);
    }

    [Fact]
    public void VerySmallValuesAreNotRoundedDuringCalculation()
    {
        var product = CreateProduct(new NutritionValues(0.0001m, 0.0001m, 0.0001m, 0.0001m));

        var calculated = NutritionCalculator.CalculateProduct(product, 0.5m);
        var rounded = NutritionCalculator.RoundForBoundary(calculated);

        Assert.Equal(new NutritionValues(0.0000005m, 0.0000005m, 0.0000005m, 0.0000005m), calculated);
        Assert.Equal(NutritionValues.Zero, rounded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveWeightsAreRejected(int invalidWeight)
    {
        var product = CreateProduct(new NutritionValues(80m, 2m, 0.1m, 17m));
        var (recipe, products) = CreateRecipe(320m);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => NutritionCalculator.CalculateProduct(product, invalidWeight));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NutritionCalculator.CalculateRecipePortion(recipe, products, invalidWeight));
    }

    private static FoodProduct CreateProduct(NutritionValues nutrition)
    {
        return new FoodProduct(
            Guid.NewGuid(),
            null,
            "Test product",
            null,
            nutrition,
            null,
            "Unit test",
            true,
            UtcNow,
            UtcNow);
    }

    private static (Recipe Recipe, IReadOnlyDictionary<Guid, FoodProduct> Products) CreateRecipe(
        decimal? preparedWeightGrams)
    {
        var potato = CreateProduct(new NutritionValues(80m, 2m, 0.1m, 17m));
        var yogurt = CreateProduct(new NutritionValues(60m, 10m, 3m, 4m));
        var recipe = new Recipe(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Potato salad",
            null,
            preparedWeightGrams,
            [
                new RecipeIngredientDefinition(Guid.NewGuid(), potato.Id, 250m, potato.NutritionPer100g),
                new RecipeIngredientDefinition(Guid.NewGuid(), yogurt.Id, 50m, yogurt.NutritionPer100g)
            ],
            "Initial",
            "Unit test",
            UtcNow);

        IReadOnlyDictionary<Guid, FoodProduct> products = new Dictionary<Guid, FoodProduct>
        {
            [potato.Id] = potato,
            [yogurt.Id] = yogurt
        };

        return (recipe, products);
    }
}
