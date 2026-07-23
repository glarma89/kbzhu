using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Recipes;

public sealed class RecipeVersion
{
    private readonly List<RecipeVersionIngredient> _ingredients = [];

    private RecipeVersion()
    {
        Name = null!;
        ChangeSource = null!;
    }

    internal RecipeVersion(
        Guid recipeId,
        int version,
        string name,
        string? description,
        decimal? totalPreparedWeightGrams,
        IEnumerable<RecipeIngredientDefinition> ingredients,
        string? changeReason,
        string changeSource,
        DateTimeOffset changedAtUtc)
    {
        RecipeId = DomainGuard.NotEmpty(recipeId, nameof(recipeId));
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "The recipe version must be positive.");
        }

        Version = version;
        Name = DomainGuard.RequiredText(name, nameof(name));
        Description = DomainGuard.OptionalText(description);
        TotalPreparedWeightGrams = totalPreparedWeightGrams is null
            ? null
            : DomainGuard.Positive(totalPreparedWeightGrams.Value, nameof(totalPreparedWeightGrams));
        ChangeReason = DomainGuard.OptionalText(changeReason);
        ChangeSource = DomainGuard.RequiredText(changeSource, nameof(changeSource));
        ChangedAtUtc = DomainGuard.Utc(changedAtUtc, nameof(changedAtUtc));
        _ingredients.AddRange(ingredients.Select(item => new RecipeVersionIngredient(
            RecipeId,
            Version,
            item.FoodProductId,
            item.WeightGrams,
            item.NutritionPer100gSnapshot)));
    }

    public Guid RecipeId { get; }

    public int Version { get; }

    public string Name { get; }

    public string? Description { get; }

    public decimal? TotalPreparedWeightGrams { get; }

    public string? ChangeReason { get; }

    public string ChangeSource { get; }

    public DateTimeOffset ChangedAtUtc { get; }

    public IReadOnlyList<RecipeVersionIngredient> Ingredients => _ingredients;
}
