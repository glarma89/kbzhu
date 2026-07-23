using NutritionTracker.Application.Common;
using NutritionTracker.Application.Recipes;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Application.Tests.Recipes;

public sealed class RecipeServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task CreateCapturesIngredientNutritionAndAuditMetadata()
    {
        var product = CreateProduct(new NutritionValues(100m, 5m, 2m, 20m));
        var repository = new FakeRecipeRepository(product);
        var service = CreateService(repository);

        var result = await service.CreateRecipeAsync(CreateCommand(product.Id), CancellationToken.None);

        Assert.Equal(1, result.CurrentVersion);
        Assert.Equal("Initial import", result.SelectedVersion.ChangeReason);
        var ingredient = Assert.Single(result.SelectedVersion.Ingredients);
        Assert.Equal(100m, ingredient.CaloriesPer100gSnapshot);
        Assert.Single(repository.Recipes);
    }

    [Fact]
    public async Task UpdateCreatesNewVersionAndPreservesPreviousComposition()
    {
        var firstProduct = CreateProduct(new NutritionValues(100m, 5m, 2m, 20m));
        var secondProduct = CreateProduct(new NutritionValues(50m, 10m, 1m, 5m));
        var repository = new FakeRecipeRepository(firstProduct, secondProduct);
        var service = CreateService(repository);
        var created = await service.CreateRecipeAsync(CreateCommand(firstProduct.Id), CancellationToken.None);

        var updated = await service.UpdateRecipeAsync(
            new UpdateRecipeCommand(
                created.Id,
                UserId,
                1,
                "Updated recipe",
                null,
                400m,
                [new RecipeIngredientInput(secondProduct.Id, 200m)],
                "Swap ingredient",
                "Unit test"),
            CancellationToken.None);
        var oldVersion = await service.GetRecipeAsync(
            new GetRecipeQuery(created.Id, UserId, 1),
            CancellationToken.None);

        Assert.Equal(2, updated.CurrentVersion);
        Assert.Equal(secondProduct.Id, Assert.Single(updated.SelectedVersion.Ingredients).FoodProductId);
        Assert.Equal(firstProduct.Id, Assert.Single(oldVersion.SelectedVersion.Ingredients).FoodProductId);
        Assert.Equal([1, 2], updated.AvailableVersions);
    }

    [Fact]
    public async Task OldVersionCalculationUsesPersistedProductSnapshot()
    {
        var product = CreateProduct(new NutritionValues(100m, 5m, 2m, 20m));
        var repository = new FakeRecipeRepository(product);
        var service = CreateService(repository);
        var created = await service.CreateRecipeAsync(CreateCommand(product.Id), CancellationToken.None);
        product.Update(
            product.Name,
            product.Brand,
            new NutritionValues(999m, 99m, 99m, 99m),
            null,
            product.Source,
            product.IsVerified,
            DateTimeOffset.UtcNow.AddMinutes(1));

        var result = await service.CalculateRecipeNutritionAsync(
            new CalculateRecipeNutritionQuery(created.Id, UserId, 1),
            CancellationToken.None);

        Assert.Equal(200m, result.Calories);
        Assert.Equal(10m, result.ProteinGrams);
    }

    [Fact]
    public async Task StaleExpectedVersionIsRejected()
    {
        var product = CreateProduct(new NutritionValues(100m, 5m, 2m, 20m));
        var repository = new FakeRecipeRepository(product);
        var service = CreateService(repository);
        var created = await service.CreateRecipeAsync(CreateCommand(product.Id), CancellationToken.None);
        var command = new UpdateRecipeCommand(
            created.Id,
            UserId,
            2,
            "Changed",
            null,
            500m,
            [new RecipeIngredientInput(product.Id, 200m)],
            null,
            "Unit test");

        await Assert.ThrowsAsync<ApplicationConflictException>(
            () => service.UpdateRecipeAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task ArchivedRecipeIsExcludedAndCannotBeUpdated()
    {
        var product = CreateProduct(new NutritionValues(100m, 5m, 2m, 20m));
        var repository = new FakeRecipeRepository(product);
        var service = CreateService(repository);
        var created = await service.CreateRecipeAsync(CreateCommand(product.Id), CancellationToken.None);
        await service.ArchiveRecipeAsync(
            new ArchiveRecipeCommand(created.Id, UserId, "Retired", "Unit test"),
            CancellationToken.None);

        var search = await service.SearchRecipesAsync(
            new SearchRecipesQuery(UserId, null),
            CancellationToken.None);

        Assert.Empty(search);
        await Assert.ThrowsAsync<ApplicationConflictException>(() => service.UpdateRecipeAsync(
            new UpdateRecipeCommand(
                created.Id,
                UserId,
                1,
                "Changed",
                null,
                500m,
                [new RecipeIngredientInput(product.Id, 200m)],
                null,
                "Unit test"),
            CancellationToken.None));
    }

    private static RecipeService CreateService(FakeRecipeRepository repository)
    {
        return new RecipeService(repository, TimeProvider.System);
    }

    private static CreateRecipeCommand CreateCommand(Guid productId)
    {
        return new CreateRecipeCommand(
            UserId,
            "Test recipe",
            null,
            400m,
            [new RecipeIngredientInput(productId, 200m)],
            "Initial import",
            "Unit test");
    }

    private static FoodProduct CreateProduct(NutritionValues nutrition)
    {
        var now = DateTimeOffset.UtcNow;
        return new FoodProduct(
            Guid.NewGuid(),
            null,
            "Ingredient",
            null,
            nutrition,
            null,
            "Unit test",
            true,
            now,
            now);
    }

    private sealed class FakeRecipeRepository(params FoodProduct[] products) : IRecipeRepository
    {
        public List<Recipe> Recipes { get; } = [];

        private Dictionary<Guid, FoodProduct> Products { get; } = products.ToDictionary(item => item.Id);

        public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(userId == UserId);
        }

        public Task<IReadOnlyDictionary<Guid, FoodProduct>> GetVisibleFoodProductsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<Guid, FoodProduct> result = Products
                .Where(item => productIds.Contains(item.Key))
                .ToDictionary();
            return Task.FromResult(result);
        }

        public Task AddAsync(Recipe recipe, CancellationToken cancellationToken)
        {
            Recipes.Add(recipe);
            return Task.CompletedTask;
        }

        public Task<Recipe?> GetByIdAsync(
            Guid id,
            Guid userId,
            bool trackChanges,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Recipes.SingleOrDefault(item => item.Id == id && item.UserId == userId));
        }

        public Task<IReadOnlyList<Recipe>> SearchAsync(
            Guid userId,
            string? normalizedQuery,
            bool includeArchived,
            int limit,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Recipe> result = Recipes
                .Where(item => item.UserId == userId && (includeArchived || !item.IsArchived))
                .Where(item => normalizedQuery is null || item.NormalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
                .Take(limit)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
