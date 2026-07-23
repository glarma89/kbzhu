using NutritionTracker.Domain.Meals;

namespace NutritionTracker.Application.Tools;

public sealed record SearchFoodsArguments(string Query, int Limit = 10) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.RequiredText(errors, "query", Query, 200);
        ToolArgumentValidation.Limit(errors, "limit", Limit, 25);
        return errors;
    }
}

public sealed record GetFoodArguments(Guid FoodProductId) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.Id(errors, "food_product_id", FoodProductId);
        return errors;
    }
}

public sealed record CreateFoodArguments(
    string Name,
    string? Brand,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal FatPer100g,
    decimal CarbohydratesPer100g,
    decimal? FiberPer100g,
    string UserIntent) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate() =>
        ValidateFood(Name, Brand, CaloriesPer100g, ProteinPer100g, FatPer100g,
            CarbohydratesPer100g, FiberPer100g, UserIntent);

    internal static IReadOnlyList<ToolValidationError> ValidateFood(
        string name,
        string? brand,
        decimal caloriesPer100g,
        decimal proteinPer100g,
        decimal fatPer100g,
        decimal carbohydratesPer100g,
        decimal? fiberPer100g,
        string userIntent)
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.RequiredText(errors, "name", name, 200);
        ToolArgumentValidation.OptionalText(errors, "brand", brand, 200);
        ToolArgumentValidation.Range(errors, "calories_per100g", caloriesPer100g, 0, 1000);
        ToolArgumentValidation.Range(errors, "protein_per100g", proteinPer100g, 0, 100);
        ToolArgumentValidation.Range(errors, "fat_per100g", fatPer100g, 0, 100);
        ToolArgumentValidation.Range(errors, "carbohydrates_per100g", carbohydratesPer100g, 0, 100);
        if (fiberPer100g is not null)
        {
            ToolArgumentValidation.Range(errors, "fiber_per100g", fiberPer100g.Value, 0, 100);
        }

        ToolArgumentValidation.RequiredText(errors, "user_intent", userIntent, 500);
        return errors;
    }
}

public sealed record UpdateFoodArguments(
    Guid FoodProductId,
    string Name,
    string? Brand,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal FatPer100g,
    decimal CarbohydratesPer100g,
    decimal? FiberPer100g,
    string UserIntent) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = CreateFoodArguments.ValidateFood(
            Name, Brand, CaloriesPer100g, ProteinPer100g, FatPer100g,
            CarbohydratesPer100g, FiberPer100g, UserIntent).ToList();
        ToolArgumentValidation.Id(errors, "food_product_id", FoodProductId);
        return errors;
    }
}

public sealed record SearchRecipesArguments(string Query, bool IncludeArchived = false, int Limit = 10)
    : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.RequiredText(errors, "query", Query, 200);
        ToolArgumentValidation.Limit(errors, "limit", Limit, 25);
        return errors;
    }
}

public sealed record GetRecipeArguments(Guid RecipeId, int? Version = null) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.Id(errors, "recipe_id", RecipeId);
        ToolArgumentValidation.Version(errors, "version", Version);
        return errors;
    }
}

public sealed record RecipeIngredientArguments(Guid FoodProductId, decimal WeightGrams)
{
    internal void Validate(ICollection<ToolValidationError> errors, int index)
    {
        ToolArgumentValidation.Id(errors, $"ingredients[{index}].food_product_id", FoodProductId);
        ToolArgumentValidation.PositiveWeight(errors, $"ingredients[{index}].weight_grams", WeightGrams);
    }
}

public sealed record CreateRecipeArguments(
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    IReadOnlyList<RecipeIngredientArguments> Ingredients,
    string? ChangeReason,
    string UserIntent) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate() =>
        ValidateRecipe(Name, Description, TotalPreparedWeightGrams, Ingredients, ChangeReason, UserIntent);

    internal static IReadOnlyList<ToolValidationError> ValidateRecipe(
        string name,
        string? description,
        decimal? totalPreparedWeightGrams,
        IReadOnlyList<RecipeIngredientArguments>? ingredients,
        string? changeReason,
        string userIntent)
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.RequiredText(errors, "name", name, 200);
        ToolArgumentValidation.OptionalText(errors, "description", description, 2000);
        ToolArgumentValidation.OptionalPositiveWeight(
            errors, "total_prepared_weight_grams", totalPreparedWeightGrams);
        ToolArgumentValidation.OptionalText(errors, "change_reason", changeReason, 500);
        ToolArgumentValidation.RequiredText(errors, "user_intent", userIntent, 500);
        if (ingredients is null || ingredients.Count == 0)
        {
            errors.Add(new ToolValidationError(
                "ingredients", "required", "At least one recipe ingredient is required."));
        }
        else if (ingredients.Count > 100)
        {
            errors.Add(new ToolValidationError(
                "ingredients", "max_items", "A recipe cannot contain more than 100 ingredients."));
        }
        else
        {
            for (var index = 0; index < ingredients.Count; index++)
            {
                ingredients[index].Validate(errors, index);
            }

            if (ingredients.Select(item => item.FoodProductId).Distinct().Count() != ingredients.Count)
            {
                errors.Add(new ToolValidationError(
                    "ingredients", "duplicate", "Each food product may appear only once."));
            }
        }

        return errors;
    }
}

