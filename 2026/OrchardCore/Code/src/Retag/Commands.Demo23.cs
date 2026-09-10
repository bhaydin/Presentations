namespace Retag;

/// <summary>
/// Demo 2 (the canary) and demo 3 (hold and stop). The load-bearing behaviour
/// here is what does NOT happen: a held lot commits nothing, and it is not an
/// error.
/// </summary>
public static partial class Commands
{
    /// <summary>
    /// Release 18 was picked up part-way through the run, at the fourth tenant.
    /// Tenants processed before that are still on 17 - which is why the first
    /// three commit cleanly and the fourth does not.
    /// </summary>
    private const int RolloutTenantIndex = 3;

    private const int DefaultCanarySize = 40;

    private sealed record CaseResult(KnownAnswer Case, string? Answer)
    {
        public bool Pass => Answer == Case.ExpectedTerm;
        public bool ChoseParent => Answer is not null && !Pass && Answer == Case.ParentTerm;
    }

    // ---------------------------------------------------------- canary + lot

    public static async Task<int> RunCanary(Args args, Config config, Func<Config, LiveAgent>? createAgent = null)
    {
        var canarySize = args.Int("--canary", DefaultCanarySize);
        var requested = args.Int("--release", AgentRelease);
        // Opt-in, not automatic. A live canary is genuinely cheap, but it does
        // not reproduce the scripted 14/20 - the whole point of the fixture is
        // that release 18 broadens exactly the argued cases - and the numbers on
        // the slides have to match what is on screen. Pass --live to see the
        // real thing; leave it off to see the story.
        var useLive = args.Has("--live") && config.IsConfigured;
        var cases = Fixtures.KnownAnswerCases;
        var tenants = Fixtures.Tenants;

        Ui.Blank();

        for (var i = 0; i < tenants.Count; i++)
        {
            var release = requested >= 18 && i >= RolloutTenantIndex ? requested : AgentRelease;
            var (results, liveAvailable) = await EvaluateAsync(cases, release, useLive, config,
                createAgent ?? LiveAgent.Create);
            useLive = liveAvailable;
            var passed = results.Count(r => r.Pass);
            var label = $"tenant {i + 1}";

            if (passed == cases.Count)
            {
                Ui.Line($"{Ui.Indent}{label,-8} .... canary {canarySize} · {passed}/{cases.Count} · "
                        + $"{Ui.Green("COMMITTED")}  {Ui.N(tenants[i].ItemCount)} items");
                continue;
            }

            // The canary failed. Nothing from this tenant is committed - not
            // committed and reverted, never committed at all.
            Ui.Line($"{Ui.Indent}{label,-8} .... canary {canarySize} ·");
            Ui.Blank();
            PrintCategories(results);
            Ui.Blank();
            PrintVerdict(results, passed, cases.Count);
            Ui.Blank();
            Ui.Line($"{Ui.Indent}{Ui.Amber("❯ CANARY HELD. Proposals remain drafts.")}");
            Ui.Line($"{Ui.Indent}  0 items committed for this tenant.");
            Ui.Blank();
            Ui.Footer(0, 1);
            return 0;
        }

        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }

    private static void PrintCategories(List<CaseResult> results)
    {
        foreach (var category in KnownAnswers.Categories)
        {
            var inCategory = results.Where(r => r.Case.Category == category).ToList();
            var passed = inCategory.Count(r => r.Pass);
            var ok = passed == inCategory.Count;
            var bar = Ui.Bar(passed, inCategory.Count);
            var verdict = ok ? Ui.Green("PASS") : Ui.Amber("FAIL");

            Ui.Line($"    {category,-17}{(ok ? Ui.Green(bar) : Ui.Amber(bar))}"
                    + $"  {passed}/{inCategory.Count}   {verdict}");
        }
    }

