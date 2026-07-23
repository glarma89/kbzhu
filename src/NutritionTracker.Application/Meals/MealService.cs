using NutritionTracker.Application.Common;
using NutritionTracker.Domain.Commands;
using NutritionTracker.Domain.Meals;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Users;

namespace NutritionTracker.Application.Meals;

public sealed class MealService(IMealRepository repository, TimeProvider timeProvider) : IMealService
{
    private const string AddFoodOperation = "AddFoodToMeal";
    private const string AddRecipePortionOperation = "AddRecipePortionToMeal";
    private const string AddRecipeFractionOperation = "AddRecipeFractionToMeal";
    private const string UpdateWeightOperation = "UpdateMealItemWeight";
    private const string DeleteItemOperation = "DeleteMealItem";
    private const string MoveItemOperation = "MoveMealItem";
    private const int MaximumIdempotencyKeyLength = 200;
    private const decimal MaximumWeightGrams = 1_000_000m;
    private const decimal MaximumFraction = 100m;

    public async Task<MealOperationResult> AddFoodToMealAsync(
        AddFoodToMealCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommonMutation(command.UserId, command.IdempotencyKey);
        ValidateId(command.FoodProductId, nameof(command.FoodProductId));
        ValidateWeight(command.WeightGrams);
        ValidateMealType(command.MealType);
        ValidateOptionalId(command.SourceMessageId, nameof(command.SourceMessageId));
        var replay = await TryReplayAsync(
            command.UserId, command.IdempotencyKey, AddFoodOperation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var user = await GetUserAsync(command.UserId, cancellationToken);
        var product = await repository.GetVisibleFoodProductAsync(
            command.FoodProductId, command.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("FoodProduct", command.FoodProductId);
        var occurredAtUtc = command.OccurredAt.ToUniversalTime();
        var date = GetUserDate(occurredAtUtc, GetTimeZone(user));
        var meal = await GetOrCreateMealAsync(
            user, occurredAtUtc, command.MealType, cancellationToken);
        var snapshot = NutritionCalculator.RoundForBoundary(
            NutritionCalculator.CalculateProduct(product, command.WeightGrams));
        var item = new MealItem(
            Guid.NewGuid(), meal.Id, product, null, command.WeightGrams, snapshot, command.SourceMessageId);
        await repository.AddMealItemAsync(item, cancellationToken);
        await AddProcessedCommandAsync(
            command.UserId, command.IdempotencyKey, AddFoodOperation, item.Id, date, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await BuildOperationResultAsync(AddFoodOperation, false, item.Id, date, command.UserId, cancellationToken);
    }

    public async Task<MealOperationResult> AddRecipePortionToMealAsync(
        AddRecipePortionToMealCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommonMutation(command.UserId, command.IdempotencyKey);
        ValidateId(command.RecipeId, nameof(command.RecipeId));
        ValidateWeight(command.WeightGrams);
        ValidateMealType(command.MealType);
        ValidateOptionalId(command.SourceMessageId, nameof(command.SourceMessageId));
        var replay = await TryReplayAsync(
            command.UserId, command.IdempotencyKey, AddRecipePortionOperation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var user = await GetUserAsync(command.UserId, cancellationToken);
        var recipe = await repository.GetOwnedRecipeAsync(command.RecipeId, command.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("Recipe", command.RecipeId);
        if (recipe.IsArchived)
        {
            throw new ApplicationConflictException("An archived recipe cannot be added to a meal.");
        }

        var occurredAtUtc = command.OccurredAt.ToUniversalTime();
        var date = GetUserDate(occurredAtUtc, GetTimeZone(user));
        var meal = await GetOrCreateMealAsync(user, occurredAtUtc, command.MealType, cancellationToken);
        var snapshot = NutritionCalculator.RoundForBoundary(
            NutritionCalculator.CalculateRecipePortion(recipe.CurrentVersion, command.WeightGrams));
        var item = new MealItem(
            Guid.NewGuid(), meal.Id, null, recipe, command.WeightGrams, snapshot, command.SourceMessageId);
        await repository.AddMealItemAsync(item, cancellationToken);
        await AddProcessedCommandAsync(
            command.UserId,
            command.IdempotencyKey,
            AddRecipePortionOperation,
            item.Id,
            date,
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await BuildOperationResultAsync(
            AddRecipePortionOperation, false, item.Id, date, command.UserId, cancellationToken);
    }

    public async Task<MealOperationResult> AddRecipeFractionToMealAsync(
        AddRecipeFractionToMealCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommonMutation(command.UserId, command.IdempotencyKey);
        ValidateId(command.RecipeId, nameof(command.RecipeId));
        if (command.Fraction is <= 0 or > MaximumFraction)
        {
            throw new ApplicationValidationException(
                $"The fraction must be greater than zero and no more than {MaximumFraction}.",
                nameof(command.Fraction));
        }

        ValidateMealType(command.MealType);
        ValidateOptionalId(command.SourceMessageId, nameof(command.SourceMessageId));
        var replay = await TryReplayAsync(
            command.UserId, command.IdempotencyKey, AddRecipeFractionOperation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var user = await GetUserAsync(command.UserId, cancellationToken);
        var recipe = await repository.GetOwnedRecipeAsync(command.RecipeId, command.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("Recipe", command.RecipeId);
        if (recipe.IsArchived)
        {
            throw new ApplicationConflictException("An archived recipe cannot be added to a meal.");
        }

        var preparedWeight = recipe.CurrentVersion.TotalPreparedWeightGrams
            ?? throw new ApplicationValidationException(
                "A prepared recipe weight is required to record a recipe fraction.",
                nameof(command.RecipeId));
        var consumedWeight = preparedWeight * command.Fraction;
        ValidateWeight(consumedWeight);
        var occurredAtUtc = command.OccurredAt.ToUniversalTime();
        var date = GetUserDate(occurredAtUtc, GetTimeZone(user));
        var meal = await GetOrCreateMealAsync(user, occurredAtUtc, command.MealType, cancellationToken);
        var snapshot = NutritionCalculator.RoundForBoundary(
            NutritionCalculator.CalculateRecipeFraction(recipe.CurrentVersion, command.Fraction));
        var item = new MealItem(
            Guid.NewGuid(), meal.Id, null, recipe, consumedWeight, snapshot, command.SourceMessageId);
        await repository.AddMealItemAsync(item, cancellationToken);
        await AddProcessedCommandAsync(
            command.UserId,
            command.IdempotencyKey,
            AddRecipeFractionOperation,
            item.Id,
            date,
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await BuildOperationResultAsync(
            AddRecipeFractionOperation, false, item.Id, date, command.UserId, cancellationToken);
    }

    public async Task<MealOperationResult> UpdateMealItemWeightAsync(
        UpdateMealItemWeightCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommonMutation(command.UserId, command.IdempotencyKey);
        ValidateId(command.MealItemId, nameof(command.MealItemId));
        ValidateWeight(command.WeightGrams);
        var replay = await TryReplayAsync(
            command.UserId, command.IdempotencyKey, UpdateWeightOperation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var user = await GetUserAsync(command.UserId, cancellationToken);
        var item = await GetOwnedItemAsync(command.MealItemId, command.UserId, true, cancellationToken);
        var meal = await GetOwnedMealAsync(item.MealId, command.UserId, cancellationToken);
        var date = GetUserDate(meal.OccurredAt, GetTimeZone(user));
        var snapshot = NutritionCalculator.RoundForBoundary(
            NutritionCalculator.RecalculateSnapshotWeight(
                item.NutritionSnapshot, item.WeightGrams, command.WeightGrams));
        item.UpdateWeight(command.WeightGrams, snapshot);
        await AddProcessedCommandAsync(
            command.UserId, command.IdempotencyKey, UpdateWeightOperation, item.Id, date, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await BuildOperationResultAsync(
            UpdateWeightOperation, false, item.Id, date, command.UserId, cancellationToken);
    }

    public async Task<MealOperationResult> DeleteMealItemAsync(
        DeleteMealItemCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommonMutation(command.UserId, command.IdempotencyKey);
        ValidateId(command.MealItemId, nameof(command.MealItemId));
        var replay = await TryReplayAsync(
            command.UserId, command.IdempotencyKey, DeleteItemOperation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var user = await GetUserAsync(command.UserId, cancellationToken);
        var item = await GetOwnedItemAsync(command.MealItemId, command.UserId, true, cancellationToken);
        var meal = await GetOwnedMealAsync(item.MealId, command.UserId, cancellationToken);
        var date = GetUserDate(meal.OccurredAt, GetTimeZone(user));
        repository.RemoveMealItem(item);
        await AddProcessedCommandAsync(
            command.UserId, command.IdempotencyKey, DeleteItemOperation, item.Id, date, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await BuildOperationResultAsync(
            DeleteItemOperation, false, item.Id, date, command.UserId, cancellationToken);
    }

    public async Task<MealOperationResult> MoveMealItemAsync(
        MoveMealItemCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommonMutation(command.UserId, command.IdempotencyKey);
        ValidateId(command.MealItemId, nameof(command.MealItemId));
        ValidateMealType(command.MealType);
        var replay = await TryReplayAsync(
            command.UserId, command.IdempotencyKey, MoveItemOperation, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var user = await GetUserAsync(command.UserId, cancellationToken);
        var item = await GetOwnedItemAsync(command.MealItemId, command.UserId, true, cancellationToken);
        var occurredAtUtc = command.OccurredAt.ToUniversalTime();
        var date = GetUserDate(occurredAtUtc, GetTimeZone(user));
        var targetMeal = await GetOrCreateMealAsync(
            user, occurredAtUtc, command.MealType, cancellationToken);
        item.MoveTo(targetMeal.Id);
        await AddProcessedCommandAsync(
            command.UserId, command.IdempotencyKey, MoveItemOperation, item.Id, date, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await BuildOperationResultAsync(
            MoveItemOperation, false, item.Id, date, command.UserId, cancellationToken);
    }

    public async Task<DailySummaryResult> GetDailySummaryAsync(
        GetDailySummaryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateUserId(query.UserId);
        var user = await GetUserAsync(query.UserId, cancellationToken);
        var timeZone = GetTimeZone(user);
        var (startUtc, endUtc) = GetUtcRange(query.Date, timeZone);
        var entries = await repository.GetMealsAsync(query.UserId, startUtc, endUtc, cancellationToken);
        var meals = entries
            .Where(entry => entry.Items.Count > 0)
            .OrderBy(entry => entry.Meal.OccurredAt)
            .Select(entry => MapMeal(entry, timeZone))
            .ToArray();
        var consumed = Sum(entries.SelectMany(entry => entry.Items));
        var target = await repository.GetTargetAsync(query.UserId, query.Date, cancellationToken);
        var targetResult = target is null ? null : MapNutrition(target.NutritionValues);
        var remaining = target is null ? null : Subtract(target.NutritionValues, consumed);

        return new DailySummaryResult(
            query.Date,
            user.TimeZone,
            MapNutrition(consumed),
            targetResult,
            remaining,
            meals);
    }

    public async Task<IReadOnlyList<MealResult>> GetMealsForDateAsync(
        GetMealsForDateQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return (await GetDailySummaryAsync(
            new GetDailySummaryQuery(query.UserId, query.Date), cancellationToken)).Meals;
    }

    public async Task<RemainingNutritionResult?> GetRemainingDailyTargetsAsync(
        GetRemainingDailyTargetsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return (await GetDailySummaryAsync(
            new GetDailySummaryQuery(query.UserId, query.Date), cancellationToken)).Remaining;
    }

    private async Task<MealOperationResult?> TryReplayAsync(
        Guid userId,
        string idempotencyKey,
        string operation,
        CancellationToken cancellationToken)
    {
        var processed = await repository.GetProcessedCommandAsync(
            userId, idempotencyKey.Trim(), cancellationToken);
        if (processed is null)
        {
            return null;
        }

        if (!string.Equals(processed.CommandType, operation, StringComparison.Ordinal))
        {
            throw new ApplicationConflictException(
                "The idempotency key has already been used for a different operation.");
        }

        if (processed.ResultEntityId == Guid.Empty || processed.ResultDate == default)
        {
            throw new ApplicationConflictException(
                "The idempotency key belongs to a legacy command without a replayable result.");
        }

        return await BuildOperationResultAsync(
            operation,
            true,
            processed.ResultEntityId,
            processed.ResultDate,
            userId,
            cancellationToken);
    }

    private async Task<MealOperationResult> BuildOperationResultAsync(
        string operation,
        bool isReplay,
        Guid itemId,
        DateOnly date,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetOwnedMealItemAsync(itemId, userId, false, cancellationToken);
        var summary = await GetDailySummaryAsync(
            new GetDailySummaryQuery(userId, date), cancellationToken);
        return new MealOperationResult(
            operation,
            isReplay,
            itemId,
            item is null ? null : MapItem(item),
            summary);
    }

    private async Task<UserProfile> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await repository.GetUserAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(UserProfile), userId);
    }

    private async Task<MealItem> GetOwnedItemAsync(
        Guid itemId,
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        return await repository.GetOwnedMealItemAsync(itemId, userId, trackChanges, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(MealItem), itemId);
    }

    private async Task<Meal> GetOwnedMealAsync(
        Guid mealId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await repository.GetOwnedMealAsync(mealId, userId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Meal), mealId);
    }

    private async Task<Meal> GetOrCreateMealAsync(
        UserProfile user,
        DateTimeOffset occurredAtUtc,
        MealType mealType,
        CancellationToken cancellationToken)
    {
        var meal = await repository.GetMealAsync(user.Id, occurredAtUtc, mealType, cancellationToken);
        if (meal is not null)
        {
            return meal;
        }

        meal = new Meal(Guid.NewGuid(), user.Id, occurredAtUtc, mealType, null, timeProvider.GetUtcNow());
        await repository.AddMealAsync(meal, cancellationToken);
        return meal;
    }

    private Task AddProcessedCommandAsync(
        Guid userId,
        string idempotencyKey,
        string operation,
        Guid resultEntityId,
        DateOnly resultDate,
        CancellationToken cancellationToken)
    {
        return repository.AddProcessedCommandAsync(
            new ProcessedCommand(
                Guid.NewGuid(),
                userId,
                idempotencyKey.Trim(),
                operation,
                resultEntityId,
                resultDate,
                timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static MealResult MapMeal(MealJournalEntry entry, TimeZoneInfo timeZone)
    {
        return new MealResult(
            entry.Meal.Id,
            TimeZoneInfo.ConvertTime(entry.Meal.OccurredAt, timeZone),
            entry.Meal.MealType,
            entry.Meal.Notes,
            entry.Items.Select(MapItem).ToArray());
    }

    private static MealItemResult MapItem(MealItem item)
    {
        return new MealItemResult(
            item.Id,
            item.MealId,
            item.FoodProductId,
            item.RecipeId,
            item.RecipeVersion,
            item.WeightGrams,
            MapNutrition(item.NutritionSnapshot),
            item.SourceMessageId);
    }

    private static NutritionValues Sum(IEnumerable<MealItem> items)
    {
        var calories = 0m;
        var protein = 0m;
        var fat = 0m;
        var carbohydrates = 0m;
        foreach (var item in items)
        {
            calories += item.NutritionSnapshot.Calories;
            protein += item.NutritionSnapshot.ProteinGrams;
            fat += item.NutritionSnapshot.FatGrams;
            carbohydrates += item.NutritionSnapshot.CarbohydrateGrams;
        }

        return NutritionCalculator.RoundForBoundary(
            new NutritionValues(calories, protein, fat, carbohydrates));
    }

    private static NutritionTotalResult MapNutrition(NutritionValues values)
    {
        return new NutritionTotalResult(
            values.Calories,
            values.ProteinGrams,
            values.FatGrams,
            values.CarbohydrateGrams);
    }

    private static RemainingNutritionResult Subtract(NutritionValues target, NutritionValues consumed)
    {
        return new RemainingNutritionResult(
            Round(target.Calories - consumed.Calories),
            Round(target.ProteinGrams - consumed.ProteinGrams),
            Round(target.FatGrams - consumed.FatGrams),
            Round(target.CarbohydrateGrams - consumed.CarbohydrateGrams));
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(
            value,
            NutritionCalculator.BoundaryDecimalPlaces,
            MidpointRounding.AwayFromZero);
    }

    private static TimeZoneInfo GetTimeZone(UserProfile user)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ApplicationValidationException(
                $"The user's time zone '{user.TimeZone}' is not available.",
                nameof(user.TimeZone));
        }
        catch (InvalidTimeZoneException)
        {
            throw new ApplicationValidationException(
                $"The user's time zone '{user.TimeZone}' is invalid.",
                nameof(user.TimeZone));
        }
    }

    private static DateOnly GetUserDate(DateTimeOffset occurredAt, TimeZoneInfo timeZone)
    {
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(occurredAt, timeZone).DateTime);
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetUtcRange(
        DateOnly date,
        TimeZoneInfo timeZone)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
        return (new DateTimeOffset(startUtc), new DateTimeOffset(endUtc));
    }

    private static void ValidateCommonMutation(Guid userId, string idempotencyKey)
    {
        ValidateUserId(userId);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ApplicationValidationException(
                "The idempotency key is required.", nameof(idempotencyKey));
        }

        if (idempotencyKey.Trim().Length > MaximumIdempotencyKeyLength)
        {
            throw new ApplicationValidationException(
                $"The idempotency key cannot exceed {MaximumIdempotencyKeyLength} characters.",
                nameof(idempotencyKey));
        }
    }

    private static void ValidateUserId(Guid userId)
    {
        ValidateId(userId, nameof(userId));
    }

    private static void ValidateId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ApplicationValidationException("The identifier cannot be empty.", parameterName);
        }
    }

    private static void ValidateOptionalId(Guid? id, string parameterName)
    {
        if (id == Guid.Empty)
        {
            throw new ApplicationValidationException(
                "The identifier cannot be empty when specified.", parameterName);
        }
    }

    private static void ValidateWeight(decimal weightGrams)
    {
        if (weightGrams is <= 0 or > MaximumWeightGrams)
        {
            throw new ApplicationValidationException(
                $"The weight must be greater than zero and no more than {MaximumWeightGrams} grams.",
                nameof(weightGrams));
        }
    }

    private static void ValidateMealType(MealType mealType)
    {
        if (!Enum.IsDefined(mealType))
        {
            throw new ApplicationValidationException("The meal type is invalid.", nameof(mealType));
        }
    }
}
