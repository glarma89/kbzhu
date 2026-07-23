namespace NutritionTracker.Application.Meals;

public interface IMealService
{
    Task<MealOperationResult> AddFoodToMealAsync(
        AddFoodToMealCommand command,
        CancellationToken cancellationToken);

    Task<MealOperationResult> AddRecipePortionToMealAsync(
        AddRecipePortionToMealCommand command,
        CancellationToken cancellationToken);

    Task<MealOperationResult> AddRecipeFractionToMealAsync(
        AddRecipeFractionToMealCommand command,
        CancellationToken cancellationToken);

    Task<MealOperationResult> UpdateMealItemWeightAsync(
        UpdateMealItemWeightCommand command,
        CancellationToken cancellationToken);

    Task<MealOperationResult> DeleteMealItemAsync(
        DeleteMealItemCommand command,
        CancellationToken cancellationToken);

    Task<MealOperationResult> MoveMealItemAsync(
        MoveMealItemCommand command,
        CancellationToken cancellationToken);

    Task<DailySummaryResult> GetDailySummaryAsync(
        GetDailySummaryQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MealResult>> GetMealsForDateAsync(
        GetMealsForDateQuery query,
        CancellationToken cancellationToken);

    Task<RemainingNutritionResult?> GetRemainingDailyTargetsAsync(
        GetRemainingDailyTargetsQuery query,
        CancellationToken cancellationToken);
}
