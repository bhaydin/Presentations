namespace Retag;

/// <summary>
/// The seeded classifier. It is not a model: it replays what the agent decided
/// on the night, and re-evaluates a decision against a different taxonomy
/// version. Deterministic by design - replay must give the same answer every
/// time or the compensation set is not trustworthy.
/// </summary>
public static class Classifier
{
    /// <summary>Stable across processes and machines. GetHashCode is not.</summary>
    public static ulong Hash(string text)
    {
        var h = 14695981039346656037UL;
        foreach (var c in text)
        {
            h ^= c;
            h *= 1099511628211UL;
        }
        return h;
    }

    public static string Choose(ContentItem item, int taxonomyVersion)
    {
        Fixtures.ValidateVersion(taxonomyVersion);
        return item.EmergencyPreparedness
            ? taxonomyVersion == Schedule.TaxonomyBefore
                ? Fixtures.EmergencyTermV41
                : Fixtures.EmergencyTerm
            : item.TrueTerm;
    }

    /// <summary>The shortlist the agent had in front of it. Always contains the chosen term.</summary>
    public static List<string> Considered(ContentItem item, int taxonomyVersion, Taxonomy taxonomy)
    {
        var chosen = Choose(item, taxonomyVersion);
        var rng = new Rng(Hash(item.Id + ":" + taxonomyVersion));
        var considered = new List<string> { chosen };

        if (item.EmergencyPreparedness)
        {
            // The two terms whose guidance moved, plus near neighbours.
            foreach (var t in new[] { "public-safety", "community-events", "storm-readiness", "public-workshops" })
                if (!considered.Contains(t)) considered.Add(t);
        }

        var pool = taxonomy.Terms;
        var target = 4 + rng.Next(3); // 4..6
        var guard = 0;
        while (considered.Count < target && guard++ < 64)
        {
            var candidate = pool[rng.Next(pool.Count)].Id;
            if (!considered.Contains(candidate)) considered.Add(candidate);
        }

        return considered;
    }

    public static string Rationale(ContentItem item, int taxonomyVersion)
    {
        if (item.EmergencyPreparedness)
            return taxonomyVersion == Schedule.TaxonomyBefore
                ? "Preparedness session run as a public event; v41 guidance files these under community-events."
                : "Preparedness content; v42 guidance files these under public-safety regardless of delivery format.";

        return $"Title and body are squarely about {Fixtures.Label(item.TrueTerm).ToLowerInvariant()}.";
    }

    /// <summary>
    /// Re-evaluate a recorded decision against another taxonomy version. No model
    /// call: the guidance change is deterministic, so the answer is too.
    /// </summary>
    public static bool WouldDiffer(Decision decision, ContentItem item, int targetVersion) =>
        Choose(item, targetVersion) != decision.Chosen[0];
}
