using NutritionTracker.Application.Common;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Application.Foods;

public sealed class FoodProductService(
    IFoodProductRepository repository,
    TimeProvider timeProvider) : IFoodProductService
{
    private const int MaximumNameLength = 200;
    private const int MaximumBrandLength = 200;
    private const int MaximumSourceLength = 100;
    private const int MaximumQueryLength = 200;
    private const int MaximumResultLimit = 100;
    private const decimal MaximumCaloriesPer100g = 1000m;
    private const decimal MaximumNutrientGramsPer100g = 100m;

    public async Task<FoodProductResult> CreateFoodProductAsync(
        CreateFoodProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateOptionalUserId(command.UserId);
        var nutrition = ValidateInput(
            command.Name,
            command.Brand,
            command.CaloriesPer100g,
            command.ProteinPer100g,
            command.FatPer100g,
            command.CarbohydratesPer100g,
            command.FiberPer100g,
            command.Source);
        if (command.UserId is Guid userId &&
            !await repository.UserExistsAsync(userId, cancellationToken))
        {
            throw new EntityNotFoundException("UserProfile", userId);
        }

        var now = timeProvider.GetUtcNow();
        var product = new FoodProduct(
            Guid.NewGuid(),
            command.UserId,
            command.Name,
            command.Brand,
            nutrition,
            command.FiberPer100g,
            command.Source,
            command.IsVerified,
            now,
            now);

        await repository.AddAsync(product, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(product);
    }

    public async Task<FoodProductResult> UpdateFoodProductAsync(
        UpdateFoodProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.Id, nameof(command.Id));
        ValidateOptionalUserId(command.UserId);
        var nutrition = ValidateInput(
            command.Name,
            command.Brand,
            command.CaloriesPer100g,
            command.ProteinPer100g,
            command.FatPer100g,
            command.CarbohydratesPer100g,
            command.FiberPer100g,
            command.Source);
        var product = await repository.GetOwnedByIdAsync(
            command.Id,
            command.UserId,
            cancellationToken)
            ?? throw new EntityNotFoundException(nameof(FoodProduct), command.Id);

        product.Update(
            command.Name,
            command.Brand,
            nutrition,
            command.FiberPer100g,
            command.Source,
            command.IsVerified,
            timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(product);
    }

    public async Task<FoodProductResult> GetFoodProductByIdAsync(
        GetFoodProductByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateId(query.Id, nameof(query.Id));
        ValidateOptionalUserId(query.UserId);
        var product = await repository.GetVisibleByIdAsync(query.Id, query.UserId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(FoodProduct), query.Id);
        return Map(product);
    }

    public async Task<IReadOnlyList<FoodProductResult>> SearchFoodProductsAsync(
        SearchFoodProductsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateOptionalUserId(query.UserId);
        ValidateLimit(query.Limit);
        string? normalizedQuery = null;
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            ValidateTextLength(query.Query, MaximumQueryLength, nameof(query.Query));
            normalizedQuery = FoodNameNormalizer.Normalize(query.Query);
        }

        var products = await repository.SearchAsync(
            query.UserId,
            normalizedQuery,
            query.Limit,
            cancellationToken);
        return products.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<FoodProductResult>> FindCandidatesByNameAsync(
        FindCandidatesByNameQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateOptionalUserId(query.UserId);
        ValidateLimit(query.Limit);
        ValidateRequiredText(query.Name, MaximumQueryLength, nameof(query.Name));
        var normalizedName = FoodNameNormalizer.Normalize(query.Name);
        var products = await repository.FindCandidatesByNameAsync(
            query.UserId,
            normalizedName,
            query.Limit,
            cancellationToken);
        return products.Select(Map).ToArray();
    }

    private static NutritionValues ValidateInput(
        string name,
        string? brand,
        decimal caloriesPer100g,
        decimal proteinPer100g,
        decimal fatPer100g,
        decimal carbohydratesPer100g,
        decimal? fiberPer100g,
        string source)
    {
        ValidateRequiredText(name, MaximumNameLength, nameof(name));
        ValidateOptionalText(brand, MaximumBrandLength, nameof(brand));
        ValidateRequiredText(source, MaximumSourceLength, nameof(source));
        ValidateRange(caloriesPer100g, MaximumCaloriesPer100g, nameof(caloriesPer100g));
        ValidateRange(proteinPer100g, MaximumNutrientGramsPer100g, nameof(proteinPer100g));
        ValidateRange(fatPer100g, MaximumNutrientGramsPer100g, nameof(fatPer100g));
        ValidateRange(carbohydratesPer100g, MaximumNutrientGramsPer100g, nameof(carbohydratesPer100g));
        if (fiberPer100g is not null)
        {
            ValidateRange(fiberPer100g.Value, MaximumNutrientGramsPer100g, nameof(fiberPer100g));
        }

        return new NutritionValues(
            caloriesPer100g,
            proteinPer100g,
            fatPer100g,
            carbohydratesPer100g);
    }

    private static void ValidateRequiredText(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApplicationValidationException("The value is required.", parameterName);
        }

        ValidateTextLength(value.Trim(), maximumLength, parameterName);
    }

    private static void ValidateOptionalText(string? value, int maximumLength, string parameterName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ValidateTextLength(value.Trim(), maximumLength, parameterName);
        }
    }

    private static void ValidateTextLength(string value, int maximumLength, string parameterName)
    {
        if (value.Length > maximumLength)
        {
            throw new ApplicationValidationException(
                $"The value cannot exceed {maximumLength} characters.",
                parameterName);
        }
    }

    private static void ValidateRange(decimal value, decimal maximumValue, string parameterName)
    {
        if (value < 0 || value > maximumValue)
        {
            throw new ApplicationValidationException(
                $"The value must be between 0 and {maximumValue}.",
                parameterName);
        }
    }

    private static void ValidateId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ApplicationValidationException("The identifier cannot be empty.", parameterName);
        }
    }

    private static void ValidateOptionalUserId(Guid? userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ApplicationValidationException(
                "The user identifier cannot be empty when specified.",
                nameof(userId));
        }
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > MaximumResultLimit)
        {
            throw new ApplicationValidationException(
                $"The limit must be between 1 and {MaximumResultLimit}.",
                nameof(limit));
        }
    }

    private static FoodProductResult Map(FoodProduct product)
    {
        return new FoodProductResult(
            product.Id,
            product.UserId,
            product.Name,
            product.NormalizedName,
            product.Brand,
            product.CaloriesPer100g,
            product.ProteinPer100g,
            product.FatPer100g,
            product.CarbohydratesPer100g,
            product.FiberPer100g,
            product.Source,
            product.IsVerified,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }
}
