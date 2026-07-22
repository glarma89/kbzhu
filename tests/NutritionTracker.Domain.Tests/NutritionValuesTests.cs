using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests;

public sealed class NutritionValuesTests
{
    [Fact]
    public void ConstructorRejectsNegativeNutritionValues()
    {
        var invalidValues = new[]
        {
            (-1m, 0m, 0m, 0m),
            (0m, -1m, 0m, 0m),
            (0m, 0m, -1m, 0m),
            (0m, 0m, 0m, -1m)
        };

        foreach (var (calories, protein, fat, carbohydrates) in invalidValues)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new NutritionValues(calories, protein, fat, carbohydrates));
        }
    }

    [Fact]
    public void ZeroContainsOnlyZeroValues()
    {
        var expected = new NutritionValues(0, 0, 0, 0);

        Assert.Equal(expected, NutritionValues.Zero);
    }
}
