using System.Text.Json;
using System.Text.Json.Nodes;

namespace NutritionTracker.Application.Tools;

public static class ToolCatalog
{
    private static readonly ToolErrorDefinition ValidationError =
        new("validation_error", "Arguments failed backend validation.", false);
    private static readonly ToolErrorDefinition NotFoundError =
        new("not_found", "The requested user-owned or visible entity was not found.", false);
    private static readonly ToolErrorDefinition ConflictError =
        new("conflict", "The operation conflicts with current state or idempotency history.", false);
    private static readonly ToolErrorDefinition ConfirmationError =
        new("confirmation_required", "Fresh user confirmation is required before execution.", true);

    private static readonly ToolIdempotencyPolicy QueryIdempotency = new(
        ToolIdempotencyRequirement.NotApplicable,
        "Read-only. Repeated calls do not mutate state.");

    private static readonly ToolIdempotencyPolicy MutationIdempotency = new(
        ToolIdempotencyRequirement.Required,
        "The backend supplies the key in ToolInvocationContext. Repeating the same key and canonical " +
        "arguments returns the original structured result; reusing it with different arguments returns conflict.");

    private static readonly IReadOnlyList<ToolDefinition> Definitions =
    [
        Define(
            "search_foods",
            "Search visible food products. Never auto-selects when multiple matches are returned.",
            typeof(SearchFoodsArguments),
            Object(
                Props(("query", Text(1, 200)), ("limit", Integer(1, 25))),
                ["query"]),
            typeof(SearchFoodsToolResult),
            "Ordered candidates and requires_selection=true when more than one candidate remains.",
            [ValidationError],
            ToolConfirmationRequirement.None,
            "Read-only.",
            QueryIdempotency),
        Define(
            "get_food",
            "Get one food product by an explicit identifier selected by the user or prior tool result.",
            typeof(GetFoodArguments),
            Object(Props(("food_product_id", Uuid())), ["food_product_id"]),
            typeof(FoodToolResult),
            "The visible food product, including validated per-100-gram nutrition.",
            [ValidationError, NotFoundError],
            ToolConfirmationRequirement.None,
            "Read-only.",
            QueryIdempotency),
        Define(
            "create_food",
            "Create a personal food from nutrition values explicitly supplied by the user, such as a label.",
            typeof(CreateFoodArguments),
            FoodMutationSchema(includeId: false),
            typeof(FoodToolResult),
            "The persisted food after backend range and ownership validation.",
            [ValidationError, ConflictError],
            ToolConfirmationRequirement.None,
            "Creation is non-destructive; the backend still validates that values came from user intent.",
            MutationIdempotency),
        Define(
            "update_food",
            "Replace editable fields of an existing personal food. Never updates global or another user's food.",
            typeof(UpdateFoodArguments),
            FoodMutationSchema(includeId: true),
            typeof(FoodToolResult),
            "The updated persisted food.",
            [ValidationError, NotFoundError, ConflictError, ConfirmationError],
            ToolConfirmationRequirement.Required,
            "Changing source nutrition can affect future calculations and requires explicit user confirmation.",
            MutationIdempotency),
        Define(
            "search_recipes",
            "Search the current user's recipes. Never auto-selects when multiple matches are returned.",
            typeof(SearchRecipesArguments),
            Object(
                Props(
                    ("query", Text(1, 200)),
                    ("include_archived", Boolean()),
                    ("limit", Integer(1, 25))),
                ["query"]),
            typeof(SearchRecipesToolResult),
            "Ordered recipe candidates and requires_selection=true when multiple candidates remain.",
            [ValidationError],
            ToolConfirmationRequirement.None,
            "Read-only.",
            QueryIdempotency),
        Define(
            "get_recipe",
            "Get the current or an explicitly requested historical version of a user-owned recipe.",
            typeof(GetRecipeArguments),
            Object(
                Props(("recipe_id", Uuid()), ("version", Integer(1, int.MaxValue))),
                ["recipe_id"]),
            typeof(RecipeToolResult),
            "Recipe composition plus authoritative backend-calculated total nutrition.",
            [ValidationError, NotFoundError],
            ToolConfirmationRequirement.None,
            "Read-only.",
            QueryIdempotency),
        Define(
            "create_recipe",
            "Create a recipe from explicit food identifiers and weights; the backend calculates nutrition.",
            typeof(CreateRecipeArguments),
            RecipeMutationSchema(includeIdAndVersion: false),
            typeof(RecipeToolResult),
            "The persisted version-1 recipe and authoritative total nutrition.",
            [ValidationError, NotFoundError, ConflictError],
            ToolConfirmationRequirement.None,
            "Creation is non-destructive.",
            MutationIdempotency),
        Define(
            "update_recipe",
            "Create a new immutable version of an existing recipe using explicit ingredient identifiers and weights.",
            typeof(UpdateRecipeArguments),
            RecipeMutationSchema(includeIdAndVersion: true),
            typeof(RecipeToolResult),
            "The newly persisted recipe version and authoritative total nutrition.",
            [ValidationError, NotFoundError, ConflictError, ConfirmationError],
            ToolConfirmationRequirement.Required,
            "Recipe composition changes affect future diary calculations and require explicit user confirmation.",
            MutationIdempotency),
        Define(
            "add_food_to_diary",
            "Add a selected food and consumed weight to the diary; nutrition is loaded and calculated by the backend.",
            typeof(AddFoodToDiaryArguments),
            AddFoodToDiarySchema(),
            typeof(DiaryMutationToolResult),
            "The created item, authoritative nutrition snapshot, replay flag, and refreshed daily summary.",
            [ValidationError, NotFoundError, ConflictError],
            ToolConfirmationRequirement.None,
            "Adds a reversible diary entry.",
            MutationIdempotency),
        Define(
            "add_recipe_to_diary",
            "Add a selected recipe by consumed weight or fraction; nutrition is calculated by the backend.",
            typeof(AddRecipeToDiaryArguments),
            AddRecipeToDiarySchema(),
            typeof(DiaryMutationToolResult),
            "The created item, persisted recipe-version nutrition snapshot, replay flag, and daily summary.",
            [ValidationError, NotFoundError, ConflictError],
            ToolConfirmationRequirement.None,
            "Adds a reversible diary entry.",
            MutationIdempotency),
        Define(
            "update_diary_item",
            "Correct an item's consumed weight or move it to another time and meal type.",
            typeof(UpdateDiaryItemArguments),
            UpdateDiaryItemSchema(),
            typeof(DiaryMutationToolResult),
            "The corrected item with backend-recalculated snapshot and refreshed daily summary.",
            [ValidationError, NotFoundError, ConflictError, ConfirmationError],
            ToolConfirmationRequirement.Required,
            "Changing historical diary data requires explicit user confirmation.",
            MutationIdempotency),
        Define(
            "delete_diary_item",
            "Delete one explicitly identified diary item.",
            typeof(DeleteDiaryItemArguments),
            Object(
                Props(("diary_item_id", Uuid()), ("user_intent", Text(1, 500))),
                ["diary_item_id", "user_intent"]),
            typeof(DiaryMutationToolResult),
            "The deleted item identifier, replay flag, and refreshed daily summary.",
            [ValidationError, NotFoundError, ConflictError, ConfirmationError],
            ToolConfirmationRequirement.Required,
            "Deletion is destructive and always requires explicit user confirmation.",
            MutationIdempotency),
        Define(
            "get_daily_summary",
            "Get meals, consumed nutrition, effective target, and remaining values for a user-local date.",
            typeof(GetDailySummaryArguments),
            Object(Props(("date", Date())), ["date"]),
            typeof(DailySummaryToolResult),
            "Snapshot-based daily totals resolved in the user's persisted time zone.",
            [ValidationError, NotFoundError],
            ToolConfirmationRequirement.None,
            "Read-only.",
            QueryIdempotency),
        Define(
            "get_recent_meals",
            "Get diary meals for up to 30 recent user-local calendar days.",
            typeof(GetRecentMealsArguments),
            Object(
                Props(("ending_on", Date()), ("days", Integer(1, 30))),
                []),
            typeof(RecentMealsToolResult),
            "Meals in chronological user-local date range with persisted nutrition snapshots.",
            [ValidationError, NotFoundError],
            ToolConfirmationRequirement.None,
            "Read-only.",
            QueryIdempotency),
        Define(
            "get_nutrition_targets",
            "Get the effective nutrition target and remaining values for a user-local date.",
            typeof(GetNutritionTargetsArguments),
            Object(Props(("date", Date())), ["date"]),
            typeof(NutritionTargetsToolResult),
            "Effective persisted target and server-calculated remaining values, or null when no target exists.",
            [ValidationError, NotFoundError],
            ToolConfirmationRequirement.None,
            "Read-only.",
            QueryIdempotency)
    ];

