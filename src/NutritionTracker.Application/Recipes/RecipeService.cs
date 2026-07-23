using NutritionTracker.Application.Common;
using NutritionTracker.Domain.Foods;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Recipes;

namespace NutritionTracker.Application.Recipes;

public sealed class RecipeService(IRecipeRepository repository, TimeProvider timeProvider) : IRecipeService
{
    private const int MaximumNameLength = 200;
    private const int MaximumDescriptionLength = 2000;
    private const int MaximumAuditTextLength = 500;
    private const int MaximumSourceLength = 100;
    private const int MaximumIngredients = 100;
    private const int MaximumResultLimit = 100;
    private const decimal MaximumWeightGrams = 1_000_000m;

    public async Task<RecipeResult> CreateRecipeAsync(
        CreateRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateUserId(command.UserId);
        ValidateRecipeInput(
            command.Name,
            command.Description,
            command.TotalPreparedWeightGrams,
            command.Ingredients,
            command.ChangeReason,
            command.ChangeSource);
        if (!await repository.UserExistsAsync(command.UserId, cancellationToken))
        {
            throw new EntityNotFoundException("UserProfile", command.UserId);
        }

        var definitions = await BuildDefinitionsAsync(
            command.UserId,
            command.Ingredients,
            cancellationToken);
        var recipe = new Recipe(
            Guid.NewGuid(),
            command.UserId,
            command.Name,
            command.Description,
            command.TotalPreparedWeightGrams,
            definitions,
            command.ChangeReason,
            command.ChangeSource,
            timeProvider.GetUtcNow());
        await repository.AddAsync(recipe, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(recipe, recipe.Version);
    }

    public async Task<RecipeResult> GetRecipeAsync(
        GetRecipeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateId(query.Id, nameof(query.Id));
        ValidateUserId(query.UserId);
        ValidateOptionalVersion(query.Version);
        var recipe = await GetRecipeEntityAsync(query.Id, query.UserId, false, cancellationToken);
        return Map(recipe, query.Version ?? recipe.Version);
    }

    public async Task<IReadOnlyList<RecipeSummaryResult>> SearchRecipesAsync(
        SearchRecipesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateUserId(query.UserId);
        ValidateLimit(query.Limit);
        string? normalizedQuery = null;
        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            ValidateText(query.Query, MaximumNameLength, nameof(query.Query), required: false);
            normalizedQuery = FoodNameNormalizer.Normalize(query.Query);
        }

        var recipes = await repository.SearchAsync(
            query.UserId,
            normalizedQuery,
            query.IncludeArchived,
            query.Limit,
            cancellationToken);
        return recipes.Select(MapSummary).ToArray();
    }

    public async Task<RecipeResult> UpdateRecipeAsync(
        UpdateRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.Id, nameof(command.Id));
        ValidateUserId(command.UserId);
        if (command.ExpectedVersion <= 0)
        {
            throw new ApplicationValidationException("ExpectedVersion must be positive.", nameof(command.ExpectedVersion));
        }

        ValidateRecipeInput(
            command.Name,
            command.Description,
            command.TotalPreparedWeightGrams,
            command.Ingredients,
            command.ChangeReason,
            command.ChangeSource);
        var recipe = await GetRecipeEntityAsync(command.Id, command.UserId, true, cancellationToken);
        if (recipe.IsArchived)
        {
            throw new ApplicationConflictException("An archived recipe cannot be updated without restoration.");
        }

        if (recipe.Version != command.ExpectedVersion)
        {
            throw new ApplicationConflictException(
                $"Recipe version conflict. Expected {command.ExpectedVersion}, current {recipe.Version}.");
        }

        var definitions = await BuildDefinitionsAsync(
            command.UserId,
            command.Ingredients,
            cancellationToken);
        recipe.Update(
            command.Name,
            command.Description,
            command.TotalPreparedWeightGrams,
            definitions,
            command.ChangeReason,
            command.ChangeSource,
            timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(recipe, recipe.Version);
    }

