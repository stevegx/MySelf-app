using System.Text.Json;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Respawn.Graph;
using MySelf.Infrastructure.Persistence;
using MySelf.Tools.WgerImport;
using MySelf.Tools.WgerImport.Import;
using MySelf.Tools.WgerImport.Snapshot;

namespace MySelf.IntegrationTests;

/// <summary>
/// One per test run (a collection fixture). Owns a <see cref="Respawner"/> that wipes every
/// application table between tests, keeping only the migration history and the seeded
/// reference catalogue (exercises / categories / muscles / equipment). Combined with
/// <c>DisableTestParallelization</c> (see <c>AssemblyInfo.cs</c>) this makes each test start
/// from a known-empty database instead of relying on per-test <c>finally</c> cleanup that a
/// crashed test would skip.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private static readonly Table[] Preserve =
    [
        new("public", "__EFMigrationsHistory"),
        new("public", "exercises"),
        new("public", "exercise_categories"),
        new("public", "exercise_muscles"),
        new("public", "exercise_equipment"),
        new("public", "muscles"),
        new("public", "equipment"),
    ];

    private Respawner _respawner = null!;

    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        // TestBootstrap's module initializer has already pointed this at the dedicated
        // "<db>_test" database, so a test run never touches the developer's dev data.
        Env.NoClobber().TraversePath().Load();
        ConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=myself_test;Username=myself;Password=myself";
    }

    public async Task InitializeAsync()
    {
        await EnsureTestDatabaseReadyAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = Preserve,
        });

        await ResetAsync();
    }

    /// <summary>
    /// Creates the test database + schema on first run and seeds the reference catalogue
    /// (Respawn preserves it thereafter), so a fresh machine needs no manual setup.
    /// </summary>
    private async Task EnsureTestDatabaseReadyAsync()
    {
        var options = new DbContextOptionsBuilder<MySelfDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new MySelfDbContext(options);
        await db.Database.MigrateAsync();

        if (!await db.Exercises.AnyAsync())
        {
            await using var stream = File.OpenRead(RepoPaths.CatalogueSnapshot);
            var snapshot = await JsonSerializer.DeserializeAsync<CatalogueSnapshot>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("wger-catalogue.json was empty or invalid.");

            await CatalogueImporter.ImportAsync(db, snapshot);
        }
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}

/// <summary>
/// Base for every integration test that touches mutable data. Resets the database before
/// each test via <see cref="DatabaseFixture"/>. Concrete classes must also carry
/// <c>[Collection(DatabaseCollection.Name)]</c> so xUnit supplies the shared fixture.
/// </summary>
public abstract class DatabaseTest(DatabaseFixture db) : IAsyncLifetime
{
    protected DatabaseFixture Database { get; } = db;

    public Task InitializeAsync() => Database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
