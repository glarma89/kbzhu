using NutritionTracker.Domain.Common;

namespace NutritionTracker.Domain.Recipes;

public sealed class Recipe
{
    private readonly List<RecipeIngredient> _ingredients = [];

    public Recipe(
        Guid id,
        Guid userId,
        string name,
        string? description,
        decimal? totalPreparedWeightGrams,
        int version,
        bool isArchived,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = DomainGuard.NotEmpty(id, nameof(id));
        UserId = DomainGuard.NotEmpty(userId, nameof(userId));
        Name = DomainGuard.RequiredText(name, nameof(name));
        Description = DomainGuard.OptionalText(description);
        TotalPreparedWeightGrams = totalPreparedWeightGrams is null
            ? null
            : DomainGuard.Positive(totalPreparedWeightGrams.Value, nameof(totalPreparedWeightGrams));

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "The recipe version must be positive.");
        }

        Version = version;
        IsArchived = isArchived;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = DomainGuard.Utc(updatedAtUtc, nameof(updatedAtUtc));

        if (UpdatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("The update timestamp cannot precede creation.", nameof(updatedAtUtc));
        }
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string Name { get; }

    public string? Description { get; }

    public decimal? TotalPreparedWeightGrams { get; }

    public int Version { get; }

    public bool IsArchived { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<RecipeIngredient> Ingredients => _ingredients;

    public RecipeIngredient AddIngredient(
        Guid ingredientId,
        Guid foodProductId,
        decimal weightGrams,
        DateTimeOffset updatedAtUtc)
    {
        var ingredient = new RecipeIngredient(ingredientId, Id, foodProductId, weightGrams);
        var validatedTimestamp = DomainGuard.Utc(updatedAtUtc, nameof(updatedAtUtc));

        if (validatedTimestamp < UpdatedAtUtc)
        {
            throw new ArgumentException("The update timestamp cannot move backwards.", nameof(updatedAtUtc));
        }

        if (_ingredients.Any(existing => existing.Id == ingredient.Id))
        {
            throw new InvalidOperationException("An ingredient with this identifier already exists.");
        }

        _ingredients.Add(ingredient);
        UpdatedAtUtc = validatedTimestamp;
        return ingredient;
    }

    public void EnsureCanBeUsed()
    {
        if (_ingredients.Count == 0)
        {
            throw new InvalidOperationException("A recipe must contain at least one ingredient before use.");
        }
    }
}
