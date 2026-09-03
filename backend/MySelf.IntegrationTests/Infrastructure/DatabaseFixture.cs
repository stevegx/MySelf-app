using DotNetEnv;
using Npgsql;
using Respawn;
using Respawn.Graph;

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
        // Same source the API uses: ConnectionStrings__DefaultConnection, from the real
        // environment in CI or the repo-root .env locally.
        Env.NoClobber().TraversePath().Load();
        ConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=myself;Username=myself;Password=myself";
    }

    public async Task InitializeAsync()
    {
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
