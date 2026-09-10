using System.Text;
using Microsoft.Extensions.AI;
using Retag;

namespace Retag.Tests;

/// <summary>
/// Drives the real dispatcher in-process and captures stdout exactly as a
/// terminal would receive it, with colour and animation off.
/// </summary>
public static class Harness
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public sealed record Result(int ExitCode, string Output)
    {
        public IEnumerable<string> Lines =>
            Output.Split('\n').Select(l => l.TrimEnd('\r'));
    }

    public static Result Run(params string[] args) => Capture(() => Cli.RunAsync(args));

    public static Result RunLive(IChatClient chat, params string[] args) => Capture(() =>
        Cli.RunAsync(args, new Config("https://test.invalid", "test-deployment", null),
            _ => new LiveAgent(chat, "test-deployment")));

    private static Result Capture(Func<Task<int>> run)
    {
        Gate.Wait();
        var original = Console.Out;
        try
        {
            Ui.UseColour = false;
            Ui.Animate = false;

            var buffer = new StringWriter { NewLine = "\n" };
            Console.SetOut(buffer);

            var exit = run().GetAwaiter().GetResult();
            return new Result(exit, buffer.ToString());
        }
        finally
        {
            Console.SetOut(original);
            Gate.Release();
        }
    }

    /// <summary>Restores the green baseline so a test never depends on what ran before it.</summary>
    public static void Reset() => Run("reset");

    /// <summary>Every command in the demo, in the order the run-of-show uses them.</summary>
    public static readonly string[][] AllCommands =
    [
        ["--help"],
        ["seed"],
        ["run", "--taxonomy", "consolidated-2026", "--workers", "12"],
        ["propose", "--item", Fixtures.SlideItemId],
        ["trace", "show", "--item", Fixtures.SlideItemId, "--attrs"],
        ["traces", "group-by", "taxonomy.version"],
        ["replay", "--from", "replay-v41.txt", "--taxonomy", "42"],
        ["run", "--lot", "tenant", "--canary", "40", "--release", "18"],
        ["run", "--lot", "tenant", "--canary", "40", "--release", "17"],
        ["run", "--lot", "tenant", "--gate", "tenant-boundary"],
        ["stop", "--job", "retag-2026", "--reason", "argued cases red"],
        ["release", "--job", "retag-2026"],
        ["reset"],
    ];

    public sealed record Step(string[] Args, Result Result);

    /// <summary>
    /// Resets to the green baseline, then runs every demo command in order.
    /// State carries between them on purpose: group-by writes the file replay
    /// reads, stop writes the flag release clears.
    /// </summary>
    public static List<Step> RunSequence()
    {
        Run("reset");
        var steps = AllCommands.Select(args => new Step(args, Run(args))).ToList();
        Run("reset");
        return steps;
    }

    /// <summary>A stable file-safe name for a command, used for golden files.</summary>
    public static string GoldenName(string[] args)
    {
        var cleaned = args.Select(a => a.Replace("--", "").Replace("/", "-").Replace(" ", "-"));
        return string.Join("_", cleaned).Replace(".", "-");
    }

    public static string GoldenPath(string[] args) =>
        Path.Combine(Paths.Root, "tests", "golden", GoldenName(args) + ".txt");
}
