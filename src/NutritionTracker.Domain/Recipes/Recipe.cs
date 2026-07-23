using NutritionTracker.Domain.Common;
using NutritionTracker.Domain.Foods;

namespace NutritionTracker.Domain.Recipes;

public sealed class Recipe
{
    private readonly List<RecipeIngredient> _ingredients = [];
    private readonly List<RecipeVersion> _versions = [];

    private Recipe()
    {
        Name = null!;
        NormalizedName = null!;
    }

    public Recipe(
        Guid id,
        Guid userId,
        string name,
        string? description,
        decimal? totalPreparedWeightGrams,
        IEnumerable<RecipeIngredientDefinition> ingredients,
        string? changeReason,
        string changeSource,
        DateTimeOffset createdAtUtc)
    {
        Name = null!;
        NormalizedName = null!;
        Id = DomainGuard.NotEmpty(id, nameof(id));
        UserId = DomainGuard.NotEmpty(userId, nameof(userId));
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        Version = 1;

        var state = BuildVersionState(
            name,
            description,
            totalPreparedWeightGrams,
            ingredients,
            changeReason,
            changeSource,
            CreatedAtUtc,
            Version);
        ApplyState(state);
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public decimal? TotalPreparedWeightGrams { get; private set; }

    public int Version { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public string? ArchiveReason { get; private set; }

    public string? ArchiveSource { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<RecipeIngredient> Ingredients => _ingredients;

    public IReadOnlyList<RecipeVersion> Versions => _versions;

    public RecipeVersion CurrentVersion => GetVersion(Version);

    public RecipeVersion GetVersion(int version)
    {
        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "The recipe version must be positive.");
        }

        return _versions.SingleOrDefault(item => item.Version == version)
            ?? throw new InvalidOperationException($"Recipe version '{version}' is unavailable.");
    }

    public void Update(
        string name,
        string? description,
        decimal? totalPreparedWeightGrams,
        IEnumerable<RecipeIngredientDefinition> ingredients,
        string? changeReason,
        string changeSource,
        DateTimeOffset updatedAtUtc)
    {
        EnsureNotArchived();
        var validatedTimestamp = DomainGuard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        if (validatedTimestamp < UpdatedAtUtc)
        {
            throw new ArgumentException("The update timestamp cannot move backwards.", nameof(updatedAtUtc));
        }

        var nextVersion = checked(Version + 1);
        var state = BuildVersionState(
            name,
            description,
            totalPreparedWeightGrams,
            ingredients,
            changeReason,
            changeSource,
            validatedTimestamp,
            nextVersion);

        Version = nextVersion;
        UpdatedAtUtc = validatedTimestamp;
        ApplyState(state);
    }

    public void Archive(string? reason, string source, DateTimeOffset archivedAtUtc)
    {
        EnsureNotArchived();
        var validatedTimestamp = DomainGuard.Utc(archivedAtUtc, nameof(archivedAtUtc));
        if (validatedTimestamp < UpdatedAtUtc)
        {
            throw new ArgumentException("The archive timestamp cannot move backwards.", nameof(archivedAtUtc));
        }

        ArchiveReason = DomainGuard.OptionalText(reason);
        ArchiveSource = DomainGuard.RequiredText(source, nameof(source));
        ArchivedAtUtc = validatedTimestamp;
        UpdatedAtUtc = validatedTimestamp;
        IsArchived = true;
    }

    public void EnsureCanBeUsed()
    {
        EnsureNotArchived();
        if (_ingredients.Count == 0)
        {
            throw new InvalidOperationException("A recipe must contain at least one ingredient before use.");
        }
    }

    private VersionState BuildVersionState(
        string name,
        string? description,
        decimal? totalPreparedWeightGrams,
        IEnumerable<RecipeIngredientDefinition> ingredients,
        string? changeReason,
        string changeSource,
        DateTimeOffset changedAtUtc,
        int version)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        var definitions = ingredients.ToArray();
        if (definitions.Length == 0)
        {
            throw new ArgumentException("A recipe must contain at least one ingredient.", nameof(ingredients));
        }

        if (definitions.Select(item => item.Id).Distinct().Count() != definitions.Length)
        {
            throw new ArgumentException("Ingredient identifiers must be unique.", nameof(ingredients));
        }

        if (definitions.Select(item => item.FoodProductId).Distinct().Count() != definitions.Length)
        {
            throw new ArgumentException("A food product can appear only once in a recipe version.", nameof(ingredients));
        }

        var validatedName = DomainGuard.RequiredText(name, nameof(name));
        var validatedDescription = DomainGuard.OptionalText(description);
        decimal? validatedPreparedWeight = totalPreparedWeightGrams is null
            ? null
            : DomainGuard.Positive(totalPreparedWeightGrams.Value, nameof(totalPreparedWeightGrams));
        var currentIngredients = definitions
            .Select(item => new RecipeIngredient(item.Id, Id, item.FoodProductId, item.WeightGrams))
            .ToArray();
        var recipeVersion = new RecipeVersion(
            Id,
            version,
            validatedName,
            validatedDescription,
            validatedPreparedWeight,
            definitions,
            changeReason,
            changeSource,
            changedAtUtc);

        return new VersionState(
            validatedName,
            FoodNameNormalizer.Normalize(validatedName),
            validatedDescription,
            validatedPreparedWeight,
            currentIngredients,
            recipeVersion);
    }

    private void ApplyState(VersionState state)
    {
        Name = state.Name;
        NormalizedName = state.NormalizedName;
        Description = state.Description;
        TotalPreparedWeightGrams = state.TotalPreparedWeightGrams;
        _ingredients.Clear();
        _ingredients.AddRange(state.Ingredients);
        _versions.Add(state.Version);
    }

    private void EnsureNotArchived()
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("An archived recipe cannot be changed or used.");
        }
    }

    private sealed record VersionState(
        string Name,
        string NormalizedName,
        string? Description,
        decimal? TotalPreparedWeightGrams,
        IReadOnlyList<RecipeIngredient> Ingredients,
        RecipeVersion Version);
}
