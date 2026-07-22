using NutritionTracker.Domain.Common;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Foods;

public sealed class FoodProduct
{
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
        NormalizedName = Name.ToUpperInvariant();
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

    public string Name { get; }

    public string NormalizedName { get; }

    public string? Brand { get; }

    public NutritionValues NutritionPer100g { get; }

    public decimal CaloriesPer100g => NutritionPer100g.Calories;

    public decimal ProteinPer100g => NutritionPer100g.ProteinGrams;

    public decimal FatPer100g => NutritionPer100g.FatGrams;

    public decimal CarbohydratesPer100g => NutritionPer100g.CarbohydrateGrams;

    public decimal? FiberPer100g { get; }

    public string Source { get; }

    public bool IsVerified { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }
}
