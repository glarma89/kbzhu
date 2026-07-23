using NutritionTracker.Domain.Common;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Domain.Meals;

public sealed class MealItem
{
    private MealItem()
    {
        NutritionSnapshot = null!;
    }

    public MealItem(
        Guid id,
        Guid mealId,
        FoodProduct? foodProduct,
        Recipe? recipe,
        decimal weightGrams,
        NutritionValues nutritionSnapshot,
        Guid? sourceMessageId)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        MealId = DomainGuard.NotEmpty(mealId, nameof(mealId));

        if ((foodProduct is null) == (recipe is null))
        {
            throw new ArgumentException("A meal item must reference either one food product or one recipe.");
        }

        if (recipe is not null)
        {
            recipe.EnsureCanBeUsed();
        }

        FoodProductId = foodProduct?.Id;
        RecipeId = recipe?.Id;
        RecipeVersion = recipe?.Version;
        WeightGrams = DomainGuard.Positive(weightGrams, nameof(weightGrams));
        NutritionSnapshot = nutritionSnapshot ?? throw new ArgumentNullException(nameof(nutritionSnapshot));
        SourceMessageId = DomainGuard.OptionalNotEmpty(sourceMessageId, nameof(sourceMessageId));
    }

    public Guid Id { get; }

    public Guid MealId { get; private set; }

    public Guid? FoodProductId { get; }

    public Guid? RecipeId { get; }

    public decimal WeightGrams { get; private set; }

    public int? RecipeVersion { get; }

    public NutritionValues NutritionSnapshot { get; private set; }

    public decimal CaloriesSnapshot => NutritionSnapshot.Calories;

    public decimal ProteinSnapshot => NutritionSnapshot.ProteinGrams;

    public decimal FatSnapshot => NutritionSnapshot.FatGrams;

    public decimal CarbohydratesSnapshot => NutritionSnapshot.CarbohydrateGrams;

    public Guid? SourceMessageId { get; }

    public void UpdateWeight(decimal weightGrams, NutritionValues nutritionSnapshot)
    {
        WeightGrams = DomainGuard.Positive(weightGrams, nameof(weightGrams));
        NutritionSnapshot = nutritionSnapshot ?? throw new ArgumentNullException(nameof(nutritionSnapshot));
    }

    public void MoveTo(Guid mealId)
    {
        MealId = DomainGuard.NotEmpty(mealId, nameof(mealId));
    }
}
