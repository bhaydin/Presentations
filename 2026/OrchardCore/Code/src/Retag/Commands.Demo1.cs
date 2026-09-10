using System.Globalization;

namespace Retag;

/// <summary>
/// Demo 1: the spine. A job that ran clean, a trace that says which taxonomy
/// version each decision held, an aggregate over that attribute, and a replay
/// that turns 3,082 suspect decisions into a 380-item compensation set.
/// </summary>
public static partial class Commands
{
    /// <summary>Column the dot leaders in the job output resolve to.</summary>
    private const int JobValueColumn = 35;

    /// <summary>Column the replay leaders resolve to.</summary>
    private const int ReplayValueColumn = 46;

    public const string DefaultJobId = "retag-2026";
    public const string ClassifierToolId = "retag-classifier";
    private const int AgentRelease = 17;

    public const string ReplayFile = "replay-v41.txt";
    public const string CompensateFile = "compensate.txt";

    // ------------------------------------------------------------------ seed

    public static int Seed(Args args)
    {
        Ui.Blank();

        Fixtures.GenerateAll();
        Fixtures.Invalidate();

        var decisions = Seeder.Build(Fixtures.Items, Fixtures.V41, Fixtures.V42);
        Seeder.Write(decisions);
        Seeder.Invalidate();

        var v41 = decisions.Count(d => d.TaxonomyVersion == Schedule.TaxonomyBefore);
        var v42 = decisions.Count - v41;

        Ui.Line(Ui.Leader("generating fixtures", "5 files", JobValueColumn));
        Ui.Line(Ui.Leader("content items", Ui.N(Fixtures.Items.Count), JobValueColumn));
        Ui.Line(Ui.Leader("decisions", Ui.N(decisions.Count), JobValueColumn));
        Ui.Line(Ui.Leader("  taxonomy v41", Ui.N(v41) + "   " + Ui.Amber("SUPERSEDED"), JobValueColumn));
        Ui.Line(Ui.Leader("  taxonomy v42", Ui.N(v42) + "   " + Ui.Green("current"), JobValueColumn));
        Ui.Line(Ui.Leader("window", $"{Time(Schedule.Start, "HH:mm:ss")} → {Time(Schedule.End, "HH:mm:ss")}", JobValueColumn));
        Ui.Blank();
        Ui.Line($"{Ui.Indent}→ fixtures/decisions.json");
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }

    // ------------------------------------------------------------------- run

    public static int RunJob(Args args)
    {
        var workers = args.Int("--workers", Schedule.WorkerCount);
        var items = Fixtures.Items.Count;
        var decisions = Seeder.Load();

        Ui.Blank();
        Ui.Line(Ui.Leader("scanning content items", $"{Ui.N(items)} found", JobValueColumn));
        Ui.Line(Ui.Leader("loading taxonomy v41", $"{Ui.N(Schedule.TermCount)} terms", JobValueColumn));
        Ui.Line($"{Ui.Indent}{workers} tenant workers started");
        Countdown("proposing", items, $"{Ui.N(items)} / {Ui.N(items)}");
        Countdown("applying", items, $"{Ui.N(items)} updated");
        Ui.Blank();

        Ui.Line(Field("TENANTS:", Ui.N(Fixtures.Tenants.Count)));
        Ui.Line(Field("UPDATED:", Ui.N(decisions.Count)));
        Ui.Line(Field("ERRORS:", "0"));
        Ui.Line(Field("DURATION:",
            $"{Schedule.Duration:hh\\:mm\\:ss}      {Schedule.Start:HH\\:mm} → {Schedule.End:HH\\:mm}"));
        Ui.Blank();
        Ui.Line($"{Ui.Indent}{Ui.Green("done.")}");
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }

