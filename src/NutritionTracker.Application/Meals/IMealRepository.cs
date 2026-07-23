using NutritionTracker.Domain.Commands;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Application.Meals;

public sealed record MealJournalEntry(Meal Meal, IReadOnlyList<MealItem> Items);

public interface IMealRepository
{
    Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<FoodProduct?> GetVisibleFoodProductAsync(
        Guid foodProductId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Recipe?> GetOwnedRecipeAsync(Guid recipeId, Guid userId, CancellationToken cancellationToken);

    Task<Meal?> GetMealAsync(
        Guid userId,
        DateTimeOffset occurredAtUtc,
        MealType mealType,
        CancellationToken cancellationToken);

    Task<Meal?> GetOwnedMealAsync(Guid mealId, Guid userId, CancellationToken cancellationToken);

    Task<MealItem?> GetOwnedMealItemAsync(
        Guid mealItemId,
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<ProcessedCommand?> GetProcessedCommandAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MealJournalEntry>> GetMealsAsync(
        Guid userId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken);

    Task<NutritionTarget?> GetTargetAsync(
        Guid userId,
        DateOnly effectiveDate,
        CancellationToken cancellationToken);

    Task AddMealAsync(Meal meal, CancellationToken cancellationToken);

    Task AddMealItemAsync(MealItem item, CancellationToken cancellationToken);

    Task AddProcessedCommandAsync(ProcessedCommand command, CancellationToken cancellationToken);

    void RemoveMealItem(MealItem item);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
