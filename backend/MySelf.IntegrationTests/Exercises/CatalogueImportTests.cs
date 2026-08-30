using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using MySelf.Domain.Exercises;
using MySelf.Infrastructure.Persistence;
using MySelf.Tools.WgerImport.Import;
using MySelf.Tools.WgerImport.Snapshot;

namespace MySelf.IntegrationTests.Exercises;

/// <summary>
/// Exercises the wger snapshot -> PostgreSQL importer against the real database.
/// Everything runs inside a transaction that is rolled back, so the dev data is untouched.
/// A synthetic <c>Source</c> keeps it isolated from real "wger" rows regardless.
/// </summary>
public class CatalogueImportTests
{
    private const string TestSource = "test-wger-import";

    private static CatalogueSnapshot BuildSnapshot(string squatInstructions) => new(
        FetchedAt: DateTimeOffset.UtcNow,
        Source: TestSource,
        Categories: [new SnapshotCategory(9001, "Legs (test)"), new SnapshotCategory(9002, "Cardio (test)")],
        Muscles:
        [
            new SnapshotMuscle(9001, "Quads (test)", "Quadriceps femoris", IsFront: true),
            new SnapshotMuscle(9002, "Glutes (test)", "Gluteus maximus", IsFront: false),
        ],
        Equipment: [new SnapshotEquipment(9001, "Barbell (test)")],
        Exercises:
        [
            new SnapshotExercise(
                ExternalId: "test-squat",
                Name: "Test Squat",
                CategoryId: 9001,
                Instructions: squatInstructions,
                TrackingMode: nameof(TrackingMode.WeightAndReps),
                VariationGroupExternalId: null,
                SourceVersion: "v1",
                LicenseShortName: "CC-BY-SA 4",
                LicenseUrl: "https://creativecommons.org/licenses/by-sa/4.0/",
                Attribution: "tester",
                PrimaryMuscleIds: [9001],
                SecondaryMuscleIds: [9002],
                EquipmentIds: [9001]),
            new SnapshotExercise(
                ExternalId: "test-run",
                Name: "Test Run",
                CategoryId: 9002,
                Instructions: null,
                TrackingMode: nameof(TrackingMode.Duration),
                VariationGroupExternalId: null,
                SourceVersion: "v1",
                LicenseShortName: "CC-BY-SA 4",
                LicenseUrl: null,
                Attribution: null,
                PrimaryMuscleIds: [],
                SecondaryMuscleIds: [],
                EquipmentIds: []),
        ]);

    private static MySelfDbContext CreateDbContext()
    {
        Env.NoClobber().TraversePath().Load();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is not set (.env).");

        var options = new DbContextOptionsBuilder<MySelfDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new MySelfDbContext(options);
    }

    [Fact]
    public async Task Import_inserts_the_catalogue_then_re_import_updates_in_place()
    {
        await using var db = CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Act 1 — first import
        var first = await CatalogueImporter.ImportAsync(db, BuildSnapshot("<p>original</p>"));

        // Assert 1 — rows landed, links + category + tracking mode are correct
        Assert.Equal(2, first.Exercises);

        db.ChangeTracker.Clear();
        var squat = await db.Exercises
            .Include(e => e.Category)
            .Include(e => e.Muscles).ThenInclude(m => m.Muscle)
            .Include(e => e.Equipment).ThenInclude(x => x.Equipment)
            .SingleAsync(e => e.Source == TestSource && e.ExternalId == "test-squat");

        Assert.Equal("Test Squat", squat.Name);
        Assert.Equal("Legs (test)", squat.Category.Name);
        Assert.Equal(TrackingMode.WeightAndReps, squat.DefaultTrackingMode);
        Assert.Equal("<p>original</p>", squat.Instructions);
        Assert.Equal(MuscleRole.Primary, squat.Muscles.Single(m => m.MuscleId == 9001).Role);
        Assert.Equal(MuscleRole.Secondary, squat.Muscles.Single(m => m.MuscleId == 9002).Role);
        Assert.Equal("Barbell (test)", squat.Equipment.Single().Equipment.Name);

        // Act 2 — re-import with a changed field
        db.ChangeTracker.Clear();
        await CatalogueImporter.ImportAsync(db, BuildSnapshot("<p>revised</p>"));

        // Assert 2 — no duplicates, the change is applied
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.Exercises.CountAsync(e => e.Source == TestSource));
        var reloaded = await db.Exercises.SingleAsync(e => e.Source == TestSource && e.ExternalId == "test-squat");
        Assert.Equal("<p>revised</p>", reloaded.Instructions);
        Assert.Equal(2, await db.ExerciseCategories.CountAsync(c => c.Id >= 9000));

        await tx.RollbackAsync();
    }
}
