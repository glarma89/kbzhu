using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests;

public sealed class MealItemTests
{
    [Fact]
    public void ConstructorRejectsMissingProductAndRecipe()
    {
        Assert.Throws<ArgumentException>(
            () => new MealItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                100,
                NutritionValues.Zero,
                null));
    }

    [Fact]
    public void ConstructorRejectsProductAndRecipeTogether()
    {
        var product = DomainTestData.CreateFoodProduct();
        var recipe = DomainTestData.CreateRecipe(addIngredient: true);

        Assert.Throws<ArgumentException>(
            () => new MealItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                product,
                recipe,
                100,
                NutritionValues.Zero,
                null));
    }

    [Fact]
    public void ConstructorRejectsEmptyRecipe()
    {
        var recipe = DomainTestData.CreateRecipe(addIngredient: false);

        Assert.Throws<InvalidOperationException>(
            () => new MealItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                recipe,
                100,
                NutritionValues.Zero,
                null));
    }

    [Fact]
    public void WeightMustBePositive()
    {
        var product = DomainTestData.CreateFoodProduct();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MealItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                product,
                null,
                0,
                NutritionValues.Zero,
                null));
    }

    [Fact]
    public void NutritionSnapshotIsRequired()
    {
        var product = DomainTestData.CreateFoodProduct();

        Assert.Throws<ArgumentNullException>(
            () => new MealItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                product,
                null,
                100,
                null!,
                null));
    }

    [Fact]
    public void RecipeItemStoresRecipeVersionAndNutritionSnapshot()
    {
        var recipe = DomainTestData.CreateRecipe(addIngredient: true);
        var snapshot = new NutritionValues(240m, 10m, 6m, 40m);

        var mealItem = new MealItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            recipe,
            200m,
            snapshot,
            Guid.NewGuid());

        Assert.Null(mealItem.FoodProductId);
        Assert.Equal(recipe.Id, mealItem.RecipeId);
        Assert.Equal(recipe.Version, mealItem.RecipeVersion);
        Assert.Equal(snapshot, mealItem.NutritionSnapshot);
        Assert.Equal(240m, mealItem.CaloriesSnapshot);
        Assert.Equal(10m, mealItem.ProteinSnapshot);
        Assert.Equal(6m, mealItem.FatSnapshot);
        Assert.Equal(40m, mealItem.CarbohydratesSnapshot);
    }
}
