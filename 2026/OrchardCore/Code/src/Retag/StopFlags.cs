namespace Retag;

public enum StopScope { Job, Tool, Tenant }

public sealed record StopFlag(StopScope Scope, string Id, string Reason)
{
    public string Display => StopFlags.Display(Scope, Id);
    public string ReleaseCommand => $"retag release --{Scope.ToString().ToLowerInvariant()} {Id}";
}

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
        if (File.Exists(PathFor(scope, id)) && Read(scope, id) is null)
            throw new InvalidOperationException("stop flag path belongs to another scope or ID.");
        File.WriteAllText(PathFor(scope, id), $"scope={scope.ToString().ToLowerInvariant()}\nid={id}\nreason={reason}\n");
    }

    public static bool Clear(StopScope scope, string id)
    {
        var path = PathFor(scope, id);
        if (Read(scope, id) is null) return false;
        File.Delete(path);
        return true;
    }

    /// <summary>Find a stop that applies to this job, its tool, or any participating tenant.</summary>
    public static StopFlag? Active(string jobId, string? toolId, IEnumerable<string> tenantIds)
    {
        if (!Directory.Exists(Paths.RunDir)) return null;

        // Prefer the job, then the tool, then tenants in a stable order.
        var candidates = new List<(StopScope Scope, string Id)> { (StopScope.Job, jobId) };
        if (toolId is not null) candidates.Add((StopScope.Tool, toolId));
        candidates.AddRange(tenantIds.Distinct().Order(StringComparer.Ordinal)
            .Select(id => (StopScope.Tenant, id)));

        foreach (var (scope, id) in candidates)
        {
            if (Read(scope, id) is { } flag) return flag;
        }

        return null;
    }

    private static StopFlag? Read(StopScope scope, string id)
    {
        var path = PathFor(scope, id);
        if (!File.Exists(path)) return null;

        var lines = File.ReadAllLines(path);
        // The scope and ID also matter: a job named "tool-x" shares a
        // filename with tool "x", but must not stop or release that tool.
        if (!lines.Contains($"scope={scope.ToString().ToLowerInvariant()}") ||
            !lines.Contains($"id={id}")) return null;

        var reason = lines.FirstOrDefault(l => l.StartsWith("reason=", StringComparison.Ordinal))?
            ["reason=".Length..] ?? "";
        return new StopFlag(scope, id, reason);
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
