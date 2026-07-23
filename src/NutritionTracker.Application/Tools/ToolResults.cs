using NutritionTracker.Domain.Meals;

namespace NutritionTracker.Application.Tools;

public sealed record ToolNutritionValues(
    decimal Calories,
    decimal ProteinGrams,
    decimal FatGrams,
    decimal CarbohydrateGrams);

public sealed record FoodToolResult(
    Guid FoodProductId,
    string Name,
    string? Brand,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal FatPer100g,
    decimal CarbohydratesPer100g,
    decimal? FiberPer100g,
    string Source,
    bool IsVerified,
    DateTimeOffset UpdatedAtUtc);

public sealed record SearchFoodsToolResult(
    IReadOnlyList<FoodToolResult> Matches,
    bool RequiresSelection);

public sealed record RecipeIngredientToolResult(
    Guid FoodProductId,
    string FoodName,
    decimal WeightGrams);

public sealed record RecipeToolResult(
    Guid RecipeId,
    int Version,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    bool IsArchived,
    IReadOnlyList<RecipeIngredientToolResult> Ingredients,
    ToolNutritionValues TotalNutrition,
    DateTimeOffset UpdatedAtUtc);

public sealed record RecipeSearchMatchToolResult(
    Guid RecipeId,
    int Version,
    string Name,
    string? Description,
    bool IsArchived,
    DateTimeOffset UpdatedAtUtc);

public sealed record SearchRecipesToolResult(
    IReadOnlyList<RecipeSearchMatchToolResult> Matches,
    bool RequiresSelection);

public sealed record DiaryItemToolResult(
    Guid DiaryItemId,
    Guid MealId,
    Guid? FoodProductId,
    Guid? RecipeId,
    int? RecipeVersion,
    decimal WeightGrams,
    ToolNutritionValues NutritionSnapshot);

public sealed record MealToolResult(
    Guid MealId,
    DateTimeOffset OccurredAt,
    MealType MealType,
    IReadOnlyList<DiaryItemToolResult> Items);

public sealed record DailySummaryToolResult(
    DateOnly Date,
    string TimeZone,
    ToolNutritionValues Consumed,
    ToolNutritionValues? Target,
    ToolNutritionValues? Remaining,
    IReadOnlyList<MealToolResult> Meals);

public sealed record DiaryMutationToolResult(
    string Operation,
    bool IsReplay,
    Guid DiaryItemId,
    DiaryItemToolResult? DiaryItem,
    DailySummaryToolResult DailySummary);

public sealed record RecentMealsToolResult(
    DateOnly FromDate,
    DateOnly ThroughDate,
    string TimeZone,
    IReadOnlyList<MealToolResult> Meals);

public sealed record NutritionTargetsToolResult(
    DateOnly RequestedDate,
    DateOnly? EffectiveFrom,
    ToolNutritionValues? Target,
    ToolNutritionValues? Remaining);
