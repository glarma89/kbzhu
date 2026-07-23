using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Application.Recipes;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/recipes")]
public sealed class RecipesController(IRecipeService recipeService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecipeSummaryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecipeSummaryResult>>> SearchAsync(
        [FromQuery] Guid userId,
        [FromQuery] string? query,
        [FromQuery] bool includeArchived = false,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var results = await recipeService.SearchRecipesAsync(
            new SearchRecipesQuery(userId, query, includeArchived, limit),
            cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RecipeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeResult>> GetAsync(
        Guid id,
        [FromQuery] Guid userId,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.GetRecipeAsync(
            new GetRecipeQuery(id, userId, version),
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
        var result = await recipeService.CreateRecipeAsync(request.ToCommand(), cancellationToken);
        return Created($"/api/recipes/{result.Id}?userId={result.UserId}", result);
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
        var result = await recipeService.UpdateRecipeAsync(request.ToCommand(id), cancellationToken);
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
            new ArchiveRecipeCommand(id, request.UserId, request.Reason, request.Source),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/nutrition")]
    [ProducesResponseType<RecipeNutritionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecipeNutritionResult>> CalculateNutritionAsync(
        Guid id,
        [FromQuery] Guid userId,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.CalculateRecipeNutritionAsync(
            new CalculateRecipeNutritionQuery(id, userId, version),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/nutrition/portion")]
    [ProducesResponseType<RecipeNutritionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecipeNutritionResult>> CalculatePortionAsync(
        Guid id,
        [FromQuery] Guid userId,
        [FromQuery] decimal weightGrams,
        [FromQuery] int? version,
        CancellationToken cancellationToken)
    {
        var result = await recipeService.CalculateRecipePortionAsync(
            new CalculateRecipePortionQuery(id, userId, weightGrams, version),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record RecipeIngredientRequest(Guid FoodProductId, decimal WeightGrams)
{
    public RecipeIngredientInput ToInput() => new(FoodProductId, WeightGrams);
}

public sealed record CreateRecipeRequest(
    Guid UserId,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    IReadOnlyList<RecipeIngredientRequest> Ingredients,
    string? ChangeReason,
    string ChangeSource)
{
    public CreateRecipeCommand ToCommand()
    {
        return new CreateRecipeCommand(
            UserId,
            Name,
            Description,
            TotalPreparedWeightGrams,
            Ingredients?.Select(item => item.ToInput()).ToArray() ?? [],
            ChangeReason,
            ChangeSource);
    }
}

public sealed record UpdateRecipeRequest(
    Guid UserId,
    int ExpectedVersion,
    string Name,
    string? Description,
    decimal? TotalPreparedWeightGrams,
    IReadOnlyList<RecipeIngredientRequest> Ingredients,
    string? ChangeReason,
    string ChangeSource)
{
    public UpdateRecipeCommand ToCommand(Guid id)
    {
        return new UpdateRecipeCommand(
            id,
            UserId,
            ExpectedVersion,
            Name,
            Description,
            TotalPreparedWeightGrams,
            Ingredients?.Select(item => item.ToInput()).ToArray() ?? [],
            ChangeReason,
            ChangeSource);
    }
}

public sealed record ArchiveRecipeRequest(Guid UserId, string? Reason, string Source);
