using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NutritionTracker.Application.Foods;
using NutritionTracker.Infrastructure.Foods;
using NutritionTracker.Infrastructure.Persistence;

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
        services.AddScoped<IFoodProductRepository, FoodProductRepository>();
        services.AddScoped<IFoodProductService, FoodProductService>();
        return services;
    }
}
