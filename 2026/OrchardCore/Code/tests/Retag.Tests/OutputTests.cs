using Retag;

namespace Retag.Tests;

/// <summary>
/// The demo commands are checked as a sequence, not in isolation, because that
/// is how they are run: group-by writes the file replay reads, and stop writes
/// the flag release clears. Isolating them would test something nobody does.
/// </summary>
[Collection("cli")]
public class SequenceTests
{
    [Fact]
    public void Sequence_output_matches_the_committed_golden_files()
    {
        var update = Environment.GetEnvironmentVariable("RETAG_UPDATE_GOLDEN") == "1";

        foreach (var (args, result) in Harness.RunSequence())
        {
            var path = Harness.GoldenPath(args);

            if (update)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, result.Output);
                continue;
            }

            Assert.True(File.Exists(path), $"missing golden file: {path}");
            Assert.Equal(
                File.ReadAllText(path).ReplaceLineEndings("\n"),
                result.Output.ReplaceLineEndings("\n"));
        }
    }

    [Fact]
    public void Running_the_sequence_twice_produces_identical_output()
    {
        var first = Harness.RunSequence();
        var second = Harness.RunSequence();

        Assert.Equal(first.Count, second.Count);

        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Result.Output, second[i].Result.Output);
            Assert.Equal(first[i].Result.ExitCode, second[i].Result.ExitCode);
        }
    }

    [Fact]
    public void Seeding_twice_produces_a_byte_identical_corpus()
    {
        Harness.Run("seed");
        var first = File.ReadAllBytes(Paths.Fixture("decisions.json"));

        Harness.Run("seed");
        var second = File.ReadAllBytes(Paths.Fixture("decisions.json"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void No_line_of_any_command_exceeds_seventy_two_columns()
    {
        foreach (var (args, result) in Harness.RunSequence())
        foreach (var line in result.Lines)
        {
            Assert.True(
                line.Length <= Ui.MaxWidth,
                $"{Harness.GoldenName(args)}: {line.Length} columns > {Ui.MaxWidth}\n{line}");
        }
    }

    [Fact]
    public void Every_command_prints_both_counters()
    {
        foreach (var (args, result) in Harness.RunSequence())
        {
            Assert.True(result.Output.Contains("ERRORS:"), $"{Harness.GoldenName(args)}: no ERRORS counter");
            Assert.True(
                result.Output.Contains("HELD BY POLICY:"),
                $"{Harness.GoldenName(args)}: no HELD BY POLICY counter");
        }
    }

    [Fact]
    public void Every_command_except_live_runs_with_no_credentials_configured()
    {
        var keys = new[]
        {
            "AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_DEPLOYMENT",
            "AZURE_OPENAI_API_KEY", "AZURE_OPENAI_KEY",
        };
        var saved = keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);

        try
        {
            foreach (var key in keys) Environment.SetEnvironmentVariable(key, null);

            foreach (var (args, result) in Harness.RunSequence())
            {
                Assert.True(
                    result.ExitCode == 0,
                    $"{Harness.GoldenName(args)} exited {result.ExitCode} without credentials");
            }
        }
        finally
        {
            foreach (var (key, value) in saved) Environment.SetEnvironmentVariable(key, value);
        }
    }
}

[Collection("cli")]
public class FormattingTests
{
    [Fact]
    public void Counters_are_printed_even_when_both_are_zero()
    {
        Harness.Reset();
        var output = Harness.Run("run", "--taxonomy", "consolidated-2026", "--workers", "12").Output;

        Assert.Contains("ERRORS: 0     HELD BY POLICY: 0", output);
    }

    [Fact]
    public void Colour_never_participates_in_layout()
    {
        // The width helper measures the plain text, so an amber line and a white
        // line of the same content occupy the same number of columns.
        var plain = RetagTracing.Attribute("taxonomy.version", "41", highlight: false);

        Ui.UseColour = true;
        var amber = RetagTracing.Attribute("taxonomy.version", "41", highlight: true);
        Ui.UseColour = false;

        Assert.NotEqual(plain, amber);
        Assert.Equal(plain.Length, Ui.Width(amber));
        Assert.Equal(plain, Ui.Plain(amber));
    }

    [Fact]
    public void The_trace_tree_fits_even_for_the_longest_item_id()
    {
        Harness.Reset();
        var longest = Fixtures.Items.OrderByDescending(i => i.Id.Length).First();

        var output = Harness.Run("trace", "show", "--item", longest.Id, "--attrs");

        Assert.All(output.Lines, line => Assert.True(
            line.Length <= Ui.MaxWidth,
            $"{line.Length} columns for {longest.Id}\n{line}"));
    }
}

[Collection("cli")]
public class BehaviourTests
{
    [Fact]
    public void A_held_lot_commits_zero_items_and_exits_zero()
    {
        Harness.Reset();
        var result = Harness.Run("run", "--lot", "tenant", "--canary", "40", "--release", "18");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CANARY HELD. Proposals remain drafts.", result.Output);
        Assert.Contains("0 items committed for this tenant.", result.Output);
        Assert.Contains("HELD BY POLICY: 1", result.Output);

        // The held tenant is the fourth, and it never appears as COMMITTED.
        Assert.DoesNotContain("tenant 4 .... canary 40 · 20/20 · COMMITTED", result.Output);
        Assert.Equal(3, result.Lines.Count(l => l.Contains("COMMITTED")));
    }

    [Fact]
    public void A_policy_hold_exits_zero_because_a_hold_is_not_an_error()
    {
        Harness.Reset();
        var result = Harness.Run("run", "--lot", "tenant", "--gate", "tenant-boundary");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[HELD BY POLICY]", result.Output);
        Assert.Contains("ERRORS: 0  |  HELD BY POLICY: 1", result.Output);
    }

    [Fact]
    public void Run_refuses_to_start_while_a_stop_flag_exists()
    {
        Harness.Reset();
        Harness.Run("stop", "--job", "retag-2026", "--reason", "argued cases red");

        var refused = Harness.Run("run", "--taxonomy", "consolidated-2026", "--workers", "12");

        Assert.Contains("STOPPED BY FLAG", refused.Output);
        Assert.DoesNotContain("done.", refused.Output);
        Assert.Contains("HELD BY POLICY: 1", refused.Output);

        Harness.Run("release", "--job", "retag-2026");
        Assert.Contains("done.", Harness.Run("run", "--taxonomy", "consolidated-2026").Output);
    }

    [Theory]
    [InlineData("--tool", "retag-classifier")]
    [InlineData("--job", "retag-2026")]
    [InlineData("--tenant", "north-shore-council")]
    public void Every_stop_level_blocks_a_run_and_is_cleared_by_release(string scope, string id)
    {
        Harness.Reset();
        Harness.Run("stop", scope, id, "--reason", "test");

        Assert.Contains("STOPPED BY FLAG", Harness.Run("run").Output);

        Harness.Run("release", scope, id);
        Assert.Contains("done.", Harness.Run("run").Output);
    }

    [Fact]
    public void Replay_writes_exactly_the_compensation_set()
    {
        Harness.Reset();
        Harness.Run("traces", "group-by", "taxonomy.version");
        Harness.Run("replay", "--from", "replay-v41.txt", "--taxonomy", "42");

        var replay = File.ReadAllLines(Paths.Output(Commands.ReplayFile)).Where(l => l.Length > 0).ToList();
        var compensate = File.ReadAllLines(Paths.Output(Commands.CompensateFile)).Where(l => l.Length > 0).ToList();

        Assert.Equal(3082, replay.Count);
        Assert.Equal(380, compensate.Count);

        // Every item needing compensation is emergency-preparedness content that
        // v41 guidance filed under community-events.
        Assert.All(compensate, id => Assert.True(Fixtures.ItemsById[id].EmergencyPreparedness));
    }

    [Fact]
    public void Reset_is_idempotent_and_restores_the_baseline()
    {
        Harness.Run("stop", "--job", "retag-2026", "--reason", "x");
        Harness.Run("traces", "group-by", "taxonomy.version");

        var first = Harness.Run("reset");
        var second = Harness.Run("reset");

        Assert.Equal(first.Output, second.Output);
        Assert.Equal(0, first.ExitCode);
        Assert.False(File.Exists(Paths.Output(Commands.ReplayFile)));
        Assert.Null(StopFlags.Active(Commands.DefaultJobId, Commands.ClassifierToolId,
            Fixtures.Tenants.Select(t => t.Id)));
    }
}

/// <summary>Commands share fixture files on disk, so they run one at a time.</summary>
[CollectionDefinition("cli", DisableParallelization = true)]
public class CliCollection;
