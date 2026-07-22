using Microsoft.EntityFrameworkCore;
using NutritionTracker.Domain.Commands;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.IntegrationTests.Persistence;

public sealed class NutritionDbContextTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MigrationCreatesDatabase()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var database = await SqliteTestDatabase.CreateAsync(cancellation.Token);
        await using var context = database.CreateContext();

        var canConnect = await context.Database.CanConnectAsync(cancellation.Token);
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(cancellation.Token);

        Assert.True(canConnect);
        Assert.Single(appliedMigrations);
        Assert.EndsWith("InitialCreate", appliedMigrations.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FoodProductCanBeSavedAndLoaded()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var database = await SqliteTestDatabase.CreateAsync(cancellation.Token);
        var product = CreateFoodProduct();

        await using (var writeContext = database.CreateContext())
        {
            writeContext.FoodProducts.Add(product);
            await writeContext.SaveChangesAsync(cancellation.Token);
        }

        await using var readContext = database.CreateContext();
        var storedProduct = await readContext.FoodProducts
            .AsNoTracking()
            .SingleAsync(item => item.Id == product.Id, cancellation.Token);

        Assert.Equal(product.Name, storedProduct.Name);
        Assert.Equal(product.NutritionPer100g, storedProduct.NutritionPer100g);
        Assert.Equal(product.FiberPer100g, storedProduct.FiberPer100g);
    }

    [Fact]
    public async Task RecipeWithIngredientsCanBeSavedAndLoaded()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var database = await SqliteTestDatabase.CreateAsync(cancellation.Token);
        var user = CreateUser();
        var product = CreateFoodProduct();
        var recipe = new Recipe(
            Guid.NewGuid(), user.Id, "Potato salad", null, 500m, 1, false, UtcNow, UtcNow);
        recipe.AddIngredient(Guid.NewGuid(), product.Id, 250m, UtcNow);

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(user, product, recipe);
            await writeContext.SaveChangesAsync(cancellation.Token);
        }

        await using var readContext = database.CreateContext();
        var storedRecipe = await readContext.Recipes
            .AsNoTracking()
            .Include(item => item.Ingredients)
            .SingleAsync(item => item.Id == recipe.Id, cancellation.Token);

        var ingredient = Assert.Single(storedRecipe.Ingredients);
        Assert.Equal(product.Id, ingredient.FoodProductId);
        Assert.Equal(250m, ingredient.WeightGrams);
    }

    [Fact]
    public async Task MealAndMealItemsCanBeSavedAndLoaded()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var database = await SqliteTestDatabase.CreateAsync(cancellation.Token);
        var user = CreateUser();
        var product = CreateFoodProduct();
        var meal = new Meal(Guid.NewGuid(), user.Id, UtcNow, MealType.Lunch, null, UtcNow);
        var snapshot = new NutritionValues(160m, 4m, 2m, 34m);
        var mealItem = new MealItem(
            Guid.NewGuid(), meal.Id, product, null, 200m, snapshot, null);

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(user, product, meal, mealItem);
            await writeContext.SaveChangesAsync(cancellation.Token);
        }

        await using var readContext = database.CreateContext();
        var storedMeal = await readContext.Meals.AsNoTracking()
            .SingleAsync(item => item.Id == meal.Id, cancellation.Token);
        var storedItem = await readContext.MealItems.AsNoTracking()
            .SingleAsync(item => item.Id == mealItem.Id, cancellation.Token);

        Assert.Equal(user.Id, storedMeal.UserId);
        Assert.Equal(product.Id, storedItem.FoodProductId);
        Assert.Equal(snapshot, storedItem.NutritionSnapshot);
        Assert.Equal(200m, storedItem.WeightGrams);
    }

    [Fact]
    public async Task DuplicateIdempotencyKeyForUserIsRejected()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var database = await SqliteTestDatabase.CreateAsync(cancellation.Token);
        var user = CreateUser();
        var firstCommand = new ProcessedCommand(
            Guid.NewGuid(), user.Id, "message-123", "LogFood", UtcNow);
        var duplicateCommand = new ProcessedCommand(
            Guid.NewGuid(), user.Id, "message-123", "LogFood", UtcNow.AddSeconds(1));

        await using var context = database.CreateContext();
        context.AddRange(user, firstCommand);
        await context.SaveChangesAsync(cancellation.Token);
        context.ProcessedCommands.Add(duplicateCommand);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(cancellation.Token));
    }

    private static UserProfile CreateUser()
    {
        return new UserProfile(Guid.NewGuid(), "Test user", "UTC", UtcNow);
    }

    private static FoodProduct CreateFoodProduct()
    {
        return new FoodProduct(
            Guid.NewGuid(),
            null,
            "Potato",
            null,
            new NutritionValues(80m, 2m, 0.1m, 17m),
            2.2m,
            "Integration test",
            true,
            UtcNow,
            UtcNow);
    }
}
