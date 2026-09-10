namespace Retag;

/// <summary>Locates the repo root so fixtures resolve the same however the app is launched.</summary>
public static class Paths
{
    private static string? _root;

    public static string Root => _root ??= Discover();

    public static string Fixtures => Path.Combine(Root, "fixtures");
    public static string RunDir => Path.Combine(Root, ".run");

    public static string Fixture(string name) => Path.Combine(Fixtures, name);
    public static string Output(string name) => Path.Combine(Root, name);

    private static string Discover()
    {
        var env = Environment.GetEnvironmentVariable("RETAG_ROOT");
        if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "retag.slnx"))) return dir.FullName;
                dir = dir.Parent;
            }
        }
        return Directory.GetCurrentDirectory();
    }
}
