using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Application.Recipes;

public interface IRecipeRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, FoodProduct>> GetVisibleFoodProductsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    Task AddAsync(Recipe recipe, CancellationToken cancellationToken);

    Task<Recipe?> GetByIdAsync(
        Guid id,
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Recipe>> SearchAsync(
        Guid userId,
        string? normalizedQuery,
        bool includeArchived,
        int limit,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
