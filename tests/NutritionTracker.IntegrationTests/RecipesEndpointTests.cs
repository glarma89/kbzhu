using System.Net;
using System.Net.Http.Json;
using NutritionTracker.Api.Controllers;
using NutritionTracker.Application.Recipes;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.IntegrationTests;

public sealed class RecipesEndpointTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecipeLifecyclePreservesVersionsAndCalculatesNutrition()
    {
        using var factory = new FoodApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var user = new UserProfile(Guid.NewGuid(), "Recipe owner", "UTC", UtcNow);
        var potato = CreateProduct("Potato", new NutritionValues(80m, 2m, 0.1m, 17m));
        var yogurt = CreateProduct("Yogurt", new NutritionValues(60m, 10m, 3m, 4m));
        await factory.SeedAsync(user, potato, yogurt);
        var createRequest = new CreateRecipeRequest(
            user.Id,
            "Potato salad",
            "Initial recipe",
            400m,
            [new RecipeIngredientRequest(potato.Id, 200m)],
            "First version",
            "Integration test");

        using var createResponse = await client.PostAsJsonAsync(
            "/api/recipes",
            createRequest,
            CancellationToken.None);
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeResult>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(1, created.CurrentVersion);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/recipes/{created.Id}",
            new UpdateRecipeRequest(
                user.Id,
                1,
                "Potato salad",
                "With yogurt",
                500m,
                [
                    new RecipeIngredientRequest(potato.Id, 200m),
                    new RecipeIngredientRequest(yogurt.Id, 100m)
                ],
                "Add yogurt",
                "Integration test"),
            CancellationToken.None);
        var updated = await updateResponse.Content.ReadFromJsonAsync<RecipeResult>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.CurrentVersion);
        Assert.Equal([1, 2], updated.AvailableVersions);

        using var oldVersionResponse = await client.GetAsync(
            $"/api/recipes/{created.Id}?userId={user.Id}&version=1",
            CancellationToken.None);
        var oldVersion = await oldVersionResponse.Content.ReadFromJsonAsync<RecipeResult>(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, oldVersionResponse.StatusCode);
        Assert.NotNull(oldVersion);
        Assert.Single(oldVersion.SelectedVersion.Ingredients);
        Assert.Equal(potato.Id, oldVersion.SelectedVersion.Ingredients[0].FoodProductId);

        using var nutritionResponse = await client.GetAsync(
            $"/api/recipes/{created.Id}/nutrition?userId={user.Id}&version=2",
            CancellationToken.None);
        var nutrition = await nutritionResponse.Content.ReadFromJsonAsync<RecipeNutritionResult>(
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, nutritionResponse.StatusCode);
        Assert.NotNull(nutrition);
        Assert.Equal(220m, nutrition.Calories);

        using var portionResponse = await client.GetAsync(
            $"/api/recipes/{created.Id}/nutrition/portion?userId={user.Id}&weightGrams=250",
            CancellationToken.None);
        var portion = await portionResponse.Content.ReadFromJsonAsync<RecipeNutritionResult>(
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, portionResponse.StatusCode);
        Assert.NotNull(portion);
        Assert.Equal(110m, portion.Calories);
    }

    [Fact]
    public async Task ArchivedRecipeIsExcludedAndRejectsUpdate()
    {
        using var factory = new FoodApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var user = new UserProfile(Guid.NewGuid(), "Recipe owner", "UTC", UtcNow);
        var product = CreateProduct("Potato", new NutritionValues(80m, 2m, 0.1m, 17m));
        await factory.SeedAsync(user, product);
        using var createResponse = await client.PostAsJsonAsync(
            "/api/recipes",
            new CreateRecipeRequest(
                user.Id,
                "Recipe to archive",
                null,
                200m,
                [new RecipeIngredientRequest(product.Id, 100m)],
                null,
                "Integration test"),
            CancellationToken.None);
        var created = await createResponse.Content.ReadFromJsonAsync<RecipeResult>(CancellationToken.None);
        Assert.NotNull(created);

        using var archiveResponse = await client.PostAsJsonAsync(
            $"/api/recipes/{created.Id}/archive",
            new ArchiveRecipeRequest(user.Id, "Retired", "Integration test"),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

        using var searchResponse = await client.GetAsync(
            $"/api/recipes?userId={user.Id}",
            CancellationToken.None);
        var searchResults = await searchResponse.Content.ReadFromJsonAsync<List<RecipeSummaryResult>>(
            CancellationToken.None);
        Assert.NotNull(searchResults);
        Assert.Empty(searchResults);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/recipes/{created.Id}",
            new UpdateRecipeRequest(
                user.Id,
                1,
                "Changed",
                null,
                200m,
                [new RecipeIngredientRequest(product.Id, 100m)],
                null,
                "Integration test"),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    }

    private static FoodProduct CreateProduct(string name, NutritionValues nutrition)
    {
        return new FoodProduct(
            Guid.NewGuid(),
            null,
            name,
            null,
            nutrition,
            null,
            "Seed",
            true,
            UtcNow,
            UtcNow);
    }
}