    private static void PrintVerdict(List<CaseResult> results, int passed, int total)
    {
        var failures = results.Where(r => !r.Pass).ToList();
        var invalid = results.Count(r => r.Answer is null);
        var note = invalid > 0
            ? $"{invalid} invalid model response(s)"
            : failures.Count > 0 && failures.All(f => f.ChoseParent)
            ? "every failure chose the parent term"
            : "see fixtures/known-answers.json";

        Ui.Line($"    {passed}/{total} — {note}");
    }

    private static List<CaseResult> Seeded(List<KnownAnswer> cases, int release) =>
        cases.Select(c => new CaseResult(c, KnownAnswers.Answer(c, release))).ToList();

    /// <summary>
    /// Live when credentials are configured - 40 items is cheap. If the call
    /// fails we say so in one line and fall back to seeded behaviour rather than
    /// dropping a stack trace into the middle of a demo.
    /// </summary>
    private static async Task<(List<CaseResult> Results, bool LiveAvailable)> EvaluateAsync(
        List<KnownAnswer> cases, int release, bool useLive, Config config, Func<Config, LiveAgent> createAgent)
    {
        if (!useLive) return (Seeded(cases, release), false);

        var results = new List<CaseResult>(cases.Count);
        try
        {
            var agent = createAgent(config);
            var taxonomy = Fixtures.V42;

            foreach (var c in cases)
            {
                var candidates = Candidates(c, taxonomy);
                var answer = await agent.ClassifyAsync(c.Title, c.Body, candidates, taxonomy, release);
                results.Add(new CaseResult(c, answer));
            }

            return (results, true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Ui.Line($"{Ui.Indent}{Ui.Amber("live call unavailable - using seeded canary")}");
            // Preserve every answer already evaluated, including invalid ones.
            // A later transport failure must not turn an observed failure green.
            results.AddRange(Seeded(cases.Skip(results.Count).ToList(), release));
            return (results, false);
        }
    }

    private static List<string> Candidates(KnownAnswer c, Taxonomy taxonomy)
    {
        var list = new List<string> { c.ExpectedTerm };
        if (c.ParentTerm is not null) list.Add(c.ParentTerm);

        var parent = c.ParentTerm ?? c.ExpectedTerm;
        foreach (var sibling in taxonomy.Terms.Where(t => t.Parent == parent).Take(4))
            if (!list.Contains(sibling.Id)) list.Add(sibling.Id);

        return list;
    }

    // ------------------------------------------------------------------ gate

    public static int RunGate(Args args)
    {
        var reason = args.Value("--gate", "tenant-boundary");
        var canarySize = args.Int("--canary", DefaultCanarySize);
        var tenants = Fixtures.Tenants;

        Ui.Blank();
        Ui.Line($"{Ui.Indent}batch complete. next lot crosses tenant:");
        Ui.Line($"{Ui.Indent}    from: {tenants[0].Id}");
        Ui.Line($"{Ui.Indent}    to:   {tenants[1].Id}");
        Ui.Blank();
        Ui.Line($"{Ui.Indent}{Ui.Amber("[HELD BY POLICY]")}");
        Ui.Line($"{Ui.Indent}  Reason:   {reason.Replace('-', '_')}");
        Ui.Line($"{Ui.Indent}  Lot:      {canarySize} items, "
                + $"{KnownAnswers.Total}/{KnownAnswers.Total} canary pass");
        Ui.Line($"{Ui.Indent}  Awaiting: human decision (approve / deny)");
        Ui.Blank();

        // A hold is a success. Exit code 0, and the counters keep them apart.
        Ui.Footer(0, 1, "  |  ");
        return 0;
    }

    // ------------------------------------------------------------------ stop

    public static int Stop(Args args)
    {
        var (scope, id) = ResolveScope(args);
        var reason = args.Value("--reason", "unspecified");

        StopFlags.Write(scope, id, reason);

        // The lots that never ran: the tenants still queued behind the hold.
        var cancelled = Fixtures.Tenants.Count - RolloutTenantIndex - 1;

        Ui.Blank();
        Ui.Line($"{Ui.Indent}flag written: {Ui.Amber(StopFlags.Display(scope, id))}");
        Ui.Line($"{Ui.Indent}pending lots cancelled: {cancelled}");
        Ui.Line($"{Ui.Indent}job status:");
        PrintJobStatus(scope == StopScope.Job ? id : DefaultJobId);
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }

    public static int Release(Args args)
    {
        var (scope, id) = ResolveScope(args);
        var cleared = StopFlags.Clear(scope, id);

        Ui.Blank();
        Ui.Line(cleared
            ? $"{Ui.Indent}flag cleared: {StopFlags.Display(scope, id)}"
            : $"{Ui.Indent}no flag at: {StopFlags.Display(scope, id)}");
        Ui.Line($"{Ui.Indent}job status:");
        PrintJobStatus(scope == StopScope.Job ? id : DefaultJobId);
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }

    private static (StopScope Scope, string Id) ResolveScope(Args args)
    {
        if (args.Value("--tool") is { } tool) return (StopScope.Tool, tool);
        if (args.Value("--tenant") is { } tenant) return (StopScope.Tenant, tenant);
        return (StopScope.Job, args.Value("--job", DefaultJobId));
    }

    private static void PrintJobStatus(string id)
    {
        foreach (var job in new[] { id, DefaultJobId }.Concat(StopFlags.OtherJobs).Distinct())
        {
            var flag = job == DefaultJobId
                ? StopFlags.Active(job, ClassifierToolId, Fixtures.Tenants.Select(t => t.Id))
                : StopFlags.Active(job, null, []);
            Ui.Line("     " + job.PadRight(16) + (flag is not null ? Ui.Amber("STOPPED") : Ui.Green("RUNNING")));
        }
    }

    private static void TryDelete(Action delete)
    {
        try
        {
            delete();
        }
        catch (IOException)
        {
            // Already gone, or held open by the sync client. Either way, moving on.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The if-statement, printed. A refused start is a hold, not an error.</summary>
    public static int RefuseToStart(StopFlag flag)
    {
        Ui.Blank();
        Ui.Line($"{Ui.Indent}{Ui.Amber("❯ STOPPED BY FLAG. " + flag.Display)}");
        if (flag.Reason.Length > 0) Ui.Line($"{Ui.Indent}  reason: {flag.Reason}");
        Ui.Line($"{Ui.Indent}  clear with: {flag.ReleaseCommand}");
        Ui.Blank();
        Ui.Footer(0, 1);
        return 0;
    }

    // ----------------------------------------------------------------- reset

    public static int Reset(Args args)
    {
        Ui.Blank();

        StopFlags.ClearAll();

        // Remove the directory if the filesystem lets us, but never fail over
        // it. Synced folders hold directory handles open and reset has to work
        // every time - it is the first thing run before every take.
        TryDelete(() => Directory.Delete(Paths.RunDir, recursive: true));

        foreach (var file in new[] { ReplayFile, CompensateFile })
            TryDelete(() => File.Delete(Paths.Output(file)));

        Fixtures.GenerateAll();
        Fixtures.Invalidate();
        var decisions = Seeder.Build(Fixtures.Items, Fixtures.V41, Fixtures.V42);
        Seeder.Write(decisions);
        Seeder.Invalidate();

        Ui.Line(Ui.Leader("clearing stop flags", "done", JobValueColumn));
        Ui.Line(Ui.Leader("removing output files", "done", JobValueColumn));
        Ui.Line(Ui.Leader("regenerating fixtures", "5 files", JobValueColumn));
        Ui.Line(Ui.Leader("reseeding decisions", Ui.N(decisions.Count), JobValueColumn));
        Ui.Blank();
        Ui.Line($"{Ui.Indent}{Ui.Green("baseline restored.")}");
        Ui.Blank();
        Ui.Footer(0, 0);
        return 0;
    }
}