    public static IReadOnlyList<ToolDefinition> All => Definitions;

    public static ToolDefinition GetRequired(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Definitions.Single(definition => string.Equals(definition.Name, name, StringComparison.Ordinal));
    }

    private static ToolDefinition Define(
        string name,
        string purpose,
        Type argumentsType,
        JsonElement schema,
        Type resultType,
        string resultDescription,
        IReadOnlyList<ToolErrorDefinition> errors,
        ToolConfirmationRequirement confirmation,
        string confirmationDescription,
        ToolIdempotencyPolicy idempotency)
    {
        var required = schema.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        return new ToolDefinition(
            name,
            purpose,
            argumentsType,
            schema,
            required,
            resultType,
            resultDescription,
            errors,
            confirmation,
            confirmationDescription,
            idempotency);
    }

    private static JsonElement FoodMutationSchema(bool includeId)
    {
        var properties = Props(
            ("name", Text(1, 200)),
            ("brand", Text(0, 200)),
            ("calories_per100g", Number(0, 1000)),
            ("protein_per100g", Number(0, 100)),
            ("fat_per100g", Number(0, 100)),
            ("carbohydrates_per100g", Number(0, 100)),
            ("fiber_per100g", Number(0, 100)),
            ("user_intent", Text(1, 500)));
        var required = new List<string>
        {
            "name", "calories_per100g", "protein_per100g", "fat_per100g",
            "carbohydrates_per100g", "user_intent"
        };
        if (includeId)
        {
            properties.Insert(0, "food_product_id", Uuid());
            required.Insert(0, "food_product_id");
        }

        return Object(properties, required);
    }

