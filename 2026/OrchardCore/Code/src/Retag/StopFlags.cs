namespace Retag;

public enum StopScope { Job, Tool, Tenant }

/// <summary>
/// Rung 0. A flag file and an if-statement. That is the whole mechanism, and it
/// is deliberately not more than that: the thing that stops the agent has to be
/// simpler than the agent.
/// </summary>
public static class StopFlags
{
    public static string FileName(StopScope scope, string id) => scope switch
    {
        StopScope.Job => $"{id}.stopped",
        StopScope.Tool => $"tool-{id}.stopped",
        StopScope.Tenant => $"tenant-{id}.stopped",
        _ => $"{id}.stopped",
    };

    public static string PathFor(StopScope scope, string id) =>
        Path.Combine(Paths.RunDir, FileName(scope, id));

    /// <summary>Repo-relative, for printing. Always forward slashes.</summary>
    public static string Display(StopScope scope, string id) => ".run/" + FileName(scope, id);

    public static void Write(StopScope scope, string id, string reason)
    {
        Directory.CreateDirectory(Paths.RunDir);
        File.WriteAllText(PathFor(scope, id), $"scope={scope.ToString().ToLowerInvariant()}\nid={id}\nreason={reason}\n");
    }

    public static bool Clear(StopScope scope, string id)
    {
        var path = PathFor(scope, id);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    /// <summary>The if-statement. Any run checks this before it starts.</summary>
    public static (string Display, string Reason)? Active()
    {
        if (!Directory.Exists(Paths.RunDir)) return null;

        var flag = Directory.GetFiles(Paths.RunDir, "*.stopped")
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
            .FirstOrDefault();

        if (flag is null) return null;

        var reason = File.ReadAllLines(flag)
            .FirstOrDefault(l => l.StartsWith("reason=", StringComparison.Ordinal))?["reason=".Length..] ?? "";

        return (".run/" + Path.GetFileName(flag), reason);
    }

    public static void ClearAll()
    {
        if (!Directory.Exists(Paths.RunDir)) return;
        foreach (var f in Directory.GetFiles(Paths.RunDir, "*.stopped"))
        {
            try
            {
                File.Delete(f);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>Other jobs on the platform, so the stop is visibly scoped and not a kill switch.</summary>
    public static readonly string[] OtherJobs = ["seo-audit", "link-check"];
}
