using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Application.Foods;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/foods")]
public sealed class FoodsController(IFoodProductService foodProductService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FoodProductResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FoodProductResult>>> SearchAsync(
        [FromQuery] string? query,
        [FromQuery] Guid? userId,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var results = await foodProductService.SearchFoodProductsAsync(
            new SearchFoodProductsQuery(userId, query, limit),
            cancellationToken);
        return Ok(results);
    }

    [HttpGet("candidates")]
    [ProducesResponseType<IReadOnlyList<FoodProductResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FoodProductResult>>> FindCandidatesAsync(
        [FromQuery] string name,
        [FromQuery] Guid? userId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var results = await foodProductService.FindCandidatesByNameAsync(
            new FindCandidatesByNameQuery(userId, name, limit),
            cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<FoodProductResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FoodProductResult>> GetByIdAsync(
        Guid id,
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var result = await foodProductService.GetFoodProductByIdAsync(
            new GetFoodProductByIdQuery(id, userId),
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
            request.ToCommand(),
            cancellationToken);
        var location = result.UserId is null
            ? $"/api/foods/{result.Id}"
            : $"/api/foods/{result.Id}?userId={result.UserId}";
        return Created(location, result);
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
            request.ToCommand(id),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record CreateFoodProductRequest(
    Guid? UserId,
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
    public CreateFoodProductCommand ToCommand()
    {
        return new CreateFoodProductCommand(
            UserId,
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
    Guid? UserId,
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
    public UpdateFoodProductCommand ToCommand(Guid id)
    {
        return new UpdateFoodProductCommand(
            id,
            UserId,
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