    public async Task<RecipeResult> ArchiveRecipeAsync(
        ArchiveRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateId(command.Id, nameof(command.Id));
        ValidateUserId(command.UserId);
        ValidateText(command.Reason, MaximumAuditTextLength, nameof(command.Reason), required: false);
        ValidateText(command.Source, MaximumSourceLength, nameof(command.Source), required: true);
        var recipe = await GetRecipeEntityAsync(command.Id, command.UserId, true, cancellationToken);
        if (recipe.IsArchived)
        {
            throw new ApplicationConflictException("The recipe is already archived.");
        }

        recipe.Archive(command.Reason, command.Source, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Map(recipe, recipe.Version);
    }

    public async Task<RecipeNutritionResult> CalculateRecipeNutritionAsync(
        CalculateRecipeNutritionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateId(query.Id, nameof(query.Id));
        ValidateUserId(query.UserId);
        ValidateOptionalVersion(query.Version);
        var recipe = await GetRecipeEntityAsync(query.Id, query.UserId, false, cancellationToken);
        var version = GetVersion(recipe, query.Version);
        var nutrition = NutritionCalculator.RoundForBoundary(NutritionCalculator.CalculateRecipe(version));
        return MapNutrition(recipe.Id, version, nutrition, null);
    }

    public async Task<RecipeNutritionResult> CalculateRecipePortionAsync(
        CalculateRecipePortionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateId(query.Id, nameof(query.Id));
        ValidateUserId(query.UserId);
        ValidateOptionalVersion(query.Version);
        ValidateWeight(query.WeightGrams, nameof(query.WeightGrams));
        var recipe = await GetRecipeEntityAsync(query.Id, query.UserId, false, cancellationToken);
        var version = GetVersion(recipe, query.Version);
        if (version.TotalPreparedWeightGrams is null)
        {
            throw new ApplicationValidationException(
                "TotalPreparedWeightGrams is required for portion calculations.",
                nameof(version.TotalPreparedWeightGrams));
        }

        var nutrition = NutritionCalculator.RoundForBoundary(
            NutritionCalculator.CalculateRecipePortion(version, query.WeightGrams));
        return MapNutrition(recipe.Id, version, nutrition, query.WeightGrams);
    }

    private async Task<Recipe> GetRecipeEntityAsync(
        Guid id,
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(id, userId, trackChanges, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Recipe), id);
    }

    private async Task<IReadOnlyList<RecipeIngredientDefinition>> BuildDefinitionsAsync(
        Guid userId,
        IReadOnlyList<RecipeIngredientInput> ingredients,
        CancellationToken cancellationToken)
    {
        var productIds = ingredients.Select(item => item.FoodProductId).ToArray();
        var products = await repository.GetVisibleFoodProductsAsync(userId, productIds, cancellationToken);
        var definitions = new List<RecipeIngredientDefinition>(ingredients.Count);
        foreach (var ingredient in ingredients)
        {
            if (!products.TryGetValue(ingredient.FoodProductId, out var product))
            {
                throw new EntityNotFoundException(nameof(FoodProduct), ingredient.FoodProductId);
            }

            definitions.Add(new RecipeIngredientDefinition(
                Guid.NewGuid(),
                ingredient.FoodProductId,
                ingredient.WeightGrams,
                product.NutritionPer100g));
        }

        return definitions;
    }

    private static void ValidateRecipeInput(
        string name,
        string? description,
        decimal? preparedWeight,
        IReadOnlyList<RecipeIngredientInput> ingredients,
        string? reason,
        string source)
    {
        ValidateText(name, MaximumNameLength, nameof(name), required: true);
        ValidateText(description, MaximumDescriptionLength, nameof(description), required: false);
        ValidateText(reason, MaximumAuditTextLength, nameof(reason), required: false);
        ValidateText(source, MaximumSourceLength, nameof(source), required: true);
        if (preparedWeight is not null)
        {
            ValidateWeight(preparedWeight.Value, nameof(preparedWeight));
        }

        if (ingredients is null || ingredients.Count is < 1 or > MaximumIngredients)
        {
            throw new ApplicationValidationException(
                $"A recipe must contain between 1 and {MaximumIngredients} ingredients.",
                nameof(ingredients));
        }

        if (ingredients.Any(item => item.FoodProductId == Guid.Empty))
        {
            throw new ApplicationValidationException("Ingredient product identifiers cannot be empty.", nameof(ingredients));
        }

        if (ingredients.Select(item => item.FoodProductId).Distinct().Count() != ingredients.Count)
        {
            throw new ApplicationValidationException("A product can appear only once in a recipe.", nameof(ingredients));
        }

        foreach (var ingredient in ingredients)
        {
            ValidateWeight(ingredient.WeightGrams, nameof(ingredient.WeightGrams));
        }
    }

