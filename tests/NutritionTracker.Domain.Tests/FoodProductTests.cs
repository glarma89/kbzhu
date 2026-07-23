using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests;

public sealed class FoodProductTests
{
    [Fact]
    public void NameNormalizationCanonicalizesUnicodeAndWhitespace()
    {
        var normalized = FoodNameNormalizer.Normalize("  Greek\tＹogurt  ");

        Assert.Equal("GREEK YOGURT", normalized);
    }

    [Fact]
    public void UpdateReplacesEditableValuesAndRenormalizesName()
    {
        var product = DomainTestData.CreateFoodProduct();
        var updatedAt = DomainTestData.UtcNow.AddMinutes(1);

        product.Update(
            "  sweet   potato ",
            "Farm",
            new NutritionValues(90m, 2m, 0.2m, 21m),
            3m,
            "Manual",
            false,
            updatedAt);

        Assert.Equal("sweet   potato", product.Name);
        Assert.Equal("SWEET POTATO", product.NormalizedName);
        Assert.Equal("Farm", product.Brand);
        Assert.Equal(90m, product.CaloriesPer100g);
        Assert.Equal(3m, product.FiberPer100g);
        Assert.Equal(updatedAt, product.UpdatedAtUtc);
    }
}
