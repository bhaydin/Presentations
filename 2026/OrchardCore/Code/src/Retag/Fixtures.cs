using System.Globalization;

namespace Retag;

/// <summary>
/// Generates and loads the committed fixtures. The content store is fake and
/// deterministic on purpose - it is the stage set, not the demonstration.
/// </summary>
public static class Fixtures
{
    public const ulong Seed = 0x5245544147323026UL; // "RETAG" + 2026

    public const string EmergencyTerm = "public-safety";
    public const string EmergencyTermV41 = "community-events";

    /// <summary>Named on a slide, so it is pinned: tenant 1, item 0, and on the v41 side.</summary>
    public const string SlideItemId = "north-shore-council/emergency-services";

    // ---------------------------------------------------------------- generate

    public static void GenerateAll()
    {
        Json.Write(Paths.Fixture("tenants.json"), BuildTenants());
        Json.Write(Paths.Fixture("taxonomy-v41.json"), BuildTaxonomy(41));
        Json.Write(Paths.Fixture("taxonomy-v42.json"), BuildTaxonomy(42));
        Json.Write(Paths.Fixture("content-items.json"), BuildContentItems());
        Json.Write(Paths.Fixture("known-answers.json"), KnownAnswers.Build());
    }

    public static List<Tenant> BuildTenants() =>
        Schedule.Tenants
            .Select((t, i) => new Tenant(i + 1, t.Id, t.Name, t.Items))
            .ToList();

    public static Taxonomy BuildTaxonomy(int version)
    {
        var terms = new List<Term>();

        foreach (var (id, children) in TaxonomyData.Categories)
        {
            terms.Add(new Term(id, null, Label(id), GuidanceFor(id, version)));
            foreach (var child in children)
                terms.Add(new Term(child, id, Label(child), GuidanceFor(child, version)));
        }

        var name = version == 41 ? "consolidated-2026" : "consolidated-2026";
        return new Taxonomy(version, name, terms);
    }

    private static string GuidanceFor(string id, int version) => (id, version) switch
    {
        ("community-events", 41) => TaxonomyData.CommunityEventsV41,
        ("community-events", 42) => TaxonomyData.CommunityEventsV42,
        ("public-safety", 41) => TaxonomyData.PublicSafetyV41,
        ("public-safety", 42) => TaxonomyData.PublicSafetyV42,
        _ => $"Content about {Label(id).ToLowerInvariant()}.",
    };

    public static List<ContentItem> BuildContentItems()
    {
        var rng = new Rng(Seed);
        var leafTerms = TaxonomyData.Categories
            .SelectMany(c => c.Children)
            .Where(t => t is not ("storm-readiness" or "flood-warnings" or "evacuation-routes"))
            .ToArray();

        var items = new List<ContentItem>(Schedule.TotalItems);

        for (var w = 0; w < Schedule.Tenants.Length; w++)
        {
            var (tenantId, _, count) = Schedule.Tenants[w];
            var emergency = Schedule.EmergencyIndices(w);

            for (var i = 0; i < count; i++)
            {
                var isEmergency = emergency.Contains(i);
                var term = isEmergency ? EmergencyTerm : leafTerms[rng.Next(leafTerms.Length)];
                var titleIndex = rng.Next(int.MaxValue);

                string slug, title;
                if (isEmergency)
                {
                    slug = w == 0 && i == 0 ? "emergency-services" : Slug("emergency-prep", i);
                    title = EmergencyTitles[titleIndex % EmergencyTitles.Length];
                }
                else
                {
                    slug = Slug(term, i);
                    title = Title(term, titleIndex);
                }

                items.Add(new ContentItem(
                    Id: $"{tenantId}/{slug}",
                    Tenant: tenantId,
                    Title: title,
                    Body: Body(term, isEmergency, titleIndex),
                    TrueTerm: term,
                    EmergencyPreparedness: isEmergency));
            }
        }

        return items;
    }

    /// <summary>Item slugs are capped at 18 characters so the trace tree never wraps at 72 columns.</summary>
    private static string Slug(string term, int index)
    {
        var stem = term.Length <= 14 ? term : term[..14];
        return stem.TrimEnd('-') + "-" + index.ToString("D3", CultureInfo.InvariantCulture);
    }

    public static string Label(string id) =>
        string.Join(' ', id.Split('-').Select((word, i) =>
            i == 0 ? char.ToUpperInvariant(word[0]) + word[1..] : word));

    private static readonly string[] TitlePatterns =
    [
        "{0} update for residents",
        "How to apply for {1}",
        "{0}: what changed this month",
        "Notice: {1} schedule change",
        "{0} information for residents",
        "Changes to {1} from next quarter",
        "{0} frequently asked questions",
        "Your guide to {1}",
        "{0} service review outcome",
        "{1}: fees and timeframes",
    ];

    private static readonly string[] EmergencyTitles =
    [
        "Household emergency preparedness checklist",
        "Storm season readiness: what to pack",
        "Prepare your family for a power outage",
        "Emergency grab bag: a resident's guide",
        "Wildfire readiness evening for neighbours",
        "Flood preparedness drop-in session",
        "Winter storm preparedness workshop",
        "Building a household emergency plan",
        "Neighbourhood emergency readiness meetup",
        "Earthquake preparedness open day",
        "Know your evacuation route: a walkthrough",
        "Preparedness basics for new residents",
    ];

    private static string Title(string term, int index)
    {
        var label = Label(term);
        return string.Format(
            CultureInfo.InvariantCulture,
            TitlePatterns[index % TitlePatterns.Length],
            label,
            label.ToLowerInvariant());
    }

    private static string Body(string term, bool isEmergency, int index) => isEmergency
        ? "A session for residents on preparing a household for severe weather and "
          + "power loss. Run as a public evening event with council staff attending."
        : $"Council information about {Label(term).ToLowerInvariant()}, including who to "
          + $"contact, current timeframes and what residents need to provide.";

    // -------------------------------------------------------------------- load

    private static List<Tenant>? _tenants;
    private static List<ContentItem>? _items;
    private static Dictionary<string, ContentItem>? _itemsById;
    private static Taxonomy? _v41;
    private static Taxonomy? _v42;
    private static List<KnownAnswer>? _knownAnswers;

    public static List<Tenant> Tenants => _tenants ??= Json.Read<List<Tenant>>(Paths.Fixture("tenants.json"));

    public static List<ContentItem> Items => _items ??= Json.Read<List<ContentItem>>(Paths.Fixture("content-items.json"));

    public static Dictionary<string, ContentItem> ItemsById =>
        _itemsById ??= Items.ToDictionary(i => i.Id);

    public static Taxonomy V41 => _v41 ??= Json.Read<Taxonomy>(Paths.Fixture("taxonomy-v41.json"));

    public static Taxonomy V42 => _v42 ??= Json.Read<Taxonomy>(Paths.Fixture("taxonomy-v42.json"));

    public static Taxonomy ForVersion(int version) => version == 41 ? V41 : V42;

    public static List<KnownAnswer> KnownAnswerCases =>
        _knownAnswers ??= Json.Read<List<KnownAnswer>>(Paths.Fixture("known-answers.json"));

    public static bool Exist() =>
        File.Exists(Paths.Fixture("content-items.json")) &&
        File.Exists(Paths.Fixture("decisions.json")) &&
        File.Exists(Paths.Fixture("known-answers.json"));

    public static void Invalidate()
    {
        _tenants = null;
        _items = null;
        _itemsById = null;
        _v41 = null;
        _v42 = null;
        _knownAnswers = null;
    }
}
