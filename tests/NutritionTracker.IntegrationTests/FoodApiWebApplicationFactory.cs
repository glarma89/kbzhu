using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NutritionTracker.Infrastructure.Persistence;
using NutritionTracker.IntegrationTests.Fakes;

namespace NutritionTracker.IntegrationTests;

internal sealed class FoodApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"nutrition-tracker-api-tests-{Guid.NewGuid():N}.db");

    public FoodApiWebApplicationFactory(Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:NutritionDatabase", $"Data Source={_databasePath}");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
        if (_configureServices is not null)
        {
            builder.ConfigureTestServices(_configureServices);
        }
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName, _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NutritionDbContext>();
        context.Database.Migrate();
        return host;
    }

    public async Task SeedAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NutritionDbContext>();
        context.AddRange(entities);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    public HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserIdHeader, userId.ToString());
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        DeleteIfExists(_databasePath);
        DeleteIfExists($"{_databasePath}-shm");
        DeleteIfExists($"{_databasePath}-wal");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
