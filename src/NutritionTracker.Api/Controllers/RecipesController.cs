using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Application.Common;
using NutritionTracker.Application.Recipes;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = "AuthenticatedUser")]
[Route("api/recipes")]
public sealed class RecipesController(IRecipeService recipeService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecipeSummaryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecipeSummaryResult>>> SearchAsync(
        [FromQuery] string? query,
        [FromQuery] bool includeArchived = false,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var results = await recipeService.SearchRecipesAsync(
            new SearchRecipesQuery(currentUser.UserId, query, includeArchived, limit),
            cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RecipeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeResult>> GetAsync(
        Guid id,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.GetRecipeAsync(
            new GetRecipeQuery(id, currentUser.UserId, version),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<RecipeResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecipeResult>> CreateAsync(
        CreateRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.CreateRecipeAsync(
            request.ToCommand(currentUser.UserId), cancellationToken);
        return Created($"/api/recipes/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<RecipeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecipeResult>> UpdateAsync(
        Guid id,
        UpdateRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.UpdateRecipeAsync(
            request.ToCommand(id, currentUser.UserId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType<RecipeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecipeResult>> ArchiveAsync(
        Guid id,
        ArchiveRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.ArchiveRecipeAsync(
            new ArchiveRecipeCommand(id, currentUser.UserId, request.Reason, request.Source),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/nutrition")]
    [ProducesResponseType<RecipeNutritionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecipeNutritionResult>> CalculateNutritionAsync(
        Guid id,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.CalculateRecipeNutritionAsync(
            new CalculateRecipeNutritionQuery(id, currentUser.UserId, version),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/nutrition/portion")]
    [ProducesResponseType<RecipeNutritionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecipeNutritionResult>> CalculatePortionAsync(
        Guid id,
        [FromQuery] decimal weightGrams,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.CalculateRecipePortionAsync(
            new CalculateRecipePortionQuery(id, currentUser.UserId, weightGrams, version),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record RecipeIngredientRequest(Guid FoodProductId, decimal WeightGrams)
{
    public RecipeIngredientInput ToInput() => new(FoodProductId, WeightGrams);
}

public sealed record CreateRecipeRequest(
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    IReadOnlyList<RecipeIngredientRequest> Ingredients,
    string? ChangeReason,
    string ChangeSource)
{
    public CreateRecipeCommand ToCommand(Guid userId)
    {
        return new CreateRecipeCommand(
            userId,
            Name,
            Description,
            TotalPreparedWeightGrams,
            Ingredients?.Select(item => item.ToInput()).ToArray() ?? [],
            ChangeReason,
            ChangeSource);
    }
}

public sealed record UpdateRecipeRequest(
    int ExpectedVersion,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    IReadOnlyList<RecipeIngredientRequest> Ingredients,
    string? ChangeReason,
    string ChangeSource)
{
    public UpdateRecipeCommand ToCommand(Guid id, Guid userId)
    {
        return new UpdateRecipeCommand(
            id,
            userId,
            ExpectedVersion,
            Name,
            Description,
            TotalPreparedWeightGrams,
            Ingredients?.Select(item => item.ToInput()).ToArray() ?? [],
            ChangeReason,
            ChangeSource);
    }
}

public sealed record ArchiveRecipeRequest(string? Reason, string Source);
