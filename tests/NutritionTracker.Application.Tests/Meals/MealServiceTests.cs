using NutritionTracker.Application.Meals;
using NutritionTracker.Domain.Commands;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Application.Tests.Meals;

public sealed class MealServiceTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddFoodUsesUserTimeZoneSnapshotsAndAllowsNegativeRemaining()
    {
        var user = new UserProfile(Guid.NewGuid(), "User", "Asia/Jerusalem", UtcNow);
        var product = CreateProduct(new NutritionValues(200m, 20m, 10m, 30m));
        var target = new NutritionTarget(
            Guid.NewGuid(), user.Id, new DateOnly(2026, 7, 24), new NutritionValues(150m, 15m, 8m, 25m));
        var repository = new FakeMealRepository(user, [product], [], [target]);
        var service = new MealService(repository, new FixedTimeProvider(UtcNow));
        var occurredAt = new DateTimeOffset(2026, 7, 23, 21, 30, 0, TimeSpan.Zero);

        var result = await service.AddFoodToMealAsync(
            new AddFoodToMealCommand(
                user.Id, "food-1", product.Id, 100m, occurredAt, MealType.Dinner),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 24), result.DailySummary.Date);
        Assert.Equal(200m, result.DailySummary.Consumed.Calories);
        Assert.Equal(-50m, result.DailySummary.Remaining?.Calories);
        Assert.Equal(TimeSpan.Zero, Assert.Single(repository.Meals).OccurredAt.Offset);
        Assert.Equal(new NutritionValues(200m, 20m, 10m, 30m), Assert.Single(repository.Items).NutritionSnapshot);
    }

    [Fact]
    public async Task RepeatedIdempotencyKeyDoesNotCreateSecondItem()
    {
        var user = CreateUser();
        var product = CreateProduct(new NutritionValues(100m, 10m, 5m, 20m));
        var repository = new FakeMealRepository(user, [product], [], []);
        var service = new MealService(repository, new FixedTimeProvider(UtcNow));
        var command = new AddFoodToMealCommand(
            user.Id, "same-request", product.Id, 50m, UtcNow, MealType.Lunch);

        var first = await service.AddFoodToMealAsync(command, CancellationToken.None);
        var repeated = await service.AddFoodToMealAsync(command, CancellationToken.None);

        Assert.False(first.IsReplay);
        Assert.True(repeated.IsReplay);
        Assert.Equal(first.MealItemId, repeated.MealItemId);
        Assert.Single(repository.Items);
        Assert.Single(repository.ProcessedCommands);
    }

    [Fact]
    public async Task RecipePortionAndFractionUsePersistedVersionSnapshots()
    {
        var user = CreateUser();
        var product = CreateProduct(new NutritionValues(100m, 10m, 5m, 20m));
        var recipe = CreateRecipe(user.Id, product, 400m);
        var repository = new FakeMealRepository(user, [product], [recipe], []);
        var service = new MealService(repository, new FixedTimeProvider(UtcNow));

        var portion = await service.AddRecipePortionToMealAsync(
            new AddRecipePortionToMealCommand(
                user.Id, "portion", recipe.Id, 100m, UtcNow, MealType.Lunch),
            CancellationToken.None);
        var fraction = await service.AddRecipeFractionToMealAsync(
            new AddRecipeFractionToMealCommand(
                user.Id, "fraction", recipe.Id, 0.5m, UtcNow, MealType.Lunch),
            CancellationToken.None);

        Assert.Equal(50m, portion.MealItem?.NutritionSnapshot.Calories);
        Assert.Equal(100m, fraction.MealItem?.NutritionSnapshot.Calories);
        Assert.Equal(200m, fraction.MealItem?.WeightGrams);
        Assert.All(repository.Items, item => Assert.Equal(1, item.RecipeVersion));
    }

    [Fact]
    public async Task UpdateMoveAndDeleteRefreshDailySummariesFromSnapshots()
    {
        var user = CreateUser();
        var product = CreateProduct(new NutritionValues(100m, 10m, 5m, 20m));
        var repository = new FakeMealRepository(user, [product], [], []);
        var service = new MealService(repository, new FixedTimeProvider(UtcNow));
        var added = await service.AddFoodToMealAsync(
            new AddFoodToMealCommand(
                user.Id, "add", product.Id, 100m, UtcNow, MealType.Lunch),
            CancellationToken.None);

        var updated = await service.UpdateMealItemWeightAsync(
            new UpdateMealItemWeightCommand(user.Id, "update", added.MealItemId, 150m),
            CancellationToken.None);
        var moved = await service.MoveMealItemAsync(
            new MoveMealItemCommand(
                user.Id,
                "move",
                added.MealItemId,
                UtcNow.AddDays(1),
                MealType.Dinner),
            CancellationToken.None);
        var deleted = await service.DeleteMealItemAsync(
            new DeleteMealItemCommand(user.Id, "delete", added.MealItemId),
            CancellationToken.None);
        var replay = await service.DeleteMealItemAsync(
            new DeleteMealItemCommand(user.Id, "delete", added.MealItemId),
            CancellationToken.None);

        Assert.Equal(150m, updated.DailySummary.Consumed.Calories);
        Assert.Equal(UtcNow.AddDays(1).Date, moved.DailySummary.Meals.Single().OccurredAt.Date);
        Assert.Equal(0m, deleted.DailySummary.Consumed.Calories);
        Assert.True(replay.IsReplay);
        Assert.Null(replay.MealItem);
    }

    private static UserProfile CreateUser()
    {
        return new UserProfile(Guid.NewGuid(), "User", "UTC", UtcNow);
    }

    private static FoodProduct CreateProduct(NutritionValues nutrition)
    {
        return new FoodProduct(
            Guid.NewGuid(), null, "Food", null, nutrition, null, "Test", true, UtcNow, UtcNow);
    }

    private static Recipe CreateRecipe(Guid userId, FoodProduct product, decimal preparedWeight)
    {
        return new Recipe(
            Guid.NewGuid(),
            userId,
            "Recipe",
            null,
            preparedWeight,
            [new RecipeIngredientDefinition(Guid.NewGuid(), product.Id, 200m, product.NutritionPer100g)],
            null,
            "Test",
            UtcNow);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeMealRepository(
        UserProfile user,
        IReadOnlyList<FoodProduct> products,
        IReadOnlyList<Recipe> recipes,
        IReadOnlyList<NutritionTarget> targets) : IMealRepository
    {
        public List<Meal> Meals { get; } = [];

        public List<MealItem> Items { get; } = [];

        public List<ProcessedCommand> ProcessedCommands { get; } = [];

        public Task<UserProfile?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult<UserProfile?>(user.Id == userId ? user : null);
        }

        public Task<FoodProduct?> GetVisibleFoodProductAsync(
            Guid foodProductId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(products.SingleOrDefault(product =>
                product.Id == foodProductId && (product.UserId is null || product.UserId == userId)));
        }

        public Task<Recipe?> GetOwnedRecipeAsync(
            Guid recipeId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(recipes.SingleOrDefault(recipe =>
                recipe.Id == recipeId && recipe.UserId == userId));
        }

        public Task<Meal?> GetMealAsync(
            Guid userId,
            DateTimeOffset occurredAtUtc,
            MealType mealType,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Meals.SingleOrDefault(meal =>
                meal.UserId == userId && meal.OccurredAt == occurredAtUtc && meal.MealType == mealType));
        }

        public Task<Meal?> GetOwnedMealAsync(
            Guid mealId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Meals.SingleOrDefault(meal => meal.Id == mealId && meal.UserId == userId));
        }

        public Task<MealItem?> GetOwnedMealItemAsync(
            Guid mealItemId,
            Guid userId,
            bool trackChanges,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Items.SingleOrDefault(item =>
                item.Id == mealItemId && Meals.Any(meal => meal.Id == item.MealId && meal.UserId == userId)));
        }

        public Task<ProcessedCommand?> GetProcessedCommandAsync(
            Guid userId,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ProcessedCommands.SingleOrDefault(command =>
                command.UserId == userId && command.IdempotencyKey == idempotencyKey));
        }

        public Task<IReadOnlyList<MealJournalEntry>> GetMealsAsync(
            Guid userId,
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<MealJournalEntry> result = Meals
                .Where(meal => meal.UserId == userId && meal.OccurredAt >= startUtc && meal.OccurredAt < endUtc)
                .Select(meal => new MealJournalEntry(
                    meal,
                    Items.Where(item => item.MealId == meal.Id).ToArray()))
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<NutritionTarget?> GetTargetAsync(
            Guid userId,
            DateOnly effectiveDate,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(targets
                .Where(target => target.UserId == userId && target.ValidFrom <= effectiveDate)
                .OrderByDescending(target => target.ValidFrom)
                .FirstOrDefault());
        }

        public Task AddMealAsync(Meal meal, CancellationToken cancellationToken)
        {
            Meals.Add(meal);
            return Task.CompletedTask;
        }

        public Task AddMealItemAsync(MealItem item, CancellationToken cancellationToken)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task AddProcessedCommandAsync(
            ProcessedCommand command,
            CancellationToken cancellationToken)
        {
            ProcessedCommands.Add(command);
            return Task.CompletedTask;
        }

        public void RemoveMealItem(MealItem item)
        {
            Items.Remove(item);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
