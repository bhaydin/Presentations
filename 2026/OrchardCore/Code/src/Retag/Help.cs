namespace Retag;

public static class Help
{
    private const int DescriptionColumn = 31;

    private static readonly (string Usage, string What)[] CommandList =
    [
        ("seed", "regenerate fixtures and decisions.json"),
        ("run", "replay the overnight re-tag job"),
        ("propose --item <id>", "classify one item (--live calls a model)"),
        ("trace show --item <id>", "print the span tree for a decision"),
        ("traces group-by <attr>", "aggregate decisions by span attribute"),
        ("replay --from <file>", "re-evaluate under another taxonomy"),
        ("stop --job <id>", "write a stop flag"),
        ("release --job <id>", "clear a stop flag"),
        ("reset", "restore the green baseline"),
    ];

    private static readonly (string Usage, string What)[] RunFlags =
    [
        ("--taxonomy <name>", "taxonomy to load"),
        ("--workers <n>", "tenant workers"),
        ("--lot tenant", "one tenant per lot"),
        ("--canary <n>", "canary size; gates the lot"),
        ("--release <n>", "agent release (17 or 18)"),
        ("--gate tenant-boundary", "hold before crossing tenants"),
        ("--live", "real model calls in the canary"),
    ];

    public static void Print()
    {
        Ui.Blank();
        Ui.Line($"{Ui.Indent}retag — AgentOps demo for bulk content re-classification");
        Ui.Blank();
        Ui.Line($"{Ui.Indent}COMMANDS");
        foreach (var (usage, what) in CommandList) Ui.Line(Entry(usage, what));
        Ui.Blank();
        Ui.Line($"{Ui.Indent}RUN FLAGS");
        foreach (var (usage, what) in RunFlags) Ui.Line(Entry(usage, what));
        Ui.Blank();
        Ui.Line($"{Ui.Indent}No Azure credentials are needed except for --live.");
        Ui.Blank();
        Ui.Footer(0, 0);
    }

    private static string Entry(string usage, string what)
    {
        var head = "    " + usage;
        var pad = DescriptionColumn - head.Length;
        return head + new string(' ', pad < 1 ? 1 : pad) + what;
    }
}
