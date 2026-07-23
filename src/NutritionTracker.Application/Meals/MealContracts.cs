using NutritionTracker.Domain.Meals;

namespace NutritionTracker.Application.Meals;

public sealed record AddFoodToMealCommand(
    Guid UserId,
    string IdempotencyKey,
    Guid FoodProductId,
    decimal WeightGrams,
    DateTimeOffset OccurredAt,
    MealType MealType,
    Guid? SourceMessageId = null);

public sealed record AddRecipePortionToMealCommand(
    Guid UserId,
    string IdempotencyKey,
    Guid RecipeId,
    decimal WeightGrams,
    DateTimeOffset OccurredAt,
    MealType MealType,
    Guid? SourceMessageId = null);

public sealed record AddRecipeFractionToMealCommand(
    Guid UserId,
    string IdempotencyKey,
    Guid RecipeId,
    decimal Fraction,
    DateTimeOffset OccurredAt,
    MealType MealType,
    Guid? SourceMessageId = null);

public sealed record UpdateMealItemWeightCommand(
    Guid UserId,
    string IdempotencyKey,
    Guid MealItemId,
    decimal WeightGrams);

public sealed record DeleteMealItemCommand(Guid UserId, string IdempotencyKey, Guid MealItemId);

public sealed record MoveMealItemCommand(
    Guid UserId,
    string IdempotencyKey,
    Guid MealItemId,
    DateTimeOffset OccurredAt,
    MealType MealType);

public sealed record GetDailySummaryQuery(Guid UserId, DateOnly Date);

public sealed record GetMealsForDateQuery(Guid UserId, DateOnly Date);

public sealed record GetRemainingDailyTargetsQuery(Guid UserId, DateOnly Date);

public sealed record NutritionTotalResult(
    decimal Calories,
    decimal ProteinGrams,
    decimal FatGrams,
    decimal CarbohydrateGrams);

public sealed record RemainingNutritionResult(
    decimal Calories,
    decimal ProteinGrams,
    decimal FatGrams,
    decimal CarbohydrateGrams);

public sealed record MealItemResult(
    Guid Id,
    Guid MealId,
    Guid? FoodProductId,
    Guid? RecipeId,
    int? RecipeVersion,
    decimal WeightGrams,
    NutritionTotalResult NutritionSnapshot,
    Guid? SourceMessageId);

public sealed record MealResult(
    Guid Id,
    DateTimeOffset OccurredAt,
    MealType MealType,
    string? Notes,
    IReadOnlyList<MealItemResult> Items);

public sealed record DailySummaryResult(
    DateOnly Date,
    string TimeZone,
    NutritionTotalResult Consumed,
    NutritionTotalResult? Target,
    RemainingNutritionResult? Remaining,
    IReadOnlyList<MealResult> Meals);

public sealed record MealOperationResult(
    string Operation,
    bool IsReplay,
    Guid MealItemId,
    MealItemResult? MealItem,
    DailySummaryResult DailySummary);
