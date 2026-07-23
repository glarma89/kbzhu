namespace NutritionTracker.Application.Foods;

public interface IFoodProductService
{
    Task<FoodProductResult> CreateFoodProductAsync(
        CreateFoodProductCommand command,
        CancellationToken cancellationToken);

    Task<FoodProductResult> UpdateFoodProductAsync(
        UpdateFoodProductCommand command,
        CancellationToken cancellationToken);

    Task<FoodProductResult> GetFoodProductByIdAsync(
        GetFoodProductByIdQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FoodProductResult>> SearchFoodProductsAsync(
        SearchFoodProductsQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FoodProductResult>> FindCandidatesByNameAsync(
        FindCandidatesByNameQuery query,
        CancellationToken cancellationToken);
}
