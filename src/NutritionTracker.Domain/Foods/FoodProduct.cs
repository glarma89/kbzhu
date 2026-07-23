using NutritionTracker.Domain.Common;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Foods;

public sealed class FoodProduct
{
    private FoodProduct()
    {
        Name = null!;
        NormalizedName = null!;
        NutritionPer100g = null!;
        Source = null!;
    }

    public FoodProduct(
        Guid id,
        Guid? userId,
        string name,
        string? brand,
        NutritionValues nutritionPer100g,
        decimal? fiberPer100g,
        string source,
        bool isVerified,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        UserId = DomainGuard.OptionalNotEmpty(userId, nameof(userId));
        Name = DomainGuard.RequiredText(name, nameof(name));
        NormalizedName = FoodNameNormalizer.Normalize(Name);
        Brand = DomainGuard.OptionalText(brand);
        NutritionPer100g = nutritionPer100g ?? throw new ArgumentNullException(nameof(nutritionPer100g));
        FiberPer100g = fiberPer100g is null
            ? null
            : DomainGuard.NonNegative(fiberPer100g.Value, nameof(fiberPer100g));
        Source = DomainGuard.RequiredText(source, nameof(source));
        IsVerified = isVerified;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = DomainGuard.Utc(updatedAtUtc, nameof(updatedAtUtc));

        if (UpdatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("The update timestamp cannot precede creation.", nameof(updatedAtUtc));
        }
    }

    public Guid Id { get; }

    public Guid? UserId { get; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Brand { get; private set; }

    public NutritionValues NutritionPer100g { get; private set; }

    public decimal CaloriesPer100g => NutritionPer100g.Calories;

    public decimal ProteinPer100g => NutritionPer100g.ProteinGrams;

    public decimal FatPer100g => NutritionPer100g.FatGrams;

    public decimal CarbohydratesPer100g => NutritionPer100g.CarbohydrateGrams;

    public decimal? FiberPer100g { get; private set; }

    public string Source { get; private set; }

    public bool IsVerified { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string? brand,
        NutritionValues nutritionPer100g,
        decimal? fiberPer100g,
        string source,
        bool isVerified,
        DateTimeOffset updatedAtUtc)
    {
        var validatedUpdatedAtUtc = DomainGuard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        if (validatedUpdatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("The update timestamp cannot precede creation.", nameof(updatedAtUtc));
        }

        var validatedName = DomainGuard.RequiredText(name, nameof(name));
        var validatedNormalizedName = FoodNameNormalizer.Normalize(validatedName);
        var validatedBrand = DomainGuard.OptionalText(brand);
        var validatedNutrition = nutritionPer100g ?? throw new ArgumentNullException(nameof(nutritionPer100g));
        decimal? validatedFiber = fiberPer100g is null
            ? null
            : DomainGuard.NonNegative(fiberPer100g.Value, nameof(fiberPer100g));
        var validatedSource = DomainGuard.RequiredText(source, nameof(source));

        Name = validatedName;
        NormalizedName = validatedNormalizedName;
        Brand = validatedBrand;
        NutritionPer100g = validatedNutrition;
        FiberPer100g = validatedFiber;
        Source = validatedSource;
        IsVerified = isVerified;
        UpdatedAtUtc = validatedUpdatedAtUtc;
    }
}
