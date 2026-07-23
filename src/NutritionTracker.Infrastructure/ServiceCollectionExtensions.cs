using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NutritionTracker.Application.Chat;
using NutritionTracker.Application.Foods;
using NutritionTracker.Application.Meals;
using NutritionTracker.Application.Recipes;
using NutritionTracker.Infrastructure.Chat;
using NutritionTracker.Infrastructure.Foods;
using NutritionTracker.Infrastructure.Meals;
using NutritionTracker.Infrastructure.Persistence;
using NutritionTracker.Infrastructure.Recipes;
using NutritionTracker.Infrastructure.LanguageModels;
using NutritionTracker.Infrastructure.Tools;

namespace NutritionTracker.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<NutritionDbContext>(options => options.UseSqlite(connectionString));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IUserMessageProcessingRepository, UserMessageProcessingRepository>();
        services.AddScoped<IMessageToolExecutor, AllowlistedToolExecutor>();
        services.AddScoped<IChatMessageService, ChatMessageService>();
        services.AddScoped<IFoodProductRepository, FoodProductRepository>();
        services.AddScoped<IFoodProductService, FoodProductService>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<IMealService, MealService>();
        return services;
    }

    public static IServiceCollection AddOpenAiLanguageModel(
        this IServiceCollection services,
        OpenAiLanguageModelOptions options,
        ChatAgentSettings agentSettings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(agentSettings);
        services.AddSingleton(options);
        services.AddSingleton(agentSettings);
        services.AddSingleton<ILanguageModelClient, OpenAiLanguageModelClient>();
        return services;
    }
}
