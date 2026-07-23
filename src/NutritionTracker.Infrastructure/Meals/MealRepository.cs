using Microsoft.EntityFrameworkCore;
using NutritionTracker.Application.Meals;
using NutritionTracker.Domain.Commands;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;
using NutritionTracker.Domain.Users;
using NutritionTracker.Infrastructure.Persistence;

namespace NutritionTracker.Infrastructure.Meals;

internal sealed class MealRepository(NutritionDbContext context) : IMealRepository
{
    public Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return context.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public Task<FoodProduct?> GetVisibleFoodProductAsync(
        Guid foodProductId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return context.FoodProducts.AsNoTracking().SingleOrDefaultAsync(
            product => product.Id == foodProductId &&
                (product.UserId == null || product.UserId == userId),
            cancellationToken);
    }

    public Task<Recipe?> GetOwnedRecipeAsync(
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return context.Recipes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(recipe => recipe.Ingredients)
            .Include(recipe => recipe.Versions)
            .ThenInclude(version => version.Ingredients)
            .SingleOrDefaultAsync(
                recipe => recipe.Id == recipeId && recipe.UserId == userId,
                cancellationToken);
    }

    public Task<Meal?> GetMealAsync(
        Guid userId,
        DateTimeOffset occurredAtUtc,
        MealType mealType,
        CancellationToken cancellationToken)
    {
        return context.Meals.SingleOrDefaultAsync(
            meal => meal.UserId == userId &&
                meal.OccurredAt == occurredAtUtc &&
                meal.MealType == mealType,
            cancellationToken);
    }

    public Task<Meal?> GetOwnedMealAsync(
        Guid mealId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return context.Meals.AsNoTracking().SingleOrDefaultAsync(
            meal => meal.Id == mealId && meal.UserId == userId,
            cancellationToken);
    }

    public Task<MealItem?> GetOwnedMealItemAsync(
        Guid mealItemId,
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        IQueryable<MealItem> query = context.MealItems.Where(item =>
            item.Id == mealItemId &&
            context.Meals.Any(meal => meal.Id == item.MealId && meal.UserId == userId));
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(cancellationToken);
    }

    public Task<ProcessedCommand?> GetProcessedCommandAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return context.ProcessedCommands.AsNoTracking().SingleOrDefaultAsync(
            command => command.UserId == userId && command.IdempotencyKey == idempotencyKey,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MealJournalEntry>> GetMealsAsync(
        Guid userId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken)
    {
        var meals = await context.Meals.AsNoTracking()
            .Where(meal => meal.UserId == userId &&
                meal.OccurredAt >= startUtc && meal.OccurredAt < endUtc)
            .OrderBy(meal => meal.OccurredAt)
            .ToListAsync(cancellationToken);
        if (meals.Count == 0)
        {
            return [];
        }

        var mealIds = meals.Select(meal => meal.Id).ToArray();
        var items = await context.MealItems.AsNoTracking()
            .Where(item => mealIds.Contains(item.MealId))
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var itemsByMeal = items.ToLookup(item => item.MealId);
        return meals
            .Select(meal => new MealJournalEntry(meal, itemsByMeal[meal.Id].ToArray()))
            .ToArray();
    }

    public Task<NutritionTarget?> GetTargetAsync(
        Guid userId,
        DateOnly effectiveDate,
        CancellationToken cancellationToken)
    {
        return context.NutritionTargets.AsNoTracking()
            .Where(target => target.UserId == userId && target.ValidFrom <= effectiveDate)
            .OrderByDescending(target => target.ValidFrom)
            .ThenByDescending(target => target.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddMealAsync(Meal meal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(meal);
        await context.Meals.AddAsync(meal, cancellationToken);
    }

    public async Task AddMealItemAsync(MealItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        await context.MealItems.AddAsync(item, cancellationToken);
    }

    public async Task AddProcessedCommandAsync(
        ProcessedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await context.ProcessedCommands.AddAsync(command, cancellationToken);
    }

    public void RemoveMealItem(MealItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        context.MealItems.Remove(item);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
