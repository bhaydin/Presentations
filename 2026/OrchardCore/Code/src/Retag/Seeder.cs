using System.Globalization;

namespace Retag;

/// <summary>
/// Writes decisions.json: the corpus of what the agent decided on the night of
/// the run. Fixed seed, fixed arithmetic, byte-identical every time.
/// </summary>
public static class Seeder
{
    public static List<Decision> Build(List<ContentItem> items, Taxonomy v41, Taxonomy v42)
    {
        var decisions = new List<Decision>(items.Count);
        var cursor = 0;

        for (var w = 0; w < Schedule.Tenants.Length; w++)
        {
            var count = Schedule.Tenants[w].Items;

            for (var i = 0; i < count; i++)
            {
                var item = items[cursor + i];
                var version = Schedule.VersionFor(w, i);
                var taxonomy = version == Schedule.TaxonomyBefore ? v41 : v42;

                decisions.Add(new Decision(
                    ItemId: item.Id,
                    Tenant: item.Tenant,
                    Chosen: [Classifier.Choose(item, version)],
                    TaxonomyVersion: version,
                    Timestamp: Stamp(Schedule.TimestampFor(w, i)),
                    Considered: Classifier.Considered(item, version, taxonomy),
                    Rationale: Classifier.Rationale(item, version)));
            }

            cursor += count;
        }

        return decisions;
    }

    public static string Stamp(DateTime value) =>
        value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    public static DateTime Parse(string stamp) =>
        DateTime.ParseExact(stamp, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    public static void Write(List<Decision> decisions) =>
        Json.Write(Paths.Fixture("decisions.json"), decisions);

    private static List<Decision>? _cached;

    public static List<Decision> Load() =>
        _cached ??= Json.Read<List<Decision>>(Paths.Fixture("decisions.json"));

    public static void Invalidate() => _cached = null;
}
