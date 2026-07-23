namespace NutritionTracker.Application.Recipes;

public sealed record RecipeIngredientInput(Guid FoodProductId, decimal WeightGrams);

public sealed record CreateRecipeCommand(
    Guid UserId,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    IReadOnlyList<RecipeIngredientInput> Ingredients,
    string? ChangeReason,
    string ChangeSource);

public sealed record UpdateRecipeCommand(
    Guid Id,
    Guid UserId,
    int ExpectedVersion,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    IReadOnlyList<RecipeIngredientInput> Ingredients,
    string? ChangeReason,
    string ChangeSource);

public sealed record ArchiveRecipeCommand(
    Guid Id,
    Guid UserId,
    string? Reason,
    string Source);

public sealed record GetRecipeQuery(Guid Id, Guid UserId, int? Version = null);

public sealed record SearchRecipesQuery(
    Guid UserId,
    string? Query,
    bool IncludeArchived = false,
    int Limit = 25);

public sealed record CalculateRecipeNutritionQuery(Guid Id, Guid UserId, int? Version = null);

public sealed record CalculateRecipePortionQuery(
    Guid Id,
    Guid UserId,
    decimal WeightGrams,
    int? Version = null);

public sealed record RecipeIngredientResult(
    Guid FoodProductId,
    decimal WeightGrams,
    decimal CaloriesPer100gSnapshot,
    decimal ProteinPer100gSnapshot,
    decimal FatPer100gSnapshot,
    decimal CarbohydratesPer100gSnapshot);

public sealed record RecipeVersionResult(
    int Version,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    string? ChangeReason,
    string ChangeSource,
    DateTimeOffset ChangedAtUtc,
    IReadOnlyList<RecipeIngredientResult> Ingredients);

public sealed record RecipeResult(
    Guid Id,
    Guid UserId,
    int CurrentVersion,
    bool IsArchived,
    DateTimeOffset? ArchivedAtUtc,
    string? ArchiveReason,
    string? ArchiveSource,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    RecipeVersionResult SelectedVersion,
    IReadOnlyList<int> AvailableVersions);

public sealed record RecipeSummaryResult(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    int Version,
    bool IsArchived,
    DateTimeOffset UpdatedAtUtc);

public sealed record RecipeNutritionResult(
    Guid RecipeId,
    int Version,
    decimal? TotalPreparedWeightGrams,
    decimal Calories,
    decimal ProteinGrams,
    decimal FatGrams,
    decimal CarbohydrateGrams,
    decimal? PortionWeightGrams);
