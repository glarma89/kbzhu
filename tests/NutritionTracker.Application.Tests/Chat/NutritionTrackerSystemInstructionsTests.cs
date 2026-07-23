using NutritionTracker.Application.Chat;

namespace NutritionTracker.Application.Tests.Chat;

public sealed class NutritionTrackerSystemInstructionsTests
{
    [Fact]
    public void InstructionsContainSafetyAndAuthoritativeDataRules()
    {
        var instructions = NutritionTrackerSystemInstructions.Build(
            new DateTimeOffset(2026, 7, 23, 15, 0, 0, TimeSpan.FromHours(3)));

        Assert.Contains("2026-07-23T12:00:00.0000000+00:00", instructions, StringComparison.Ordinal);
        Assert.Contains("backend tools", instructions, StringComparison.Ordinal);
        Assert.Contains("не рассчитывай", instructions, StringComparison.Ordinal);
        Assert.Contains("requires_selection", instructions, StringComparison.Ordinal);
        Assert.Contains("одноразовое изменение", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("данные с упаковки", instructions, StringComparison.Ordinal);
        Assert.Contains("массовым изменением", instructions, StringComparison.Ordinal);
        Assert.Contains("временной зоне пользователя", instructions, StringComparison.Ordinal);
        Assert.Contains("API keys", instructions, StringComparison.Ordinal);
        Assert.Contains("вызвать незарегистрированный метод", instructions, StringComparison.Ordinal);
        Assert.Contains("только через зарегистрированные tools", instructions, StringComparison.Ordinal);
    }
}
