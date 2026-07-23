using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NutritionTracker.Api.Controllers;
using NutritionTracker.Application.Meals;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;
using NutritionTracker.Domain.Users;
using NutritionTracker.Infrastructure.Persistence;

namespace NutritionTracker.IntegrationTests;

public sealed class MealsEndpointTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RepeatedFoodRequestCreatesOneItemAndUsesUserLocalDate()
    {
        using var factory = new FoodApiWebApplicationFactory();
        var user = new UserProfile(Guid.NewGuid(), "Meal user", "Asia/Jerusalem", UtcNow);
        var product = CreateProduct(new NutritionValues(200m, 20m, 10m, 30m));
        var target = new NutritionTarget(
            Guid.NewGuid(), user.Id, new DateOnly(2026, 7, 24), new NutritionValues(150m, 15m, 8m, 25m));
        await factory.SeedAsync(user, product, target);
        using var client = factory.CreateAuthenticatedClient(user.Id);
        var request = new AddFoodToMealRequest(
            "meal-request-1",
            product.Id,
            100m,
            new DateTimeOffset(2026, 7, 23, 21, 30, 0, TimeSpan.Zero),
            MealType.Dinner,
            null);

        using var firstResponse = await client.PostAsJsonAsync(
            "/api/meals/items/food", request, CancellationToken.None);
        using var repeatedResponse = await client.PostAsJsonAsync(
            "/api/meals/items/food", request, CancellationToken.None);
        var first = await firstResponse.Content.ReadFromJsonAsync<MealOperationResult>(CancellationToken.None);
        var repeated = await repeatedResponse.Content.ReadFromJsonAsync<MealOperationResult>(CancellationToken.None);

        Assert.True(
            firstResponse.StatusCode == HttpStatusCode.Created,
            await firstResponse.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(repeated);
        Assert.False(first.IsReplay);
        Assert.True(repeated.IsReplay);
        Assert.Equal(first.MealItemId, repeated.MealItemId);
        Assert.Equal(new DateOnly(2026, 7, 24), repeated.DailySummary.Date);
        Assert.Equal(200m, repeated.DailySummary.Consumed.Calories);
        Assert.Equal(-50m, repeated.DailySummary.Remaining?.Calories);
        Assert.Equal(TimeSpan.FromHours(3), repeated.DailySummary.Meals.Single().OccurredAt.Offset);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NutritionDbContext>();
        Assert.Equal(1, await context.MealItems.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.ProcessedCommands.CountAsync(CancellationToken.None));
        var storedMeal = await context.Meals.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(TimeSpan.Zero, storedMeal.OccurredAt.Offset);
    }

    [Fact]
    public async Task RecipeFractionCanBeUpdatedMovedAndDeleted()
    {
        using var factory = new FoodApiWebApplicationFactory();
        var user = new UserProfile(Guid.NewGuid(), "Meal user", "UTC", UtcNow);
        var product = CreateProduct(new NutritionValues(100m, 10m, 5m, 20m));
        var recipe = new Recipe(
            Guid.NewGuid(),
            user.Id,
            "Recipe",
            null,
            400m,
            [new RecipeIngredientDefinition(Guid.NewGuid(), product.Id, 200m, product.NutritionPer100g)],
            null,
            "Integration test",
            UtcNow);
        await factory.SeedAsync(user, product, recipe);
        using var client = factory.CreateAuthenticatedClient(user.Id);

        using var addResponse = await client.PostAsJsonAsync(
            "/api/meals/items/recipe",
            new AddRecipeToMealRequest(
                "recipe-add", recipe.Id, null, 0.5m, UtcNow, MealType.Lunch, null),
            CancellationToken.None);
        var added = await addResponse.Content.ReadFromJsonAsync<MealOperationResult>(CancellationToken.None);
        Assert.True(
            addResponse.StatusCode == HttpStatusCode.Created,
            await addResponse.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.NotNull(added);
        Assert.Equal(100m, added.MealItem?.NutritionSnapshot.Calories);
        Assert.Equal(200m, added.MealItem?.WeightGrams);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/meals/items/{added.MealItemId}",
            new UpdateMealItemRequest("recipe-update", 100m, null, null),
            CancellationToken.None);
        var updated = await updateResponse.Content.ReadFromJsonAsync<MealOperationResult>(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(50m, updated.MealItem?.NutritionSnapshot.Calories);

        using var moveResponse = await client.PutAsJsonAsync(
            $"/api/meals/items/{added.MealItemId}",
            new UpdateMealItemRequest(
                "recipe-move", null, UtcNow.AddDays(1), MealType.Dinner),
            CancellationToken.None);
        var moved = await moveResponse.Content.ReadFromJsonAsync<MealOperationResult>(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, moveResponse.StatusCode);
        Assert.NotNull(moved);
        Assert.Equal(new DateOnly(2026, 7, 24), moved.DailySummary.Date);

        using var deleteResponse = await client.DeleteAsync(
            $"/api/meals/items/{added.MealItemId}?idempotencyKey=recipe-delete",
            CancellationToken.None);
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<MealOperationResult>(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.NotNull(deleted);
        Assert.Null(deleted.MealItem);
        Assert.Equal(0m, deleted.DailySummary.Consumed.Calories);

        using var summaryResponse = await client.GetAsync(
            "/api/daily-summary?date=2026-07-24",
            CancellationToken.None);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<DailySummaryResult>(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        Assert.NotNull(summary);
        Assert.Empty(summary.Meals);
    }

    private static FoodProduct CreateProduct(NutritionValues nutrition)
    {
        return new FoodProduct(
            Guid.NewGuid(),
            null,
            "Food",
            null,
            nutrition,
            null,
            "Seed",
            true,
            UtcNow,
            UtcNow);
    }
}
