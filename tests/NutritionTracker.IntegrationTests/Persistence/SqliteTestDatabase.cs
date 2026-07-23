using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NutritionTracker.Infrastructure.Persistence;

namespace NutritionTracker.IntegrationTests.Persistence;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly DbContextOptions<NutritionDbContext> _options;

    private SqliteTestDatabase(string databasePath, DbContextOptions<NutritionDbContext> options)
    {
        _databasePath = databasePath;
        _options = options;
    }

    public static async Task<SqliteTestDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        return await CreateAtMigrationAsync(null, cancellationToken);
    }

    public static async Task<SqliteTestDatabase> CreateAtMigrationAsync(
        string? targetMigration,
        CancellationToken cancellationToken)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nutrition-tracker-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<NutritionDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        var database = new SqliteTestDatabase(databasePath, options);

        await using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(targetMigration, cancellationToken);
        return database;
    }

    public NutritionDbContext CreateContext()
    {
        return new NutritionDbContext(_options);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        DeleteIfExists(_databasePath);
        DeleteIfExists($"{_databasePath}-shm");
        DeleteIfExists($"{_databasePath}-wal");
        return ValueTask.CompletedTask;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
