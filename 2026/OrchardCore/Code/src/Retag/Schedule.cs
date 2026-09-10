namespace Retag;

/// <summary>
/// The faked concurrency. No real workers ever run: this class decides, for each
/// item, which taxonomy version the agent held and what time it decided. The
/// interleaving is deliberate - a worker that started a batch before the
/// republish finishes that batch on v41, so modification time alone cannot
/// separate the two versions.
/// </summary>
public static class Schedule
{
    public const int TotalItems = 4182;
    public const int V41Decisions = 3082;
    public const int V42Decisions = 1100;
    public const int DivergentUnderV42 = 380;
    public const int IdenticalUnderV42 = V41Decisions - DivergentUnderV42; // 2,702

    public const int TaxonomyBefore = 41;
    public const int TaxonomyAfter = 42;
    public const int TermCount = 312;

    public static readonly DateTime JobDate = new(2026, 8, 18);
    public static readonly DateTime Start = JobDate.AddHours(22).AddMinutes(40);            // 22:40:00
    public static readonly DateTime End = JobDate.AddHours(23).AddMinutes(21).AddSeconds(16); // 23:21:16
    public static readonly DateTime Cutover = JobDate.AddHours(23).AddMinutes(10);          // 23:10:00

    public static TimeSpan Duration => End - Start; // 00:41:16

    /// <summary>The worker that had just finished a batch when the taxonomy was republished.</summary>
    private const int FirstToPickUpV42 = 5;

    public static readonly (string Id, string Name, int Items)[] Tenants =
    [
        ("north-shore-council",   "North Shore Council",   428),
        ("west-valley-district",  "West Valley District",  356),
        ("eastmoor-borough",      "Eastmoor Borough",      511),
        ("granite-bay-council",   "Granite Bay Council",   392),
        ("fairfield-township",    "Fairfield Township",    274),
        ("lakeside-district",     "Lakeside District",     336),
        ("redstone-council",      "Redstone Council",      301),
        ("harbourview-borough",   "Harbourview Borough",   358),
        ("pinehurst-township",    "Pinehurst Township",    245),
        ("midvale-district",      "Midvale District",      319),
        ("stonebridge-council",   "Stonebridge Council",   383),
        ("westgate-borough",      "Westgate Borough",      279),
    ];

    public static int WorkerCount => Tenants.Length;

    public static int[] ItemCounts => Tenants.Select(t => t.Items).ToArray();

    /// <summary>Items each worker had decided before the republish - these carry v41.</summary>
    public static int[] V41PerWorker { get; } = Allocate(ItemCounts, V41Decisions);

    /// <summary>Emergency-preparedness items caught on the v41 side. Sums to 380.</summary>
    public static int[] EmergencyInV41 { get; } = Allocate(V41PerWorker, DivergentUnderV42);

    /// <summary>Emergency-preparedness items decided after the republish - correctly filed.</summary>
    public static int[] EmergencyInV42 { get; } =
        Enumerable.Range(0, Tenants.Length)
            .Select(w => Math.Max(1, (Tenants[w].Items - V41PerWorker[w]) * 12 / 100))
            .ToArray();

    /// <summary>
    /// Largest-remainder apportionment in pure integer arithmetic, so the split
    /// is identical on every machine and the totals land exactly.
    /// </summary>
    public static int[] Allocate(int[] weights, int total)
    {
        var sum = weights.Sum();
        var result = new int[weights.Length];
        var remainders = new (int Index, int Remainder)[weights.Length];
        var assigned = 0;

        for (var i = 0; i < weights.Length; i++)
        {
            var numerator = (long)weights[i] * total;
            result[i] = (int)(numerator / sum);
            remainders[i] = (i, (int)(numerator % sum));
            assigned += result[i];
        }

        foreach (var (index, _) in remainders
                     .OrderByDescending(r => r.Remainder)
                     .ThenBy(r => r.Index)
                     .Take(total - assigned))
        {
            result[index]++;
        }

        return result;
    }

    // Per-worker timing. Fixed arithmetic, no RNG, so the timeline never drifts.
    private static int Stagger(int w) => w == 0 ? 0 : (w * 17 + 3) % 96;

    /// <summary>Seconds the in-flight batch ran past the republish. Negative means it finished early.</summary>
    private static int Overrun(int w) => w == FirstToPickUpV42 ? -25 : 40 + (w * 53) % 200;

    private static int Gap(int w) => w == FirstToPickUpV42 ? 0 : (w * 29) % 170 + 1;

    private static int EndSlack(int w) => w == 0 ? 0 : (w * 13) % 55;

    public static DateTime WorkerStart(int w) => Start.AddSeconds(Stagger(w));

    public static DateTime PreCutoverEnd(int w) => Cutover.AddSeconds(Overrun(w));

    public static DateTime PostCutoverStart(int w)
    {
        var earliest = Cutover.AddSeconds(Gap(w));
        var afterInFlightBatch = PreCutoverEnd(w).AddSeconds(1);
        return earliest > afterInFlightBatch ? earliest : afterInFlightBatch;
    }

    public static DateTime WorkerEnd(int w) => End.AddSeconds(-EndSlack(w));

    /// <summary>Timestamp for item <paramref name="index"/> of worker <paramref name="w"/>.</summary>
    public static DateTime TimestampFor(int w, int index)
    {
        var cut = V41PerWorker[w];
        if (index < cut)
            return Spread(WorkerStart(w), PreCutoverEnd(w), cut, index);

        var postCount = Tenants[w].Items - cut;
        return Spread(PostCutoverStart(w), WorkerEnd(w), postCount, index - cut);
    }

    public static int VersionFor(int w, int index) =>
        index < V41PerWorker[w] ? TaxonomyBefore : TaxonomyAfter;

    private static DateTime Spread(DateTime from, DateTime to, int count, int index)
    {
        if (count <= 1) return from;
        var span = (long)(to - from).TotalSeconds;
        return from.AddSeconds(span * index / (count - 1));
    }

    /// <summary>
    /// Indices within a tenant that hold emergency-preparedness content, split so
    /// exactly <see cref="DivergentUnderV42"/> of them land on the v41 side.
    /// </summary>
    public static HashSet<int> EmergencyIndices(int w)
    {
        var cut = V41PerWorker[w];
        var total = Tenants[w].Items;
        var set = new HashSet<int>();
        AddSpread(set, 0, cut, EmergencyInV41[w]);
        AddSpread(set, cut, total, EmergencyInV42[w]);
        return set;
    }

    private static void AddSpread(HashSet<int> set, int start, int end, int count)
    {
        var span = end - start;
        if (count <= 0 || span <= 0) return;
        for (var j = 0; j < count; j++) set.Add(start + (int)((long)j * span / count));
    }
}
