using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Recipes;

public sealed class RecipeIngredient
{
    private RecipeIngredient()
    {
    }

    public RecipeIngredient(Guid id, Guid recipeId, Guid foodProductId, decimal weightGrams)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        RecipeId = DomainGuard.NotEmpty(recipeId, nameof(recipeId));
        FoodProductId = DomainGuard.NotEmpty(foodProductId, nameof(foodProductId));
        WeightGrams = DomainGuard.Positive(weightGrams, nameof(weightGrams));
    }

    public Guid Id { get; }

    public Guid RecipeId { get; }

    public Guid FoodProductId { get; }

    public decimal WeightGrams { get; }
}
