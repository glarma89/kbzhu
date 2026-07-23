using Microsoft.EntityFrameworkCore;
using NutritionTracker.Application.Recipes;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Recipes;
using NutritionTracker.Infrastructure.Persistence;

namespace NutritionTracker.Infrastructure.Recipes;

internal sealed class RecipeRepository(NutritionDbContext context) : IRecipeRepository
{
    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return context.UserProfiles.AnyAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, FoodProduct>> GetVisibleFoodProductsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        return await context.FoodProducts
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id) &&
                (product.UserId == null || product.UserId == userId))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
    }

    public async Task AddAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        await context.Recipes.AddAsync(recipe, cancellationToken);
    }

    public Task<Recipe?> GetByIdAsync(
        Guid id,
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        IQueryable<Recipe> query = context.Recipes
            .AsSplitQuery()
            .Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.Versions)
            .ThenInclude(version => version.Ingredients);
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            recipe => recipe.Id == id && recipe.UserId == userId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> SearchAsync(
        Guid userId,
        string? normalizedQuery,
        bool includeArchived,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = context.Recipes.AsNoTracking().Where(recipe => recipe.UserId == userId);
        if (!includeArchived)
        {
            query = query.Where(recipe => !recipe.IsArchived);
        }

        if (normalizedQuery is not null)
        {
            query = query.Where(recipe => recipe.NormalizedName.Contains(normalizedQuery));
        }

        return await query
            .OrderBy(recipe => recipe.NormalizedName == normalizedQuery ? 0 : 1)
            .ThenBy(recipe => recipe.Name)
            .ThenBy(recipe => recipe.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
