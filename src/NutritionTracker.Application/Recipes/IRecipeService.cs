namespace NutritionTracker.Application.Recipes;

public interface IRecipeService
{
    Task<RecipeResult> CreateRecipeAsync(CreateRecipeCommand command, CancellationToken cancellationToken);

    Task<RecipeResult> GetRecipeAsync(GetRecipeQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<RecipeSummaryResult>> SearchRecipesAsync(
        SearchRecipesQuery query,
        CancellationToken cancellationToken);

    Task<RecipeResult> UpdateRecipeAsync(UpdateRecipeCommand command, CancellationToken cancellationToken);

    Task<RecipeResult> ArchiveRecipeAsync(ArchiveRecipeCommand command, CancellationToken cancellationToken);

    Task<RecipeNutritionResult> CalculateRecipeNutritionAsync(
        CalculateRecipeNutritionQuery query,
        CancellationToken cancellationToken);

    Task<RecipeNutritionResult> CalculateRecipePortionAsync(
        CalculateRecipePortionQuery query,
        CancellationToken cancellationToken);
}