    private static JsonElement RecipeMutationSchema(bool includeIdAndVersion)
    {
        var ingredient = ObjectNode(
            Props(
                ("food_product_id", Uuid()),
                ("weight_grams", Number(0, ToolArgumentValidation.MaximumWeightGrams, true))),
            ["food_product_id", "weight_grams"]);
        var properties = Props(
            ("name", Text(1, 200)),
            ("description", Text(0, 2000)),
            ("total_prepared_weight_grams", Number(
                0, ToolArgumentValidation.MaximumWeightGrams, true)),
            ("ingredients", Array(ingredient, 1, 100)),
            ("change_reason", Text(0, 500)),
            ("user_intent", Text(1, 500)));
        var required = new List<string> { "name", "ingredients", "user_intent" };
        if (includeIdAndVersion)
        {
            properties.Insert(0, "expected_version", Integer(1, int.MaxValue));
            properties.Insert(0, "recipe_id", Uuid());
            required.Insert(0, "expected_version");
            required.Insert(0, "recipe_id");
        }

        return Object(properties, required);
    }

    private static JsonElement AddFoodToDiarySchema() => Object(
        Props(
            ("food_product_id", Uuid()),
            ("weight_grams", Number(0, ToolArgumentValidation.MaximumWeightGrams, true)),
            ("occurred_at", Timestamp()),
            ("meal_type", MealType()),
            ("user_intent", Text(1, 500))),
        ["food_product_id", "weight_grams", "occurred_at", "meal_type", "user_intent"]);

