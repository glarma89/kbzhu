using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NutritionTracker.Application.Chat;
using NutritionTracker.Application.Common;
using NutritionTracker.Application.Foods;
using NutritionTracker.Application.Meals;
using NutritionTracker.Application.Recipes;
using NutritionTracker.Application.Tools;
using NutritionTracker.Domain.Commands;
using NutritionTracker.Infrastructure.Persistence;

namespace NutritionTracker.Infrastructure.Tools;

internal sealed partial class AllowlistedToolExecutor(
    IFoodProductService foodService,
    IRecipeService recipeService,
    IMealService mealService,
    NutritionDbContext context,
    TimeProvider timeProvider,
    ILogger<AllowlistedToolExecutor> logger) : IMessageToolExecutor
{
    public async Task<MessageToolExecutionOutcome> ExecuteAsync(
        MessageToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        MessageToolExecutionOutcome outcome;
        try
        {
            var definition = ToolCatalog.GetRequired(request.ToolName);
            var arguments = ToolJson.DeserializeArguments(
                request.ArgumentsJson, definition.ArgumentsType);
            outcome = definition.Idempotency.Requirement == ToolIdempotencyRequirement.Required
                ? await ExecuteMutationAsync(definition, arguments, request, cancellationToken)
                : await ExecuteCoreAsync(definition.Name, arguments, request.Context, cancellationToken);
        }
        catch (ToolArgumentsValidationException exception)
        {
            outcome = Failure(
                "validation_error",
                "Tool arguments failed backend validation.",
                exception.Errors.Count == 0 ? null : exception.Errors[0].Field);
        }
        catch (ApplicationValidationException exception)
        {
            outcome = Failure("validation_error", exception.Message, exception.ParameterName);
        }
        catch (EntityNotFoundException exception)
        {
            outcome = Failure("not_found", exception.Message);
        }
        catch (ApplicationConflictException exception)
        {
            outcome = Failure("conflict", exception.Message);
        }

        stopwatch.Stop();
        LogToolCompleted(logger, request.ToolName, stopwatch.ElapsedMilliseconds, outcome.IsSuccess);
        return outcome;
    }

    private async Task<MessageToolExecutionOutcome> ExecuteMutationAsync(
        ToolDefinition definition,
        IToolArguments arguments,
        MessageToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = request.Context.IdempotencyKey;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Failure("idempotency_required", "A backend idempotency key is required.");
        }

        var argumentsHash = ToolArgumentsHash.Create(definition.Name, request.ArgumentsJson);
        var existing = await context.ToolExecutions.AsNoTracking().SingleOrDefaultAsync(
            item => item.UserId == request.Context.UserId &&
                item.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return string.Equals(existing.ToolName, definition.Name, StringComparison.Ordinal) &&
                string.Equals(existing.ArgumentsHash, argumentsHash, StringComparison.Ordinal)
                ? new MessageToolExecutionOutcome(true, existing.ResultJson)
                : Failure("conflict", "The idempotency key was used for a different tool operation.");
        }

        if (definition.ConfirmationRequirement == ToolConfirmationRequirement.Required &&
            !HasValidConfirmation(request, argumentsHash))
        {
            return Failure("confirmation_required", definition.ConfirmationDescription);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var outcome = await ExecuteCoreAsync(
            definition.Name, arguments, request.Context, cancellationToken);
        if (!outcome.IsSuccess)
        {
            await transaction.RollbackAsync(cancellationToken);
            return outcome;
        }

        await context.ToolExecutions.AddAsync(
            new ToolExecution(
                Guid.NewGuid(),
                request.Context.UserId,
                idempotencyKey,
                definition.Name,
                argumentsHash,
                outcome.ResultJson,
                timeProvider.GetUtcNow()),
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return outcome;
    }

    private async Task<MessageToolExecutionOutcome> ExecuteCoreAsync(
        string toolName,
        IToolArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "search_foods" => Success(await SearchFoodsAsync(
                (SearchFoodsArguments)arguments, invocationContext, cancellationToken)),
            "get_food" => Success(await GetFoodAsync(
                (GetFoodArguments)arguments, invocationContext, cancellationToken)),
            "create_food" => Success(await CreateFoodAsync(
                (CreateFoodArguments)arguments, invocationContext, cancellationToken)),
            "update_food" => Success(await UpdateFoodAsync(
                (UpdateFoodArguments)arguments, invocationContext, cancellationToken)),
            "search_recipes" => Success(await SearchRecipesAsync(
                (SearchRecipesArguments)arguments, invocationContext, cancellationToken)),
            "get_recipe" => Success(await GetRecipeAsync(
                (GetRecipeArguments)arguments, invocationContext, cancellationToken)),
            "create_recipe" => Success(await CreateRecipeAsync(
                (CreateRecipeArguments)arguments, invocationContext, cancellationToken)),
            "update_recipe" => Success(await UpdateRecipeAsync(
                (UpdateRecipeArguments)arguments, invocationContext, cancellationToken)),
            "add_food_to_diary" => Success(await AddFoodToDiaryAsync(
                (AddFoodToDiaryArguments)arguments, invocationContext, cancellationToken)),
            "add_recipe_to_diary" => Success(await AddRecipeToDiaryAsync(
                (AddRecipeToDiaryArguments)arguments, invocationContext, cancellationToken)),
            "update_diary_item" => Success(await UpdateDiaryItemAsync(
                (UpdateDiaryItemArguments)arguments, invocationContext, cancellationToken)),
            "delete_diary_item" => Success(await DeleteDiaryItemAsync(
                (DeleteDiaryItemArguments)arguments, invocationContext, cancellationToken)),
            "get_daily_summary" => Success(await GetDailySummaryAsync(
                (GetDailySummaryArguments)arguments, invocationContext, cancellationToken)),
            "get_recent_meals" => Success(await GetRecentMealsAsync(
                (GetRecentMealsArguments)arguments, invocationContext, cancellationToken)),
            "get_nutrition_targets" => Success(await GetNutritionTargetsAsync(
                (GetNutritionTargetsArguments)arguments, invocationContext, cancellationToken)),
            _ => Failure("tool_not_allowed", "The requested tool is not registered.")
        };
    }

    private async Task<SearchFoodsToolResult> SearchFoodsAsync(
        SearchFoodsArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var matches = await foodService.SearchFoodProductsAsync(
            new SearchFoodProductsQuery(invocationContext.UserId, arguments.Query, arguments.Limit),
            cancellationToken);
        return new SearchFoodsToolResult(matches.Select(MapFood).ToArray(), matches.Count > 1);
    }

    private async Task<FoodToolResult> GetFoodAsync(
        GetFoodArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken) =>
        MapFood(await foodService.GetFoodProductByIdAsync(
            new GetFoodProductByIdQuery(arguments.FoodProductId, invocationContext.UserId),
            cancellationToken));

    private async Task<FoodToolResult> CreateFoodAsync(
        CreateFoodArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken) =>
        MapFood(await foodService.CreateFoodProductAsync(
            new CreateFoodProductCommand(
                invocationContext.UserId,
                arguments.Name,
                arguments.Brand,
                arguments.CaloriesPer100g,
                arguments.ProteinPer100g,
                arguments.FatPer100g,
                arguments.CarbohydratesPer100g,
                arguments.FiberPer100g,
                "User chat",
                false),
            cancellationToken));

    private async Task<FoodToolResult> UpdateFoodAsync(
        UpdateFoodArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken) =>
        MapFood(await foodService.UpdateFoodProductAsync(
            new UpdateFoodProductCommand(
                arguments.FoodProductId,
                invocationContext.UserId,
                arguments.Name,
                arguments.Brand,
                arguments.CaloriesPer100g,
                arguments.ProteinPer100g,
                arguments.FatPer100g,
                arguments.CarbohydratesPer100g,
                arguments.FiberPer100g,
                "User chat",
                false),
            cancellationToken));

    private async Task<SearchRecipesToolResult> SearchRecipesAsync(
        SearchRecipesArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var matches = await recipeService.SearchRecipesAsync(
            new SearchRecipesQuery(
                invocationContext.UserId,
                arguments.Query,
                arguments.IncludeArchived,
                arguments.Limit),
            cancellationToken);
        return new SearchRecipesToolResult(
            matches.Select(item => new RecipeSearchMatchToolResult(
                item.Id,
                item.Version,
                item.Name,
                item.Description,
                item.IsArchived,
                item.UpdatedAtUtc)).ToArray(),
            matches.Count > 1);
    }

    private async Task<RecipeToolResult> GetRecipeAsync(
        GetRecipeArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var recipe = await recipeService.GetRecipeAsync(
            new GetRecipeQuery(arguments.RecipeId, invocationContext.UserId, arguments.Version),
            cancellationToken);
        return await MapRecipeAsync(recipe, invocationContext.UserId, cancellationToken);
    }

    private async Task<RecipeToolResult> CreateRecipeAsync(
        CreateRecipeArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var recipe = await recipeService.CreateRecipeAsync(
            new CreateRecipeCommand(
                invocationContext.UserId,
                arguments.Name,
                arguments.Description,
                arguments.TotalPreparedWeightGrams,
                MapIngredients(arguments.Ingredients),
                arguments.ChangeReason,
                "User chat"),
            cancellationToken);
        return await MapRecipeAsync(recipe, invocationContext.UserId, cancellationToken);
    }

    private async Task<RecipeToolResult> UpdateRecipeAsync(
        UpdateRecipeArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var recipe = await recipeService.UpdateRecipeAsync(
            new UpdateRecipeCommand(
                arguments.RecipeId,
                invocationContext.UserId,
                arguments.ExpectedVersion,
                arguments.Name,
                arguments.Description,
                arguments.TotalPreparedWeightGrams,
                MapIngredients(arguments.Ingredients),
                arguments.ChangeReason,
                "User chat"),
            cancellationToken);
        return await MapRecipeAsync(recipe, invocationContext.UserId, cancellationToken);
    }

    private async Task<DiaryMutationToolResult> AddFoodToDiaryAsync(
        AddFoodToDiaryArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken) =>
        MapDiaryMutation(await mealService.AddFoodToMealAsync(
            new AddFoodToMealCommand(
                invocationContext.UserId,
                RequireIdempotencyKey(invocationContext),
                arguments.FoodProductId,
                arguments.WeightGrams,
                arguments.OccurredAt,
                arguments.MealType!.Value,
                invocationContext.SourceMessageId),
            cancellationToken));

    private async Task<DiaryMutationToolResult> AddRecipeToDiaryAsync(
        AddRecipeToDiaryArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var result = arguments.WeightGrams is not null
            ? await mealService.AddRecipePortionToMealAsync(
                new AddRecipePortionToMealCommand(
                    invocationContext.UserId,
                    RequireIdempotencyKey(invocationContext),
                    arguments.RecipeId,
                    arguments.WeightGrams.Value,
                    arguments.OccurredAt,
                    arguments.MealType!.Value,
                    invocationContext.SourceMessageId),
                cancellationToken)
            : await mealService.AddRecipeFractionToMealAsync(
                new AddRecipeFractionToMealCommand(
                    invocationContext.UserId,
                    RequireIdempotencyKey(invocationContext),
                    arguments.RecipeId,
                    arguments.Fraction!.Value,
                    arguments.OccurredAt,
                    arguments.MealType!.Value,
                    invocationContext.SourceMessageId),
                cancellationToken);
        return MapDiaryMutation(result);
    }

    private async Task<DiaryMutationToolResult> UpdateDiaryItemAsync(
        UpdateDiaryItemArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var result = arguments.WeightGrams is not null
            ? await mealService.UpdateMealItemWeightAsync(
                new UpdateMealItemWeightCommand(
                    invocationContext.UserId,
                    RequireIdempotencyKey(invocationContext),
                    arguments.DiaryItemId,
                    arguments.WeightGrams.Value),
                cancellationToken)
            : await mealService.MoveMealItemAsync(
                new MoveMealItemCommand(
                    invocationContext.UserId,
                    RequireIdempotencyKey(invocationContext),
                    arguments.DiaryItemId,
                    arguments.OccurredAt!.Value,
                    arguments.MealType!.Value),
                cancellationToken);
        return MapDiaryMutation(result);
    }

    private async Task<DiaryMutationToolResult> DeleteDiaryItemAsync(
        DeleteDiaryItemArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken) =>
        MapDiaryMutation(await mealService.DeleteMealItemAsync(
            new DeleteMealItemCommand(
                invocationContext.UserId,
                RequireIdempotencyKey(invocationContext),
                arguments.DiaryItemId),
            cancellationToken));

    private async Task<DailySummaryToolResult> GetDailySummaryAsync(
        GetDailySummaryArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken) =>
        MapDailySummary(await mealService.GetDailySummaryAsync(
            new GetDailySummaryQuery(invocationContext.UserId, arguments.Date), cancellationToken));

    private async Task<RecentMealsToolResult> GetRecentMealsAsync(
        GetRecentMealsArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var user = await context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == invocationContext.UserId,
            cancellationToken) ?? throw new EntityNotFoundException("UserProfile", invocationContext.UserId);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone);
        var endingOn = arguments.EndingOn ?? DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime);
        var fromDate = endingOn.AddDays(-(arguments.Days - 1));
        var meals = new List<MealToolResult>();
        for (var date = fromDate; date <= endingOn; date = date.AddDays(1))
        {
            var dayMeals = await mealService.GetMealsForDateAsync(
                new GetMealsForDateQuery(invocationContext.UserId, date), cancellationToken);
            meals.AddRange(dayMeals.Select(MapMeal));
        }

        return new RecentMealsToolResult(fromDate, endingOn, user.TimeZone, meals);
    }

    private async Task<NutritionTargetsToolResult> GetNutritionTargetsAsync(
        GetNutritionTargetsArguments arguments,
        ToolInvocationContext invocationContext,
        CancellationToken cancellationToken)
    {
        var summary = await mealService.GetDailySummaryAsync(
            new GetDailySummaryQuery(invocationContext.UserId, arguments.Date), cancellationToken);
        return new NutritionTargetsToolResult(
            arguments.Date,
            summary.Target is null ? null : arguments.Date,
            summary.Target is null ? null : MapNutrition(summary.Target),
            summary.Remaining is null ? null : MapNutrition(summary.Remaining));
    }

    private async Task<RecipeToolResult> MapRecipeAsync(
        RecipeResult recipe,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var nutrition = await recipeService.CalculateRecipeNutritionAsync(
            new CalculateRecipeNutritionQuery(recipe.Id, userId, recipe.SelectedVersion.Version),
            cancellationToken);
        var ingredients = new List<RecipeIngredientToolResult>(recipe.SelectedVersion.Ingredients.Count);
        foreach (var ingredient in recipe.SelectedVersion.Ingredients)
        {
            var food = await foodService.GetFoodProductByIdAsync(
                new GetFoodProductByIdQuery(ingredient.FoodProductId, userId), cancellationToken);
            ingredients.Add(new RecipeIngredientToolResult(
                ingredient.FoodProductId, food.Name, ingredient.WeightGrams));
        }

        return new RecipeToolResult(
            recipe.Id,
            recipe.SelectedVersion.Version,
            recipe.SelectedVersion.Name,
            recipe.SelectedVersion.Description,
            recipe.SelectedVersion.TotalPreparedWeightGrams,
            recipe.IsArchived,
            ingredients,
            new ToolNutritionValues(
                nutrition.Calories,
                nutrition.ProteinGrams,
                nutrition.FatGrams,
                nutrition.CarbohydrateGrams),
            recipe.UpdatedAtUtc);
    }

    private static RecipeIngredientInput[] MapIngredients(
        IReadOnlyList<RecipeIngredientArguments> ingredients) =>
        ingredients.Select(item => new RecipeIngredientInput(
            item.FoodProductId, item.WeightGrams)).ToArray();

    private static FoodToolResult MapFood(FoodProductResult food) => new(
        food.Id,
        food.Name,
        food.Brand,
        food.CaloriesPer100g,
        food.ProteinPer100g,
        food.FatPer100g,
        food.CarbohydratesPer100g,
        food.FiberPer100g,
        food.Source,
        food.IsVerified,
        food.UpdatedAtUtc);

    private static DiaryMutationToolResult MapDiaryMutation(MealOperationResult result) => new(
        result.Operation,
        result.IsReplay,
        result.MealItemId,
        result.MealItem is null ? null : MapDiaryItem(result.MealItem),
        MapDailySummary(result.DailySummary));

    private static DailySummaryToolResult MapDailySummary(DailySummaryResult summary) => new(
        summary.Date,
        summary.TimeZone,
        MapNutrition(summary.Consumed),
        summary.Target is null ? null : MapNutrition(summary.Target),
        summary.Remaining is null ? null : MapNutrition(summary.Remaining),
        summary.Meals.Select(MapMeal).ToArray());

    private static MealToolResult MapMeal(MealResult meal) => new(
        meal.Id,
        meal.OccurredAt,
        meal.MealType,
        meal.Items.Select(MapDiaryItem).ToArray());

    private static DiaryItemToolResult MapDiaryItem(MealItemResult item) => new(
        item.Id,
        item.MealId,
        item.FoodProductId,
        item.RecipeId,
        item.RecipeVersion,
        item.WeightGrams,
        MapNutrition(item.NutritionSnapshot));

    private static ToolNutritionValues MapNutrition(NutritionTotalResult nutrition) => new(
        nutrition.Calories,
        nutrition.ProteinGrams,
        nutrition.FatGrams,
        nutrition.CarbohydrateGrams);

    private static ToolNutritionValues MapNutrition(RemainingNutritionResult nutrition) => new(
        nutrition.Calories,
        nutrition.ProteinGrams,
        nutrition.FatGrams,
        nutrition.CarbohydrateGrams);

    private static MessageToolExecutionOutcome Success<TResult>(TResult result) =>
        new(true, ToolJson.Serialize(ToolExecutionResults.Success(result)));

    private static MessageToolExecutionOutcome Failure(
        string code,
        string message,
        string? field = null) =>
        new(
            false,
            ToolJson.Serialize(ToolExecutionResults.Failure<object>(code, message, field)),
            code,
            message);

    private static bool HasValidConfirmation(
        MessageToolExecutionRequest request,
        string argumentsHash)
    {
        var confirmation = request.Context.Confirmation;
        return confirmation is not null &&
            string.Equals(confirmation.ToolName, request.ToolName, StringComparison.Ordinal) &&
            string.Equals(confirmation.CanonicalArgumentsHash, argumentsHash, StringComparison.Ordinal);
    }


    private static string RequireIdempotencyKey(ToolInvocationContext context) =>
        context.IdempotencyKey
        ?? throw new ApplicationValidationException("The idempotency key is required.");

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Tool {ToolName} completed in {DurationMs} ms with success {IsSuccess}")]
    private static partial void LogToolCompleted(
        ILogger logger,
        string toolName,
        long durationMs,
        bool isSuccess);
}
