namespace NutritionTracker.Application.Tools;

public interface IToolHandler<TArguments, TResult> where TArguments : IToolArguments
{
    string ToolName { get; }

    Task<ToolExecutionResult<TResult>> HandleAsync(
        TArguments arguments,
        ToolInvocationContext context,
        CancellationToken cancellationToken);
}

public interface ISearchFoodsToolHandler : IToolHandler<SearchFoodsArguments, SearchFoodsToolResult>
{
}

public interface IGetFoodToolHandler : IToolHandler<GetFoodArguments, FoodToolResult>
{
}

public interface ICreateFoodToolHandler : IToolHandler<CreateFoodArguments, FoodToolResult>
{
}

public interface IUpdateFoodToolHandler : IToolHandler<UpdateFoodArguments, FoodToolResult>
{
}

public interface ISearchRecipesToolHandler : IToolHandler<SearchRecipesArguments, SearchRecipesToolResult>
{
}

public interface IGetRecipeToolHandler : IToolHandler<GetRecipeArguments, RecipeToolResult>
{
}

public interface ICreateRecipeToolHandler : IToolHandler<CreateRecipeArguments, RecipeToolResult>
{
}

public interface IUpdateRecipeToolHandler : IToolHandler<UpdateRecipeArguments, RecipeToolResult>
{
}

public interface IAddFoodToDiaryToolHandler
    : IToolHandler<AddFoodToDiaryArguments, DiaryMutationToolResult>
{
}

public interface IAddRecipeToDiaryToolHandler
    : IToolHandler<AddRecipeToDiaryArguments, DiaryMutationToolResult>
{
}

public interface IUpdateDiaryItemToolHandler
    : IToolHandler<UpdateDiaryItemArguments, DiaryMutationToolResult>
{
}

public interface IDeleteDiaryItemToolHandler
    : IToolHandler<DeleteDiaryItemArguments, DiaryMutationToolResult>
{
}

public interface IGetDailySummaryToolHandler
    : IToolHandler<GetDailySummaryArguments, DailySummaryToolResult>
{
}

public interface IGetRecentMealsToolHandler
    : IToolHandler<GetRecentMealsArguments, RecentMealsToolResult>
{
}

public interface IGetNutritionTargetsToolHandler
    : IToolHandler<GetNutritionTargetsArguments, NutritionTargetsToolResult>
{
}
