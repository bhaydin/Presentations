using Retag;

namespace Retag.Tests;

[Collection("cli")]
public class StopScopeTests
{
    private static readonly string[][] RunModes =
    [
        ["run"],
        ["run", "--lot", "tenant", "--canary", "40"],
        ["run", "--lot", "tenant", "--gate", "tenant-boundary"],
    ];

    [Theory]
    [InlineData("--job", "seo-audit")]
    [InlineData("--tool", "link-checker")]
    [InlineData("--tenant", "unrelated-council")]
    [InlineData("--job", "tool-retag-classifier")]
    [InlineData("--job", "tenant-north-shore-council")]
    public void Unrelated_flags_do_not_block_any_run_mode(string scope, string id)
    {
        Harness.Reset();
        Assert.Equal(0, Harness.Run("stop", scope, id).ExitCode);

        foreach (var mode in RunModes)
        {
            var result = Harness.Run(mode);
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("STOPPED BY FLAG", result.Output);
        }
    }

    [Theory]
    [InlineData("--job", "retag-2026")]
    [InlineData("--tool", "retag-classifier")]
    [InlineData("--tenant", "north-shore-council")]
    [InlineData("--tenant", "westgate-borough")]
    public void Matching_flags_hold_every_run_mode_and_show_the_correct_release(string scope, string id)
    {
        Harness.Reset();
        Harness.Run("stop", scope, id);

        foreach (var mode in RunModes)
        {
            var result = Harness.Run(mode);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("STOPPED BY FLAG", result.Output);
            Assert.Contains($"clear with: retag release {scope} {id}", result.Output);
            Assert.Contains("ERRORS: 0     HELD BY POLICY: 1", result.Output);
            Assert.DoesNotContain("COMMITTED", result.Output);
            Assert.DoesNotContain("scanning content", result.Output);
            Assert.DoesNotContain("batch complete", result.Output);
            Assert.All(result.Lines, line => Assert.True(line.Length <= Ui.MaxWidth));
        }

        Harness.Run("release", scope, id);
        Assert.Contains("done.", Harness.Run("run").Output);
    }

    [Fact]
    public void A_colliding_job_name_cannot_overwrite_or_release_a_tool_stop()
    {
        Harness.Reset();
        Harness.Run("stop", "--tool", "retag-classifier", "--reason", "keep stopped");
        var collision = Harness.Run("stop", "--job", "tool-retag-classifier");
        Assert.Equal(1, collision.ExitCode);

        Harness.Run("release", "--job", "tool-retag-classifier");
        var held = Harness.Run("run");
        Assert.Contains("STOPPED BY FLAG", held.Output);
        Assert.Contains("keep stopped", held.Output);
        Assert.Contains("release --tool retag-classifier", held.Output);
    }

    [Fact]
    public void Releasing_one_matching_flag_keeps_the_other_stop_in_effect()
    {
        Harness.Reset();
        Harness.Run("stop", "--job", "aaa-unrelated");
        Harness.Run("stop", "--job", "retag-2026");
        Harness.Run("stop", "--tool", "retag-classifier");

        Assert.Contains("release --job retag-2026", Harness.Run("run").Output);
        var release = Harness.Run("release", "--job", "retag-2026");
        Assert.Contains("retag-2026      STOPPED", release.Output);
        Assert.Contains("release --tool retag-classifier", Harness.Run("run").Output);

        Harness.Run("release", "--tool", "retag-classifier");
        Assert.Contains("done.", Harness.Run("run").Output);
        Assert.True(File.Exists(StopFlags.PathFor(StopScope.Job, "aaa-unrelated")));
    }
}
