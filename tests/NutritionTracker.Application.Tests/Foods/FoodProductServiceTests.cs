using NutritionTracker.Application.Common;
using NutritionTracker.Application.Foods;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Application.Tests.Foods;

public sealed class FoodProductServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task CreateStoresSeparateProductsEvenWhenNamesNormalizeEqually()
    {
        var repository = new FakeFoodProductRepository();
        var service = CreateService(repository);

        var first = await service.CreateFoodProductAsync(
            CreateCommand("Greek  yogurt"),
            CancellationToken.None);
        var second = await service.CreateFoodProductAsync(
            CreateCommand(" greek\tyogurt "),
            CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("GREEK YOGURT", first.NormalizedName);
        Assert.Equal("GREEK YOGURT", second.NormalizedName);
        Assert.Equal(2, repository.Products.Count);
        Assert.Equal(2, repository.SaveChangesCount);
    }

    [Fact]
    public async Task CreateStoresNutritionPerOneHundredGrams()
    {
        var repository = new FakeFoodProductRepository();
        var service = CreateService(repository);

        var result = await service.CreateFoodProductAsync(
            CreateCommand("Rice"),
            CancellationToken.None);

        Assert.Equal(120m, result.CaloriesPer100g);
        Assert.Equal(4m, result.ProteinPer100g);
        Assert.Equal(3m, result.FatPer100g);
        Assert.Equal(20m, result.CarbohydratesPer100g);
        Assert.Equal(2m, result.FiberPer100g);
    }

    [Fact]
    public async Task CreateRejectsUnknownUser()
    {
        var repository = new FakeFoodProductRepository { UserExists = false };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.CreateFoodProductAsync(CreateCommand("Rice"), CancellationToken.None));
        Assert.Empty(repository.Products);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task CreateRejectsCaloriesOutsideAllowedRange(decimal calories)
    {
        var service = CreateService(new FakeFoodProductRepository());
        var command = CreateCommand("Rice") with { CaloriesPer100g = calories };

        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.CreateFoodProductAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRequiresMatchingOwner()
    {
        var product = CreateProduct(UserId, "Rice");
        var repository = new FakeFoodProductRepository(product);
        var service = CreateService(repository);
        var command = UpdateCommand(product.Id, Guid.NewGuid(), "Brown rice");

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => service.UpdateFoodProductAsync(command, CancellationToken.None));
        Assert.Equal("Rice", product.Name);
    }

    [Fact]
    public async Task SearchNormalizesQueryBeforeCallingRepository()
    {
        var repository = new FakeFoodProductRepository();
        var service = CreateService(repository);

        await service.SearchFoodProductsAsync(
            new SearchFoodProductsQuery(UserId, "  greek\tＹogurt ", 7),
            CancellationToken.None);

        Assert.Equal("GREEK YOGURT", repository.LastNormalizedQuery);
        Assert.Equal(7, repository.LastLimit);
    }

    [Fact]
    public async Task FindCandidatesReturnsEveryRepositoryCandidateWithoutMerging()
    {
        var personal = CreateProduct(UserId, "Yogurt");
        var global = CreateProduct(null, "Yogurt");
        var repository = new FakeFoodProductRepository(personal, global);
        var service = CreateService(repository);

        var results = await service.FindCandidatesByNameAsync(
            new FindCandidatesByNameQuery(UserId, "yogurt", 10),
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.Id == personal.Id);
        Assert.Contains(results, result => result.Id == global.Id);
    }

    private static FoodProductService CreateService(FakeFoodProductRepository repository)
    {
        return new FoodProductService(repository, TimeProvider.System);
    }

    private static CreateFoodProductCommand CreateCommand(string name)
    {
        return new CreateFoodProductCommand(
            UserId,
            name,
            null,
            120m,
            4m,
            3m,
            20m,
            2m,
            "Manual",
            false);
    }

    private static UpdateFoodProductCommand UpdateCommand(Guid id, Guid? userId, string name)
    {
        return new UpdateFoodProductCommand(
            id,
            userId,
            name,
            null,
            120m,
            4m,
            3m,
            20m,
            2m,
            "Manual",
            false);
    }

    private static FoodProduct CreateProduct(Guid? userId, string name)
    {
        var now = DateTimeOffset.UtcNow;
        return new FoodProduct(
            Guid.NewGuid(),
            userId,
            name,
            null,
            new NutritionValues(100m, 5m, 2m, 15m),
            1m,
            "Test",
            false,
            now,
            now);
    }

    private sealed class FakeFoodProductRepository(params FoodProduct[] products) : IFoodProductRepository
    {
        public List<FoodProduct> Products { get; } = [.. products];

        public string? LastNormalizedQuery { get; private set; }

        public int LastLimit { get; private set; }

        public int SaveChangesCount { get; private set; }

        public bool UserExists { get; init; } = true;

        public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(UserExists);
        }

        public Task AddAsync(FoodProduct product, CancellationToken cancellationToken)
        {
            Products.Add(product);
            return Task.CompletedTask;
        }

        public Task<FoodProduct?> GetVisibleByIdAsync(
            Guid id,
            Guid? userId,
            CancellationToken cancellationToken)
        {
            var product = Products.SingleOrDefault(
                item => item.Id == id && (item.UserId is null || item.UserId == userId));
            return Task.FromResult(product);
        }

        public Task<FoodProduct?> GetOwnedByIdAsync(
            Guid id,
            Guid? userId,
            CancellationToken cancellationToken)
        {
            var product = Products.SingleOrDefault(item => item.Id == id && item.UserId == userId);
            return Task.FromResult(product);
        }

        public Task<IReadOnlyList<FoodProduct>> SearchAsync(
            Guid? userId,
            string? normalizedQuery,
            int limit,
            CancellationToken cancellationToken)
        {
            LastNormalizedQuery = normalizedQuery;
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<FoodProduct>>(Visible(userId).Take(limit).ToArray());
        }

        public Task<IReadOnlyList<FoodProduct>> FindCandidatesByNameAsync(
            Guid? userId,
            string normalizedName,
            int limit,
            CancellationToken cancellationToken)
        {
            LastNormalizedQuery = normalizedName;
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<FoodProduct>>(
                Visible(userId)
                    .Where(item => item.NormalizedName.Contains(normalizedName, StringComparison.Ordinal))
                    .Take(limit)
                    .ToArray());
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }

        private IEnumerable<FoodProduct> Visible(Guid? userId)
        {
            return Products.Where(item => item.UserId is null || item.UserId == userId);
        }
    }
}
