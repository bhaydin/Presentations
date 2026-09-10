using Retag;

namespace Retag.Tests;

[Collection("cli")]
public class ReplayValidationTests
{
    private const string InputFile = "review-replay.txt";
    private const string PreviousOutput = "previous compensation set\n";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Unknown_ids_fail_before_creating_or_overwriting_output(bool mixed, bool existingOutput)
    {
        Harness.Reset();
        var input = mixed ? Fixtures.SlideItemId + "\n" : "";
        // The diagnostic should identify the line without overflowing the terminal.
        input += "north-shore-council/" + new string('x', 100) + "\n";
        File.WriteAllText(Paths.Output(InputFile), input);
        if (existingOutput) File.WriteAllText(Paths.Output(Commands.CompensateFile), PreviousOutput);

        var result = Harness.Run("replay", "--from", InputFile, "--taxonomy", "42");

        AssertInvalid(result);
        Assert.Contains($"line {(mixed ? 2 : 1)}", result.Output);
        Assert.DoesNotContain("proposals differ", result.Output);
        if (existingOutput) Assert.Equal(PreviousOutput, File.ReadAllText(Paths.Output(Commands.CompensateFile)));
        else Assert.False(File.Exists(Paths.Output(Commands.CompensateFile)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Each_id_must_have_both_content_and_a_recorded_decision(bool removeContent)
    {
        Harness.Reset();
        File.WriteAllText(Paths.Output(InputFile), Fixtures.SlideItemId + "\n");
        File.WriteAllText(Paths.Output(Commands.CompensateFile), PreviousOutput);
        try
        {
            if (removeContent) Fixtures.ItemsById.Remove(Fixtures.SlideItemId);
            else Seeder.Load().RemoveAll(d => d.ItemId == Fixtures.SlideItemId);

            AssertInvalid(Harness.Run("replay", "--from", InputFile, "--taxonomy", "42"));
            Assert.Equal(PreviousOutput, File.ReadAllText(Paths.Output(Commands.CompensateFile)));
        }
        finally
        {
            Fixtures.Invalidate();
            Seeder.Invalidate();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("42.5")]
    public void Invalid_taxonomy_arguments_fail_without_overwriting_output(string? version)
    {
        Harness.Reset();
        File.WriteAllText(Paths.Output(InputFile), Fixtures.SlideItemId + "\n");
        File.WriteAllText(Paths.Output(Commands.CompensateFile), PreviousOutput);
        var args = new List<string> { "replay", "--taxonomy" };
        if (version is not null) args.Add(version);
        args.AddRange(["--from", InputFile]);

        var result = Harness.Run(args.ToArray());

        AssertInvalid(result);
        Assert.Contains("41 or 42", result.Output);
        Assert.Equal(PreviousOutput, File.ReadAllText(Paths.Output(Commands.CompensateFile)));
    }

    [Theory]
    [InlineData(null, 42, 380)]
    [InlineData("41", 41, 0)]
    [InlineData("42", 42, 380)]
    public void Supported_versions_and_the_omitted_default_replay_the_complete_corpus(
        string? version, int expectedVersion, int expectedDifferences)
    {
        Harness.Reset();
        Harness.Run("traces", "group-by", "taxonomy.version");
        var args = new List<string> { "replay", "--from", Commands.ReplayFile };
        if (version is not null) args.AddRange(["--taxonomy", version]);

        var result = Harness.Run(args.ToArray());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"replaying 3,082 decisions under v{expectedVersion}", result.Output);
        Assert.Contains(Ui.N(3082 - expectedDifferences), result.Output);
        Assert.Equal(expectedDifferences, File.ReadLines(Paths.Output(Commands.CompensateFile)).Count(l => l.Length > 0));
        Assert.Contains("ERRORS: 0     HELD BY POLICY: 0", result.Output);
    }

    [Fact]
    public void Classifier_and_fixture_lookups_reject_unsupported_versions()
    {
        Harness.Reset();
        var emergency = Fixtures.Items.First(i => i.EmergencyPreparedness);
        var ordinary = Fixtures.Items.First(i => !i.EmergencyPreparedness);
        foreach (var version in new[] { 40, 43, 999 })
        {
            Assert.Throws<InvalidDataException>(() => Classifier.Choose(emergency, version));
            Assert.Throws<InvalidDataException>(() => Classifier.Choose(ordinary, version));
            Assert.Throws<InvalidDataException>(() => Fixtures.ForVersion(version));
            Assert.Throws<InvalidDataException>(() => Fixtures.BuildTaxonomy(version));
        }
    }

    [Fact]
    public void Missing_replay_file_preserves_previous_output()
    {
        Harness.Reset();
        File.WriteAllText(Paths.Output(Commands.CompensateFile), PreviousOutput);
        var result = Harness.Run("replay", "--from", "missing.txt");
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(PreviousOutput, File.ReadAllText(Paths.Output(Commands.CompensateFile)));
    }

    private static void AssertInvalid(Harness.Result result)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ERRORS: 1     HELD BY POLICY: 0", result.Output);
        Assert.DoesNotContain("done", result.Output);
        Assert.All(result.Lines, line => Assert.True(line.Length <= Ui.MaxWidth));
    }
}
