using System.Diagnostics;

namespace Retag;

/// <summary>
/// Tracing on the OpenTelemetry API surface that ships in .NET: ActivitySource
/// and ActivityListener. The renderer is ours on purpose - the console exporter
/// does not give enough control over layout, and the layout is the deliverable.
/// </summary>
public static class RetagTracing
{
    public const string SourceName = "retag";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    // The attribute set emitted on every decision.
    public const string TenantId = "orchard.tenant_id";
    public const string ContentItemId = "orchard.content_item_id";
    public const string TaxonomyVersion = "taxonomy.version";
    public const string AgentRelease = "agent.release";
    public const string RetrievalConsidered = "retrieval.considered";
    public const string DecisionChosen = "decision.chosen";

    /// <summary>The attribute that would have caught this, in the order we print it.</summary>
    public static readonly string[] DecisionAttributes =
    [
        TenantId, ContentItemId, TaxonomyVersion, AgentRelease, RetrievalConsidered, DecisionChosen,
    ];

    public static Capture Listen() => new();

    /// <summary>Collects finished spans in memory so they can be rendered as a tree.</summary>
    public sealed class Capture : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly List<Activity> _spans = [];

        public Capture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a => _spans.Add(a),
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<Activity> Spans => _spans;

        public void Dispose() => _listener.Dispose();
    }

    // ------------------------------------------------------------- rendering

    private const int AttributeIndent = 6;

    /// <summary>
    /// Fixed, and chosen so the longest possible attribute - a 23-character name
    /// against a 39-character content item id - still lands inside 72 columns.
    /// </summary>
    private const int AttributeValueColumn = 32;

    /// <summary>
    /// Renders captured spans as a tree. Attribute columns are fixed rather than
    /// derived from depth, so no trace can ever push past 72 columns.
    /// </summary>
    public static void Render(IReadOnlyList<Activity> spans, bool showAttributes)
    {
        var byParent = spans
            .GroupBy(s => s.ParentSpanId.ToString())
            .ToDictionary(g => g.Key, g => g.ToList());

        var ids = spans.Select(s => s.SpanId.ToString()).ToHashSet();
        var roots = spans.Where(s => !ids.Contains(s.ParentSpanId.ToString())).ToList();

        foreach (var root in roots) RenderSpan(root, byParent, 0, showAttributes);
    }

    private static void RenderSpan(
        Activity span, Dictionary<string, List<Activity>> byParent, int depth, bool showAttributes)
    {
        // Depth 1 sits flush under the root; every level below steps in by three.
        var indent = new string(' ', depth == 0 ? 2 : 2 + 3 * (depth - 1));
        var connector = depth == 0 ? "" : "└─ ";
        var subject = span.GetTagItem("span.subject") as string;

        Ui.Line(indent + connector + span.DisplayName + (subject is null ? "" : "  " + subject));

        if (showAttributes)
            foreach (var name in DecisionAttributes)
                if (span.GetTagItem(name) is { } value)
                    Ui.Line(Attribute(name, value.ToString() ?? "", name == TaxonomyVersion));

        if (byParent.TryGetValue(span.SpanId.ToString(), out var children))
            foreach (var child in children)
                RenderSpan(child, byParent, depth + 1, showAttributes);
    }

    /// <summary>The one thing to look at is amber. Everything else is default white.</summary>
    public static string Attribute(string name, string value, bool highlight)
    {
        var head = new string(' ', AttributeIndent) + name + " ";
        var dots = AttributeValueColumn - 1 - head.Length;
        if (dots < 1) dots = 1;
        var line = head + new string('.', dots) + " " + value;
        return highlight ? Ui.Amber(line) : line;
    }

    /// <summary>Builds the span tree for one recorded decision. Real spans, real listener.</summary>
    public static void EmitDecision(Decision decision, ContentItem item, int release, string jobId)
    {
        using var job = Source.StartActivity("retag.job");
        job?.SetTag("span.subject", jobId);

        using var lot = Source.StartActivity("tenant.lot");
        lot?.SetTag("span.subject", decision.Tenant);

        using var classify = Source.StartActivity("agent.classify");
        classify?.SetTag("span.subject", item.Id.Split('/')[^1]);
        classify?.SetTag(TenantId, decision.Tenant);
        classify?.SetTag(ContentItemId, decision.ItemId);
        classify?.SetTag(TaxonomyVersion, decision.TaxonomyVersion.ToString());
        classify?.SetTag(AgentRelease, release.ToString());
        classify?.SetTag(RetrievalConsidered, decision.Considered.Count.ToString());
        classify?.SetTag(DecisionChosen, string.Join(", ", decision.Chosen));
    }
}
