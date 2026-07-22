using NutritionTracker.Domain.Chat;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Domain.Tests;

public sealed class EntityValidationTests
{
    [Fact]
    public void EntityIdentifiersCannotBeEmpty()
    {
        Assert.Throws<ArgumentException>(
            () => new UserProfile(Guid.Empty, "User", "Asia/Jerusalem", DomainTestData.UtcNow));
    }

    [Fact]
    public void UtcTimestampMustHaveZeroOffset()
    {
        var nonUtcTimestamp = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.FromHours(3));

        Assert.Throws<ArgumentException>(
            () => new ChatMessage(Guid.NewGuid(), Guid.NewGuid(), ChatRole.User, "Hello", nonUtcTimestamp));
    }

    [Fact]
    public void FoodProductRejectsNegativeFiber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FoodProduct(
                Guid.NewGuid(),
                null,
                "Potato",
                null,
                NutritionValues.Zero,
                -1,
                "Test",
                false,
                DomainTestData.UtcNow,
                DomainTestData.UtcNow));
    }

    [Fact]
    public void GlobalFoodProductHasNoUserAndNormalizesName()
    {
        var product = DomainTestData.CreateFoodProduct();

        Assert.Null(product.UserId);
        Assert.Equal("POTATO", product.NormalizedName);
    }
}