    /// <summary>Times are invariant so the output is identical on every machine.</summary>
    private static string Time(DateTime value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>Label field in the summary block: values all land on the same column.</summary>
    private static string Field(string label, string value) => Ui.Indent + label.PadRight(11) + value;

    /// <summary>
    /// Counters animate for a live audience and collapse to the final line when
    /// redirected, so the golden files stay byte-identical.
    /// </summary>
    private static void Countdown(string label, int total, string finalValue)
    {
        if (Ui.Animate)
        {
            const int steps = 14;
            for (var i = 1; i < steps; i++)
            {
                var shown = (int)((long)total * i / steps);
                Console.Out.Write("\r" + Ui.Leader(label, $"{Ui.N(shown)} / {Ui.N(total)}", JobValueColumn));
                Thread.Sleep(45);
            }
            Console.Out.Write("\r");
        }

        Ui.Line(Ui.Leader(label, finalValue, JobValueColumn));
    }

    // --------------------------------------------------------------- propose

    public static async Task<int> Propose(Args args, Config config)
    {
        var itemId = args.Value("--item") ?? Fixtures.SlideItemId;
        var live = args.Has("--live");

        if (!Fixtures.ItemsById.TryGetValue(itemId, out var item))
        {
            Ui.Blank();
            Ui.Line($"{Ui.Indent}{Ui.Amber("unknown item: " + itemId)}");
            Ui.Blank();
            Ui.Footer(1, 0);
            return 1;
        }

        var decision = Seeder.Load().First(d => d.ItemId == itemId);

        Ui.Blank();
        Ui.Line($"{Ui.Indent}proposing  {itemId}");

        if (live)
        {
            var agent = LiveAgent.Create(config);
            var taxonomy = Fixtures.ForVersion(decision.TaxonomyVersion);
            var chosen = await agent.ClassifyAsync(
                item.Title, item.Body, decision.Considered, taxonomy, AgentRelease);

            Ui.Line(Ui.Leader("source", "live model call", JobValueColumn));
            Ui.Line(Ui.Leader("deployment", agent.Deployment, JobValueColumn));
            decision = decision with { Chosen = [chosen] };
        }
        else
        {
            Ui.Line(Ui.Leader("source", "seeded decision (no model call)", JobValueColumn));
        }

        Ui.Blank();
        EmitAndRender(decision, item, showAttributes: true);
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }

    // ----------------------------------------------------------- trace show

    public static int TraceShow(Args args)
    {
        var itemId = args.Value("--item") ?? Fixtures.SlideItemId;

        if (!Fixtures.ItemsById.TryGetValue(itemId, out var item))
        {
            Ui.Blank();
            Ui.Line($"{Ui.Indent}{Ui.Amber("unknown item: " + itemId)}");
            Ui.Blank();
            Ui.Footer(1, 0);
            return 1;
        }

        var decision = Seeder.Load().First(d => d.ItemId == itemId);

        Ui.Blank();
        EmitAndRender(decision, item, showAttributes: args.Has("--attrs"));
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }

    private static void EmitAndRender(Decision decision, ContentItem item, bool showAttributes)
    {
        using var capture = RetagTracing.Listen();
        RetagTracing.EmitDecision(decision, item, AgentRelease, DefaultJobId);
        RetagTracing.Render(capture.Spans, showAttributes);
    }

    // ------------------------------------------------------- traces group-by

    public static int TracesGroupBy(Args args)
    {
        var attribute = args.At(2) ?? RetagTracing.TaxonomyVersion;

        if (attribute != RetagTracing.TaxonomyVersion)
        {
            Ui.Blank();
            Ui.Line($"{Ui.Indent}{Ui.Amber("group-by supports " + RetagTracing.TaxonomyVersion)}");
            Ui.Blank();
            Ui.Footer(1, 0);
            return 1;
        }

        var decisions = Seeder.Load();
        var v41 = decisions.Where(d => d.TaxonomyVersion == Schedule.TaxonomyBefore).ToList();
        var v42Count = decisions.Count - v41.Count;

        var rule = Ui.Indent + "-------   ---------   ------";

        Ui.Blank();
        Ui.Line(Ui.Indent + "version".PadRight(7) + "   " + "decisions".PadRight(9) + "   " + "status");
        Ui.Line(rule);
        Ui.Line(Row("42", v42Count, Ui.Green("current")));
        Ui.Line(Row("41", v41.Count, Ui.Amber("SUPERSEDED")));
        Ui.Line(rule);
        Ui.Line(Ui.Indent + new string(' ', 7) + "   " + Ui.N(decisions.Count).PadLeft(9));
        Ui.Blank();

        File.WriteAllText(Paths.Output(ReplayFile), string.Join("\n", v41.Select(d => d.ItemId)) + "\n");
        Ui.Line($"{Ui.Indent}→ {Ui.N(v41.Count)} item ids written to {ReplayFile}");
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }

    private static string Row(string version, int count, string status) =>
        Ui.Indent + version.PadRight(7) + "   " + Ui.N(count).PadLeft(9) + "   " + status;

    // ---------------------------------------------------------------- replay

    public static int Replay(Args args)
    {
        var from = args.Value("--from") ?? ReplayFile;
        var target = args.Int("--taxonomy", Schedule.TaxonomyAfter);
        var path = Paths.Output(from);

        Ui.Blank();

        if (!File.Exists(path))
        {
            Ui.Line($"{Ui.Indent}{Ui.Amber(from + " not found - run: retag traces group-by taxonomy.version")}");
            Ui.Blank();
            Ui.Footer(1, 0);
            return 1;
        }

        var ids = File.ReadAllLines(path).Where(l => l.Length > 0).ToList();
        var byId = Seeder.Load().ToDictionary(d => d.ItemId);
        var items = Fixtures.ItemsById;

        var differ = new List<string>();
        var identical = 0;

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var decision) || !items.TryGetValue(id, out var item)) continue;
            if (Classifier.WouldDiffer(decision, item, target)) differ.Add(id);
            else identical++;
        }

        Ui.Line(Ui.Leader(
            $"replaying {Ui.N(ids.Count)} decisions under v{target}", Ui.Green("done"), ReplayValueColumn));
        Ui.Line(Ui.Leader("proposals identical", Ui.N(identical), ReplayValueColumn));
        Ui.Line(Ui.Leader("proposals differ", Ui.Amber(Ui.N(differ.Count)), ReplayValueColumn));
        Ui.Blank();

        File.WriteAllText(Paths.Output(CompensateFile), string.Join("\n", differ) + "\n");
        Ui.Line($"{Ui.Indent}→ {Ui.N(differ.Count)} item ids written to {CompensateFile}");
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }
}
