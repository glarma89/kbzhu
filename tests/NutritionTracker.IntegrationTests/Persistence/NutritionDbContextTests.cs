using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        Assert.Equal(2, appliedMigrations.Count());
        Assert.EndsWith("AddRecipeVersionHistory", appliedMigrations.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task VersionHistoryMigrationBackfillsExistingRecipeAndKeepsMealItemValid()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var database = await SqliteTestDatabase.CreateAtMigrationAsync(
            "20260722182615_InitialCreate",
            cancellation.Token);
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var mealId = Guid.NewGuid();
        var mealItemId = Guid.NewGuid();

        await using (var legacyContext = database.CreateContext())
        {
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "UserProfiles" ("Id", "DisplayName", "TimeZone", "CreatedAtUtc")
                VALUES ({userId}, {"Legacy user"}, {"UTC"}, {UtcNow});
                """, cancellation.Token);
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "FoodProducts"
                    ("Id", "UserId", "Name", "NormalizedName", "Brand", "CaloriesPer100g",
                     "ProteinPer100g", "FatPer100g", "CarbohydratesPer100g", "FiberPer100g",
                     "Source", "IsVerified", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES ({productId}, NULL, {"Potato"}, {"POTATO"}, NULL, {80m}, {2m}, {0.1m},
                        {17m}, {2.2m}, {"Legacy"}, {true}, {UtcNow}, {UtcNow});
                """, cancellation.Token);
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Recipes"
                    ("Id", "UserId", "Name", "Description", "TotalPreparedWeightGrams",
                     "Version", "IsArchived", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES ({recipeId}, {userId}, {"Legacy recipe"}, NULL, {400m}, {3}, {false},
                        {UtcNow}, {UtcNow});
                """, cancellation.Token);
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "RecipeIngredients" ("Id", "RecipeId", "FoodProductId", "WeightGrams")
                VALUES ({ingredientId}, {recipeId}, {productId}, {200m});
                """, cancellation.Token);
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Meals" ("Id", "UserId", "OccurredAt", "MealType", "Notes", "CreatedAtUtc")
                VALUES ({mealId}, {userId}, {UtcNow}, {"Dinner"}, NULL, {UtcNow});
                """, cancellation.Token);
            await legacyContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "MealItems"
                    ("Id", "MealId", "FoodProductId", "RecipeId", "WeightGrams", "RecipeVersion",
                     "CaloriesSnapshot", "ProteinSnapshot", "FatSnapshot", "CarbohydratesSnapshot",
                     "SourceMessageId")
                VALUES ({mealItemId}, {mealId}, NULL, {recipeId}, {100m}, {3}, {40m}, {1m}, {0.05m},
                        {8.5m}, NULL);
                """, cancellation.Token);
        }

        await using (var migrationContext = database.CreateContext())
        {
            var migrator = migrationContext.GetService<IMigrator>();
            await migrator.MigrateAsync(cancellationToken: cancellation.Token);
        }

        await using var readContext = database.CreateContext();
        var recipe = await readContext.Recipes.AsNoTracking()
            .SingleAsync(item => item.Id == recipeId, cancellation.Token);
        var version = await readContext.RecipeVersions.AsNoTracking()
            .Include(item => item.Ingredients)
            .SingleAsync(item => item.RecipeId == recipeId && item.Version == 3, cancellation.Token);
        var storedMealItem = await readContext.MealItems.AsNoTracking()
            .SingleAsync(item => item.Id == mealItemId, cancellation.Token);

        Assert.Equal("LEGACY RECIPE", recipe.NormalizedName);
        Assert.Equal("Migration", version.ChangeSource);
        var ingredient = Assert.Single(version.Ingredients);
        Assert.Equal(200m, ingredient.WeightGrams);
        Assert.Equal(new NutritionValues(80m, 2m, 0.1m, 17m), ingredient.NutritionPer100gSnapshot);
        Assert.Equal(3, storedMealItem.RecipeVersion);
        Assert.Equal(new NutritionValues(40m, 1m, 0.05m, 8.5m), storedMealItem.NutritionSnapshot);
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
            Guid.NewGuid(),
            user.Id,
            "Potato salad",
            null,
            500m,
            [new RecipeIngredientDefinition(Guid.NewGuid(), product.Id, 250m, product.NutritionPer100g)],
            "Initial",
            "Integration test",
            UtcNow);

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(user, product, recipe);
            await writeContext.SaveChangesAsync(cancellation.Token);
        }

        await using var readContext = database.CreateContext();
        var storedRecipe = await readContext.Recipes
            .AsNoTracking()
            .Include(item => item.Ingredients)
            .Include(item => item.Versions)
            .ThenInclude(version => version.Ingredients)
            .SingleAsync(item => item.Id == recipe.Id, cancellation.Token);

        var ingredient = Assert.Single(storedRecipe.Ingredients);
        Assert.Equal(product.Id, ingredient.FoodProductId);
        Assert.Equal(250m, ingredient.WeightGrams);
        var version = Assert.Single(storedRecipe.Versions);
        Assert.Equal(1, version.Version);
        Assert.Equal(product.NutritionPer100g, Assert.Single(version.Ingredients).NutritionPer100gSnapshot);
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
    public async Task RecipeUpdateDoesNotChangeHistoricalMealItemOrPreviousVersion()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var database = await SqliteTestDatabase.CreateAsync(cancellation.Token);
        var user = CreateUser();
        var product = CreateFoodProduct();
        var recipe = new Recipe(
            Guid.NewGuid(),
            user.Id,
            "Potato recipe",
            null,
            400m,
            [new RecipeIngredientDefinition(Guid.NewGuid(), product.Id, 200m, product.NutritionPer100g)],
            "Initial",
            "Integration test",
            UtcNow);
        var meal = new Meal(Guid.NewGuid(), user.Id, UtcNow, MealType.Dinner, null, UtcNow);
        var snapshot = NutritionCalculator.CalculateRecipePortion(recipe.CurrentVersion, 100m);
        var mealItem = new MealItem(Guid.NewGuid(), meal.Id, null, recipe, 100m, snapshot, null);

        await using (var writeContext = database.CreateContext())
        {
            writeContext.AddRange(user, product, recipe, meal, mealItem);
            await writeContext.SaveChangesAsync(cancellation.Token);
        }

        await using (var updateContext = database.CreateContext())
        {
            var storedRecipe = await updateContext.Recipes
                .Include(item => item.Ingredients)
                .Include(item => item.Versions)
                .ThenInclude(version => version.Ingredients)
                .SingleAsync(item => item.Id == recipe.Id, cancellation.Token);
            storedRecipe.Update(
                "Potato recipe",
                null,
                500m,
                [new RecipeIngredientDefinition(Guid.NewGuid(), product.Id, 300m, product.NutritionPer100g)],
                "Larger batch",
                "Integration test",
                UtcNow.AddMinutes(1));
            await updateContext.SaveChangesAsync(cancellation.Token);
        }

        await using var readContext = database.CreateContext();
        var storedItem = await readContext.MealItems.AsNoTracking()
            .SingleAsync(item => item.Id == mealItem.Id, cancellation.Token);
        var versions = await readContext.RecipeVersions.AsNoTracking()
            .Include(version => version.Ingredients)
            .Where(version => version.RecipeId == recipe.Id)
            .OrderBy(version => version.Version)
            .ToListAsync(cancellation.Token);

        Assert.Equal(1, storedItem.RecipeVersion);
        Assert.Equal(snapshot, storedItem.NutritionSnapshot);
        Assert.Equal(2, versions.Count);
        Assert.Equal(200m, Assert.Single(versions[0].Ingredients).WeightGrams);
        Assert.Equal(300m, Assert.Single(versions[1].Ingredients).WeightGrams);
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