    private static void ValidateText(string? value, int maximumLength, string parameterName, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                throw new ApplicationValidationException("The value is required.", parameterName);
            }

            return;
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new ApplicationValidationException(
                $"The value cannot exceed {maximumLength} characters.",
                parameterName);
        }
    }

    private static void ValidateWeight(decimal value, string parameterName)
    {
        if (value is <= 0 or > MaximumWeightGrams)
        {
            throw new ApplicationValidationException(
                $"The weight must be greater than 0 and at most {MaximumWeightGrams} grams.",
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

    private static void ValidateUserId(Guid userId) => ValidateId(userId, nameof(userId));

    private static void ValidateOptionalVersion(int? version)
    {
        if (version <= 0)
        {
            throw new ApplicationValidationException("The version must be positive when specified.", nameof(version));
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

    private static RecipeVersion GetVersion(Recipe recipe, int? requestedVersion)
    {
        var version = requestedVersion ?? recipe.Version;
        var result = recipe.Versions.SingleOrDefault(item => item.Version == version);
        return result ?? throw new EntityNotFoundException("RecipeVersion", recipe.Id);
    }

    private static RecipeResult Map(Recipe recipe, int selectedVersion)
    {
        var version = recipe.Versions.SingleOrDefault(item => item.Version == selectedVersion)
            ?? throw new EntityNotFoundException("RecipeVersion", recipe.Id);
        return new RecipeResult(
            recipe.Id,
            recipe.UserId,
            recipe.Version,
            recipe.IsArchived,
            recipe.ArchivedAtUtc,
            recipe.ArchiveReason,
            recipe.ArchiveSource,
            recipe.CreatedAtUtc,
            recipe.UpdatedAtUtc,
            MapVersion(version),
            recipe.Versions.Select(item => item.Version).Order().ToArray());
    }

    private static RecipeVersionResult MapVersion(RecipeVersion version)
    {
        return new RecipeVersionResult(
            version.Version,
            version.Name,
            version.Description,
            version.TotalPreparedWeightGrams,
            version.ChangeReason,
            version.ChangeSource,
            version.ChangedAtUtc,
            version.Ingredients.Select(item => new RecipeIngredientResult(
                item.FoodProductId,
                item.WeightGrams,
                item.NutritionPer100gSnapshot.Calories,
                item.NutritionPer100gSnapshot.ProteinGrams,
                item.NutritionPer100gSnapshot.FatGrams,
                item.NutritionPer100gSnapshot.CarbohydrateGrams)).ToArray());
    }

    private static RecipeSummaryResult MapSummary(Recipe recipe)
    {
        return new RecipeSummaryResult(
            recipe.Id,
            recipe.UserId,
            recipe.Name,
            recipe.Description,
            recipe.TotalPreparedWeightGrams,
            recipe.Version,
            recipe.IsArchived,
            recipe.UpdatedAtUtc);
    }

    private static RecipeNutritionResult MapNutrition(
        Guid recipeId,
        RecipeVersion version,
        NutritionValues nutrition,
        decimal? portionWeight)
    {
        return new RecipeNutritionResult(
            recipeId,
            version.Version,
            version.TotalPreparedWeightGrams,
            nutrition.Calories,
            nutrition.ProteinGrams,
            nutrition.FatGrams,
            nutrition.CarbohydrateGrams,
            portionWeight);
    }
}
