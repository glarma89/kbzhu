using System.Text.Json;
using NutritionTracker.Application.Tools;
using NutritionTracker.Domain.Meals;

namespace NutritionTracker.Application.Tests.Tools;

public sealed class ToolContractTests
{
    private static readonly string[] ExpectedNames =
    [
        "search_foods",
        "get_food",
        "create_food",
        "update_food",
        "search_recipes",
        "get_recipe",
        "create_recipe",
        "update_recipe",
        "add_food_to_diary",
        "add_recipe_to_diary",
        "update_diary_item",
        "delete_diary_item",
        "get_daily_summary",
        "get_recent_meals",
        "get_nutrition_targets"
    ];

    [Fact]
    public void CatalogContainsExactlyTheAllowlistedTools()
    {
        Assert.Equal(ExpectedNames, ToolCatalog.All.Select(definition => definition.Name));
        Assert.Equal(ExpectedNames.Length, ToolCatalog.All.Select(definition => definition.Name).Distinct().Count());
        Assert.All(ToolCatalog.All, definition =>
        {
            Assert.True(typeof(IToolArguments).IsAssignableFrom(definition.ArgumentsType));
            Assert.Equal("object", definition.ArgumentsJsonSchema.GetProperty("type").GetString());
            Assert.False(definition.ArgumentsJsonSchema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(
                definition.RequiredArguments,
                definition.ArgumentsJsonSchema.GetProperty("required")
                    .EnumerateArray()
                    .Select(item => item.GetString()));
            Assert.NotEmpty(definition.Purpose);
            Assert.NotEmpty(definition.ResultDescription);
            Assert.NotEmpty(definition.Errors);
        });
    }

    [Fact]
    public void MutationPoliciesKeepIdentityConfirmationAndIdempotencyOutOfModelArguments()
    {
        var mutations = ToolCatalog.All.Where(definition =>
            definition.Idempotency.Requirement == ToolIdempotencyRequirement.Required).ToArray();

        Assert.Equal(8, mutations.Length);
        Assert.All(mutations, definition =>
        {
            var properties = definition.ArgumentsJsonSchema.GetProperty("properties");
            Assert.False(properties.TryGetProperty("user_id", out _));
            Assert.False(properties.TryGetProperty("idempotency_key", out _));
            Assert.False(properties.TryGetProperty("confirmation", out _));
        });

        Assert.Equal(
            ["update_food", "update_recipe", "update_diary_item", "delete_diary_item"],
            ToolCatalog.All
                .Where(definition =>
                    definition.ConfirmationRequirement == ToolConfirmationRequirement.Required)
                .Select(definition => definition.Name));
    }

    [Fact]
    public void DiaryAdditionSchemasDoNotAcceptModelCalculatedNutrition()
    {
        foreach (var name in new[] { "add_food_to_diary", "add_recipe_to_diary" })
        {
            var properties = ToolCatalog.GetRequired(name).ArgumentsJsonSchema.GetProperty("properties");
            Assert.False(properties.TryGetProperty("calories", out _));
            Assert.False(properties.TryGetProperty("protein_grams", out _));
            Assert.False(properties.TryGetProperty("fat_grams", out _));
            Assert.False(properties.TryGetProperty("carbohydrate_grams", out _));
        }
    }

    [Fact]
    public void ArgumentsSerializeWithStrictSnakeCaseAndStringEnums()
    {
        var arguments = new AddFoodToDiaryArguments(
            Guid.Parse("f4306fb3-1b90-4025-bbfe-b9521ca7331d"),
            125.5m,
            new DateTimeOffset(2026, 7, 23, 19, 30, 0, TimeSpan.FromHours(3)),
            MealType.Dinner,
            "The user said they ate this for dinner.");

        var json = ToolJson.Serialize(arguments);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("dinner", root.GetProperty("meal_type").GetString());
        Assert.Equal(125.5m, root.GetProperty("weight_grams").GetDecimal());
        Assert.True(root.TryGetProperty("food_product_id", out _));
        Assert.False(root.TryGetProperty("FoodProductId", out _));
        Assert.False(root.TryGetProperty("user_id", out _));
        Assert.False(root.TryGetProperty("calories", out _));
    }

    [Fact]
    public void SerializedFoodArgumentsUseTheSameNamesAsTheirJsonSchema()
    {
        var arguments = new CreateFoodArguments(
            "Yogurt", null, 60m, 10m, 0m, 4m, null, "Values read from the user's label.");

        var json = ToolJson.Serialize(arguments);
        using var document = JsonDocument.Parse(json);
        var schemaProperties = ToolCatalog.GetRequired("create_food")
            .ArgumentsJsonSchema.GetProperty("properties");

        foreach (var property in document.RootElement.EnumerateObject())
        {
            Assert.True(schemaProperties.TryGetProperty(property.Name, out _), property.Name);
        }

        Assert.False(document.RootElement.TryGetProperty("brand", out _));
        Assert.False(document.RootElement.TryGetProperty("fiber_per100g", out _));
    }

    [Fact]
    public void ValidJsonDeserializesToTypedArguments()
    {
        const string json = """
            {
              "recipe_id": "e8bfae8e-d188-45d4-94b1-f82d6b3c12f7",
              "fraction": 0.5,
              "occurred_at": "2026-07-23T19:30:00+03:00",
              "meal_type": "dinner",
              "user_intent": "The user said they ate half of the recipe."
            }
            """;

        var arguments = ToolJson.DeserializeArguments<AddRecipeToDiaryArguments>(json);

        Assert.Equal(0.5m, arguments.Fraction);
        Assert.Null(arguments.WeightGrams);
        Assert.Equal(MealType.Dinner, arguments.MealType);
        Assert.Equal(TimeSpan.FromHours(3), arguments.OccurredAt.Offset);
    }

    [Fact]
    public void UnknownJsonPropertiesAreRejected()
    {
        const string json = """
            {
              "food_product_id": "f4306fb3-1b90-4025-bbfe-b9521ca7331d",
              "weight_grams": 100,
              "occurred_at": "2026-07-23T19:30:00+03:00",
              "meal_type": "dinner",
              "user_intent": "The user asked to log it.",
              "calories": 999
            }
            """;

        Assert.Throws<JsonException>(() => ToolJson.DeserializeArguments<AddFoodToDiaryArguments>(json));
    }

    [Fact]
    public void MissingAndOutOfRangeValuesReturnFieldErrors()
    {
        const string json = """
            {
              "food_product_id": "00000000-0000-0000-0000-000000000000",
              "weight_grams": 0,
              "occurred_at": "0001-01-01T00:00:00+00:00",
              "user_intent": " "
            }
            """;

        var exception = Assert.Throws<ToolArgumentsValidationException>(
            () => ToolJson.DeserializeArguments<AddFoodToDiaryArguments>(json));

        Assert.Contains(exception.Errors, error => error.Field == "food_product_id");
        Assert.Contains(exception.Errors, error => error.Field == "weight_grams");
        Assert.Contains(exception.Errors, error => error.Field == "occurred_at");
        Assert.Contains(exception.Errors, error => error.Field == "meal_type");
        Assert.Contains(exception.Errors, error => error.Field == "user_intent");
    }

    [Fact]
    public void RecipeDiaryArgumentsRequireExactlyOneQuantityMode()
    {
        var both = new AddRecipeToDiaryArguments(
            Guid.NewGuid(), 100m, 0.5m, DateTimeOffset.UtcNow, MealType.Lunch, "Correct intent");
        var neither = both with { WeightGrams = null, Fraction = null };

        Assert.Contains(both.Validate(), error => error.Code == "exactly_one");
        Assert.Contains(neither.Validate(), error => error.Code == "exactly_one");
    }

    [Fact]
    public void RecipeValidationRejectsDuplicateProductsAndInvalidWeights()
    {
        var foodId = Guid.NewGuid();
        var arguments = new CreateRecipeArguments(
            "Recipe",
            null,
            300m,
            [new(foodId, 0m), new(foodId, 100m)],
            null,
            "The user supplied this recipe.");

        var errors = arguments.Validate();

        Assert.Contains(errors, error => error.Field == "ingredients[0].weight_grams");
        Assert.Contains(errors, error => error.Field == "ingredients" && error.Code == "duplicate");
    }

    [Fact]
    public void StructuredExecutionResultSerializesWithoutResponseText()
    {
        var food = new FoodToolResult(
            Guid.NewGuid(), "Yogurt", null, 60m, 10m, 0m, 4m, null,
            "user_label", false, DateTimeOffset.UtcNow);
        var result = ToolExecutionResults.Success(food);

        var json = ToolJson.Serialize(result);
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.GetProperty("is_success").GetBoolean());
        Assert.True(document.RootElement.TryGetProperty("result", out _));
        Assert.False(document.RootElement.TryGetProperty("error", out _));
        Assert.False(document.RootElement.TryGetProperty("response_text", out _));
    }
}
