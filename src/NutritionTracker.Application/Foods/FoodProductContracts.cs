namespace NutritionTracker.Application.Foods;

public sealed record CreateFoodProductCommand(
    Guid? UserId,
    string Name,
    string? Brand,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal FatPer100g,
    decimal CarbohydratesPer100g,
    decimal? FiberPer100g,
    string Source,
    bool IsVerified);

public sealed record UpdateFoodProductCommand(
    Guid Id,
    Guid? UserId,
    string Name,
    string? Brand,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal FatPer100g,
    decimal CarbohydratesPer100g,
    decimal? FiberPer100g,
    string Source,
    bool IsVerified);

public sealed record GetFoodProductByIdQuery(Guid Id, Guid? UserId);

public sealed record SearchFoodProductsQuery(Guid? UserId, string? Query, int Limit = 25);

public sealed record FindCandidatesByNameQuery(Guid? UserId, string Name, int Limit = 10);

public sealed record FoodProductResult(
    Guid Id,
    Guid? UserId,
    string Name,
    string NormalizedName,
    string? Brand,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal FatPer100g,
    decimal CarbohydratesPer100g,
    decimal? FiberPer100g,
    string Source,
    bool IsVerified,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