public sealed record UpdateRecipeArguments(
    Guid RecipeId,
    int ExpectedVersion,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    IReadOnlyList<RecipeIngredientArguments> Ingredients,
    string? ChangeReason,
    string UserIntent) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = CreateRecipeArguments.ValidateRecipe(
            Name, Description, TotalPreparedWeightGrams, Ingredients, ChangeReason, UserIntent).ToList();
        ToolArgumentValidation.Id(errors, "recipe_id", RecipeId);
        ToolArgumentValidation.Version(errors, "expected_version", ExpectedVersion);
        return errors;
    }
}

public sealed record AddFoodToDiaryArguments(
    Guid FoodProductId,
    decimal WeightGrams,
    DateTimeOffset OccurredAt,
    MealType? MealType,
    string UserIntent) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.Id(errors, "food_product_id", FoodProductId);
        ToolArgumentValidation.PositiveWeight(errors, "weight_grams", WeightGrams);
        ToolArgumentValidation.OccurredAt(errors, "occurred_at", OccurredAt);
        ToolArgumentValidation.MealType(errors, "meal_type", MealType);
        ToolArgumentValidation.RequiredText(errors, "user_intent", UserIntent, 500);
        return errors;
    }
}

public sealed record AddRecipeToDiaryArguments(
    Guid RecipeId,
    decimal? WeightGrams,
    decimal? Fraction,
    DateTimeOffset OccurredAt,
    MealType? MealType,
    string UserIntent) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.Id(errors, "recipe_id", RecipeId);
        if ((WeightGrams is null) == (Fraction is null))
        {
            errors.Add(new ToolValidationError(
                "weight_grams", "exactly_one", "Specify exactly one of weight_grams or fraction."));
        }

        ToolArgumentValidation.OptionalPositiveWeight(errors, "weight_grams", WeightGrams);
        if (Fraction is not null)
        {
            ToolArgumentValidation.Range(errors, "fraction", Fraction.Value, 0.000001m, 1m);
        }

        ToolArgumentValidation.OccurredAt(errors, "occurred_at", OccurredAt);
        ToolArgumentValidation.MealType(errors, "meal_type", MealType);
        ToolArgumentValidation.RequiredText(errors, "user_intent", UserIntent, 500);
        return errors;
    }
}

public sealed record UpdateDiaryItemArguments(
    Guid DiaryItemId,
    decimal? WeightGrams,
    DateTimeOffset? OccurredAt,
    MealType? MealType,
    string UserIntent) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.Id(errors, "diary_item_id", DiaryItemId);
        var isWeightUpdate = WeightGrams is not null && OccurredAt is null && MealType is null;
        var isMove = WeightGrams is null && OccurredAt is not null && MealType is not null;
        if (!isWeightUpdate && !isMove)
        {
            errors.Add(new ToolValidationError(
                "weight_grams", "invalid_combination",
                "Specify either weight_grams, or both occurred_at and meal_type."));
        }

        ToolArgumentValidation.OptionalPositiveWeight(errors, "weight_grams", WeightGrams);
        if (OccurredAt is not null)
        {
            ToolArgumentValidation.OccurredAt(errors, "occurred_at", OccurredAt.Value);
        }

        if (MealType is not null)
        {
            ToolArgumentValidation.MealType(errors, "meal_type", MealType);
        }

        ToolArgumentValidation.RequiredText(errors, "user_intent", UserIntent, 500);
        return errors;
    }
}

public sealed record DeleteDiaryItemArguments(Guid DiaryItemId, string UserIntent) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        ToolArgumentValidation.Id(errors, "diary_item_id", DiaryItemId);
        ToolArgumentValidation.RequiredText(errors, "user_intent", UserIntent, 500);
        return errors;
    }
}

public sealed record GetDailySummaryArguments(DateOnly Date) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate() => ValidateDate("date", Date);

    internal static IReadOnlyList<ToolValidationError> ValidateDate(string field, DateOnly date)
    {
        if (date == default)
        {
            return [new ToolValidationError(field, "required", "A user-local calendar date is required.")];
        }

        return [];
    }
}

public sealed record GetRecentMealsArguments(DateOnly? EndingOn = null, int Days = 7) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate()
    {
        var errors = new List<ToolValidationError>();
        if (EndingOn is not null && EndingOn.Value == default)
        {
            errors.Add(new ToolValidationError(
                "ending_on", "invalid_date", "When supplied, ending_on must be a valid date."));
        }

        ToolArgumentValidation.Limit(errors, "days", Days, 30);
        return errors;
    }
}

public sealed record GetNutritionTargetsArguments(DateOnly Date) : IToolArguments
{
    public IReadOnlyList<ToolValidationError> Validate() => GetDailySummaryArguments.ValidateDate("date", Date);
}