    private static JsonElement AddRecipeToDiarySchema()
    {
        var schema = ObjectNode(
            Props(
                ("recipe_id", Uuid()),
                ("weight_grams", Number(0, ToolArgumentValidation.MaximumWeightGrams, true)),
                ("fraction", Number(0, 1, true)),
                ("occurred_at", Timestamp()),
                ("meal_type", MealType()),
                ("user_intent", Text(1, 500))),
            ["recipe_id", "occurred_at", "meal_type", "user_intent"]);
        schema["oneOf"] = new JsonArray(
            RequiredOnly("weight_grams", "fraction"),
            RequiredOnly("fraction", "weight_grams"));
        return ToElement(schema);
    }

    private static JsonElement UpdateDiaryItemSchema()
    {
        var schema = ObjectNode(
            Props(
                ("diary_item_id", Uuid()),
                ("weight_grams", Number(0, ToolArgumentValidation.MaximumWeightGrams, true)),
                ("occurred_at", Timestamp()),
                ("meal_type", MealType()),
                ("user_intent", Text(1, 500))),
            ["diary_item_id", "user_intent"]);
        schema["oneOf"] = new JsonArray(
            RequiredOnly("weight_grams", "occurred_at", "meal_type"),
            RequiredOnly(["occurred_at", "meal_type"], "weight_grams"));
        return ToElement(schema);
    }

    private static JsonObject RequiredOnly(string required, params string[] prohibited) =>
        RequiredOnly([required], prohibited);

    private static JsonObject RequiredOnly(IReadOnlyList<string> required, params string[] prohibited)
    {
        return new JsonObject
        {
            ["required"] = new JsonArray(required.Select(item => JsonValue.Create(item)).ToArray()),
            ["not"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(prohibited.Select(item =>
                    (JsonNode)new JsonObject
                    {
                        ["required"] = new JsonArray(JsonValue.Create(item))
                    }).ToArray())
            }
        };
    }

    private static JsonElement Object(JsonObject properties, IReadOnlyList<string> required) =>
        ToElement(ObjectNode(properties, required));

    private static JsonObject ObjectNode(JsonObject properties, IReadOnlyList<string> required)
    {
        return new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
            ["required"] = new JsonArray(required.Select(item => JsonValue.Create(item)).ToArray())
        };
    }

    private static JsonObject Props(params (string Name, JsonNode Schema)[] properties)
    {
        var result = new JsonObject();
        foreach (var property in properties)
        {
            result.Add(property.Name, property.Schema);
        }

        return result;
    }

    private static JsonObject Text(int minimumLength, int maximumLength) => new()
    {
        ["type"] = "string",
        ["minLength"] = minimumLength,
        ["maxLength"] = maximumLength
    };

    private static JsonObject Uuid() => new() { ["type"] = "string", ["format"] = "uuid" };

    private static JsonObject Date() => new() { ["type"] = "string", ["format"] = "date" };

    private static JsonObject Timestamp() => new() { ["type"] = "string", ["format"] = "date-time" };

    private static JsonObject Boolean() => new() { ["type"] = "boolean" };

    private static JsonObject Integer(int minimum, int maximum) => new()
    {
        ["type"] = "integer",
        ["minimum"] = minimum,
        ["maximum"] = maximum
    };

    private static JsonObject Number(decimal minimum, decimal maximum, bool exclusiveMinimum = false) => new()
    {
        ["type"] = "number",
        [exclusiveMinimum ? "exclusiveMinimum" : "minimum"] = minimum,
        ["maximum"] = maximum
    };

    private static JsonObject MealType() => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray("breakfast", "lunch", "dinner", "snack", "other")
    };

    private static JsonObject Array(JsonNode items, int minimumItems, int maximumItems) => new()
    {
        ["type"] = "array",
        ["items"] = items,
        ["minItems"] = minimumItems,
        ["maxItems"] = maximumItems
    };

    private static JsonElement ToElement(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
