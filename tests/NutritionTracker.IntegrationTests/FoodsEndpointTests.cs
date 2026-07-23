using System.Net;
using System.Net.Http.Json;
using NutritionTracker.Api.Controllers;
using NutritionTracker.Application.Foods;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.IntegrationTests;

public sealed class FoodsEndpointTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProtectedEndpointRejectsUnauthenticatedRequest()
    {
        using var factory = new FoodApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/foods", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateGetAndUpdateFoodProductRoundTrip()
    {
        using var factory = new FoodApiWebApplicationFactory();
        var user = new UserProfile(Guid.NewGuid(), "Owner", "UTC", UtcNow);
        await factory.SeedAsync(user);
        using var client = factory.CreateAuthenticatedClient(user.Id);
        var createRequest = CreateRequest(" Greek  yogurt ");

        using var createResponse = await client.PostAsJsonAsync(
            "/api/foods",
            createRequest,
            CancellationToken.None);
        var created = await createResponse.Content.ReadFromJsonAsync<FoodProductResult>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("GREEK YOGURT", created.NormalizedName);
        Assert.Equal(120m, created.CaloriesPer100g);

        var updateRequest = new UpdateFoodProductRequest(
            "Strained yogurt",
            "Dairy",
            130m,
            8m,
            4m,
            12m,
            0m,
            "Manual",
            false);
        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/foods/{created.Id}",
            updateRequest,
            CancellationToken.None);
        var updated = await updateResponse.Content.ReadFromJsonAsync<FoodProductResult>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("STRAINED YOGURT", updated.NormalizedName);
        Assert.Equal(130m, updated.CaloriesPer100g);

        using var getResponse = await client.GetAsync(
            $"/api/foods/{created.Id}",
            CancellationToken.None);
        var loaded = await getResponse.Content.ReadFromJsonAsync<FoodProductResult>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(updated, loaded);
    }

    [Fact]
    public async Task CandidateSearchReturnsPersonalBeforeGlobalAndKeepsBoth()
    {
        using var factory = new FoodApiWebApplicationFactory();
        var user = new UserProfile(Guid.NewGuid(), "Owner", "UTC", UtcNow);
        var personal = CreateProduct(user.Id, "Greek yogurt", false);
        var global = CreateProduct(null, "Greek yogurt", true);
        await factory.SeedAsync(user, personal, global);
        using var client = factory.CreateAuthenticatedClient(user.Id);

        var requestUris = new[]
        {
            "/api/foods?query=greek%20yogurt",
            "/api/foods/candidates?name=greek%20yogurt"
        };

        foreach (var requestUri in requestUris)
        {
            using var response = await client.GetAsync(requestUri, CancellationToken.None);
            var candidates = await response.Content.ReadFromJsonAsync<List<FoodProductResult>>(
                CancellationToken.None);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(candidates);
            Assert.Equal(2, candidates.Count);
            Assert.Equal(personal.Id, candidates[0].Id);
            Assert.Equal(global.Id, candidates[1].Id);
        }
    }

    [Fact]
    public async Task ProductOwnedByAnotherUserIsNotVisibleOrEditable()
    {
        using var factory = new FoodApiWebApplicationFactory();
        var owner = new UserProfile(Guid.NewGuid(), "Owner", "UTC", UtcNow);
        var product = CreateProduct(owner.Id, "Private yogurt", false);
        var otherUserId = Guid.NewGuid();
        var otherUser = new UserProfile(otherUserId, "Other", "UTC", UtcNow);
        await factory.SeedAsync(owner, otherUser, product);
        using var client = factory.CreateAuthenticatedClient(otherUserId);

        using var getResponse = await client.GetAsync(
            $"/api/foods/{product.Id}",
            CancellationToken.None);
        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/foods/{product.Id}",
            CreateUpdateRequest("Changed"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidNutritionReturnsProblemDetails()
    {
        using var factory = new FoodApiWebApplicationFactory();
        var user = new UserProfile(Guid.NewGuid(), "Owner", "UTC", UtcNow);
        await factory.SeedAsync(user);
        using var client = factory.CreateAuthenticatedClient(user.Id);
        var request = CreateRequest("Invalid") with { ProteinPer100g = 101m };

        using var response = await client.PostAsJsonAsync(
            "/api/foods",
            request,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static CreateFoodProductRequest CreateRequest(string name)
    {
        return new CreateFoodProductRequest(
            name,
            null,
            120m,
            8m,
            4m,
            12m,
            0m,
            "Manual",
            false);
    }

    private static UpdateFoodProductRequest CreateUpdateRequest(string name)
    {
        return new UpdateFoodProductRequest(
            name,
            null,
            120m,
            8m,
            4m,
            12m,
            0m,
            "Manual",
            false);
    }

    private static FoodProduct CreateProduct(Guid? userId, string name, bool isVerified)
    {
        return new FoodProduct(
            Guid.NewGuid(),
            userId,
            name,
            null,
            new NutritionValues(120m, 8m, 4m, 12m),
            0m,
            "Seed",
            isVerified,
            UtcNow,
            UtcNow);
    }
}
