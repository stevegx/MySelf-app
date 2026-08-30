namespace MySelf.Tools.WgerImport;

/// <summary>Locates repo directories regardless of where the tool is run from.</summary>
public static class RepoPaths
{
    public static string SeedDataDir => Path.Combine(RepoRoot(), "backend", "seed-data");

    public static string CatalogueSnapshot => Path.Combine(SeedDataDir, "wger-catalogue.json");

    public static string TrackingModeOverrides =>
        Path.Combine(SeedDataDir, "tracking-mode-overrides.json");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "backend", "MySelf.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate the repo root (looked for backend/MySelf.sln).");
    }
}
