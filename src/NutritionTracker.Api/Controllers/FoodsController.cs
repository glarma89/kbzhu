using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Application.Common;
using NutritionTracker.Application.Foods;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = "AuthenticatedUser")]
[Route("api/foods")]
public sealed class FoodsController(
    IFoodProductService foodProductService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FoodProductResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FoodProductResult>>> SearchAsync(
        [FromQuery] string? query,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var results = await foodProductService.SearchFoodProductsAsync(
            new SearchFoodProductsQuery(currentUser.UserId, query, limit),
            cancellationToken);
        return Ok(results);
    }

    [HttpGet("candidates")]
    [ProducesResponseType<IReadOnlyList<FoodProductResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FoodProductResult>>> FindCandidatesAsync(
        [FromQuery] string name,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var results = await foodProductService.FindCandidatesByNameAsync(
            new FindCandidatesByNameQuery(currentUser.UserId, name, limit),
            cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<FoodProductResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FoodProductResult>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await foodProductService.GetFoodProductByIdAsync(
            new GetFoodProductByIdQuery(id, currentUser.UserId),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<FoodProductResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FoodProductResult>> CreateAsync(
        CreateFoodProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await foodProductService.CreateFoodProductAsync(
            request.ToCommand(currentUser.UserId),
            cancellationToken);
        return Created($"/api/foods/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<FoodProductResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FoodProductResult>> UpdateAsync(
        Guid id,
        UpdateFoodProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await foodProductService.UpdateFoodProductAsync(
            request.ToCommand(id, currentUser.UserId),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record CreateFoodProductRequest(
    string Name,
    string? Brand,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal FatPer100g,
    decimal CarbohydratesPer100g,
    decimal? FiberPer100g,
    string Source,
    bool IsVerified)
{
    public CreateFoodProductCommand ToCommand(Guid userId)
    {
        return new CreateFoodProductCommand(
            userId,
            Name,
            Brand,
            CaloriesPer100g,
            ProteinPer100g,
            FatPer100g,
            CarbohydratesPer100g,
            FiberPer100g,
            Source,
            IsVerified);
    }
}

public sealed record UpdateFoodProductRequest(
    string Name,
    string? Brand,
    decimal CaloriesPer100g,
    decimal ProteinPer100g,
    decimal FatPer100g,
    decimal CarbohydratesPer100g,
    decimal? FiberPer100g,
    string Source,
    bool IsVerified)
{
    public UpdateFoodProductCommand ToCommand(Guid id, Guid userId)
    {
        return new UpdateFoodProductCommand(
            id,
            userId,
            Name,
            Brand,
            CaloriesPer100g,
            ProteinPer100g,
            FatPer100g,
            CarbohydratesPer100g,
            FiberPer100g,
            Source,
            IsVerified);
    }
}
