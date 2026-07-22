using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Domain.Tests;

public sealed class RecipeTests
{
    [Fact]
    public void EmptyRecipeCannotBeUsed()
    {
        var recipe = DomainTestData.CreateRecipe(addIngredient: false);

        Assert.Throws<InvalidOperationException>(recipe.EnsureCanBeUsed);
    }

    [Fact]
    public void RecipeWithIngredientCanBeUsed()
    {
        var recipe = DomainTestData.CreateRecipe(addIngredient: true);

        var exception = Record.Exception(recipe.EnsureCanBeUsed);

        Assert.Null(exception);
        Assert.Single(recipe.Ingredients);
    }

    [Fact]
    public void IngredientWeightMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RecipeIngredient(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RecipeIngredient(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -1));
    }

    [Fact]
    public void PreparedWeightMustBePositiveWhenSpecified()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Recipe(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Salad",
                null,
                0,
                1,
                false,
                DomainTestData.UtcNow,
                DomainTestData.UtcNow));
    }
}
