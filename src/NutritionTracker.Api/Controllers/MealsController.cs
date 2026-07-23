using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Application.Common;
using NutritionTracker.Application.Meals;
using NutritionTracker.Domain.Meals;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = "AuthenticatedUser")]
[Route("api/meals")]
public sealed class MealsController(IMealService mealService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("items/food")]
    [ProducesResponseType<MealOperationResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MealOperationResult>> AddFoodAsync(
        AddFoodToMealRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mealService.AddFoodToMealAsync(
            request.ToCommand(currentUser.UserId), cancellationToken);
        return result.IsReplay
            ? Ok(result)
            : Created($"/api/meals/items/{result.MealItemId}", result);
    }

    [HttpPost("items/recipe")]
    [ProducesResponseType<MealOperationResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MealOperationResult>> AddRecipeAsync(
        AddRecipeToMealRequest request,
        CancellationToken cancellationToken)
    {
        MealOperationResult result;
        if (request.WeightGrams is not null && request.Fraction is null)
        {
            result = await mealService.AddRecipePortionToMealAsync(
                request.ToPortionCommand(currentUser.UserId), cancellationToken);
        }
        else if (request.Fraction is not null && request.WeightGrams is null)
        {
            result = await mealService.AddRecipeFractionToMealAsync(
                request.ToFractionCommand(currentUser.UserId), cancellationToken);
        }
        else
        {
            throw new ApplicationValidationException(
                "Specify exactly one of WeightGrams or Fraction.",
                nameof(request.WeightGrams));
        }

        return result.IsReplay
            ? Ok(result)
            : Created($"/api/meals/items/{result.MealItemId}", result);
    }

    [HttpPut("items/{id:guid}")]
    [ProducesResponseType<MealOperationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MealOperationResult>> UpdateAsync(
        Guid id,
        UpdateMealItemRequest request,
        CancellationToken cancellationToken)
    {
        var isWeightUpdate = request.WeightGrams is not null &&
            request.OccurredAt is null && request.MealType is null;
        var isMove = request.WeightGrams is null &&
            request.OccurredAt is not null && request.MealType is not null;
        if (isWeightUpdate)
        {
            return Ok(await mealService.UpdateMealItemWeightAsync(
                new UpdateMealItemWeightCommand(
                    currentUser.UserId,
                    request.IdempotencyKey,
                    id,
                    request.WeightGrams!.Value),
                cancellationToken));
        }

        if (isMove)
        {
            return Ok(await mealService.MoveMealItemAsync(
                new MoveMealItemCommand(
                    currentUser.UserId,
                    request.IdempotencyKey,
                    id,
                    request.OccurredAt!.Value,
                    request.MealType!.Value),
                cancellationToken));
        }

        throw new ApplicationValidationException(
            "Specify either WeightGrams, or both OccurredAt and MealType.");
    }

    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType<MealOperationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MealOperationResult>> DeleteAsync(
        Guid id,
        [FromQuery] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return Ok(await mealService.DeleteMealItemAsync(
            new DeleteMealItemCommand(currentUser.UserId, idempotencyKey, id),
            cancellationToken));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MealResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MealResult>>> GetMealsForDateAsync(
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        return Ok(await mealService.GetMealsForDateAsync(
            new GetMealsForDateQuery(currentUser.UserId, date), cancellationToken));
    }

    [HttpGet("/api/daily-summary")]
    [ProducesResponseType<DailySummaryResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailySummaryResult>> GetDailySummaryAsync(
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        return Ok(await mealService.GetDailySummaryAsync(
            new GetDailySummaryQuery(currentUser.UserId, date), cancellationToken));
    }
}

public sealed record AddFoodToMealRequest(
    string IdempotencyKey,
    Guid FoodProductId,
    decimal WeightGrams,
    DateTimeOffset OccurredAt,
    MealType MealType,
    Guid? SourceMessageId)
{
    public AddFoodToMealCommand ToCommand(Guid userId)
    {
        return new AddFoodToMealCommand(
            userId,
            IdempotencyKey,
            FoodProductId,
            WeightGrams,
            OccurredAt,
            MealType,
            SourceMessageId);
    }
}

public sealed record AddRecipeToMealRequest(
    string IdempotencyKey,
    Guid RecipeId,
    decimal? WeightGrams,
    decimal? Fraction,
    DateTimeOffset OccurredAt,
    MealType MealType,
    Guid? SourceMessageId)
{
    public AddRecipePortionToMealCommand ToPortionCommand(Guid userId)
    {
        return new AddRecipePortionToMealCommand(
            userId,
            IdempotencyKey,
            RecipeId,
            WeightGrams!.Value,
            OccurredAt,
            MealType,
            SourceMessageId);
    }

    public AddRecipeFractionToMealCommand ToFractionCommand(Guid userId)
    {
        return new AddRecipeFractionToMealCommand(
            userId,
            IdempotencyKey,
            RecipeId,
            Fraction!.Value,
            OccurredAt,
            MealType,
            SourceMessageId);
    }
}

public sealed record UpdateMealItemRequest(
    string IdempotencyKey,
    decimal? WeightGrams,
    DateTimeOffset? OccurredAt,
    MealType? MealType);
