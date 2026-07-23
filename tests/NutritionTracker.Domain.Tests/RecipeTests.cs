using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Domain.Tests;

public sealed class RecipeTests
{
    [Fact]
    public void EmptyRecipeCannotBeCreated()
    {
        Assert.Throws<ArgumentException>(() => DomainTestData.CreateRecipe(addIngredient: false));
    }

    [Fact]
    public void RecipeWithIngredientCanBeUsed()
    {
        var recipe = DomainTestData.CreateRecipe(addIngredient: true);

        var exception = Record.Exception(recipe.EnsureCanBeUsed);

        Assert.Null(exception);
        Assert.Single(recipe.Ingredients);
        Assert.Single(recipe.Versions);
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
                [CreateDefinition()],
                null,
                "Unit test",
                DomainTestData.UtcNow));
    }

    [Fact]
    public void UpdateCreatesImmutableVersionWithAuditData()
    {
        var recipe = DomainTestData.CreateRecipe();
        var firstVersion = recipe.CurrentVersion;
        var updatedAt = DomainTestData.UtcNow.AddMinutes(5);

        recipe.Update(
            "Updated salad",
            "More vegetables",
            600m,
            [CreateDefinition()],
            "Adjusted ingredients",
            "Unit test",
            updatedAt);

        Assert.Equal(2, recipe.Version);
        Assert.Equal(2, recipe.Versions.Count);
        Assert.Same(firstVersion, recipe.GetVersion(1));
        Assert.Equal("Salad", firstVersion.Name);
        Assert.Equal("Updated salad", recipe.CurrentVersion.Name);
        Assert.Equal("Adjusted ingredients", recipe.CurrentVersion.ChangeReason);
        Assert.Equal(updatedAt, recipe.CurrentVersion.ChangedAtUtc);
    }

    [Fact]
    public void ArchivedRecipeCannotBeUsedOrUpdated()
    {
        var recipe = DomainTestData.CreateRecipe();
        recipe.Archive("No longer used", "Unit test", DomainTestData.UtcNow.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(recipe.EnsureCanBeUsed);
        Assert.Throws<InvalidOperationException>(() => recipe.Update(
            "Changed",
            null,
            500m,
            [CreateDefinition()],
            null,
            "Unit test",
            DomainTestData.UtcNow.AddMinutes(2)));
    }

    private static RecipeIngredientDefinition CreateDefinition()
    {
        return new RecipeIngredientDefinition(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            DomainTestData.CreateNutritionValues());
    }
}
