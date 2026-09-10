using Retag;

namespace Retag.Tests;

/// <summary>
/// These exist because the numbers appear on slides and a mistake is visible to
/// the room.
/// </summary>
[Collection("cli")]
public class ArithmeticTests
{
    [Fact]
    public void Version_split_sums_to_the_corpus()
    {
        Assert.Equal(Schedule.TotalItems, Schedule.V41Decisions + Schedule.V42Decisions);
        Assert.Equal(4182, Schedule.V41Decisions + Schedule.V42Decisions);
        Assert.Equal(3082, Schedule.V41Decisions);
        Assert.Equal(1100, Schedule.V42Decisions);
    }

    [Fact]
    public void Replay_split_sums_to_the_v41_decisions()
    {
        Assert.Equal(Schedule.V41Decisions, Schedule.IdenticalUnderV42 + Schedule.DivergentUnderV42);
        Assert.Equal(3082, 2702 + 380);
        Assert.Equal(2702, Schedule.IdenticalUnderV42);
        Assert.Equal(380, Schedule.DivergentUnderV42);
    }

    [Fact]
    public void Seeded_corpus_holds_every_numeric_invariant()
    {
        Harness.Reset();

        var decisions = Seeder.Load();
        var items = Fixtures.ItemsById;

        Assert.Equal(4182, decisions.Count);
        Assert.Equal(3082, decisions.Count(d => d.TaxonomyVersion == 41));
        Assert.Equal(1100, decisions.Count(d => d.TaxonomyVersion == 42));

        var divergent = decisions
            .Where(d => d.TaxonomyVersion == 41)
            .Count(d => Classifier.WouldDiffer(d, items[d.ItemId], 42));

        Assert.Equal(380, divergent);
        Assert.Equal(2702, 3082 - divergent);
    }

    [Fact]
    public void Timestamps_span_the_stated_window_and_interleave()
    {
        Harness.Reset();
        var decisions = Seeder.Load();

        var stamps = decisions.Select(d => Seeder.Parse(d.Timestamp)).ToList();
        Assert.Equal(Schedule.Start, stamps.Min());
        Assert.Equal(Schedule.End, stamps.Max());

        var v42 = decisions.Where(d => d.TaxonomyVersion == 42).Select(d => Seeder.Parse(d.Timestamp)).ToList();
        var v41 = decisions.Where(d => d.TaxonomyVersion == 41).Select(d => Seeder.Parse(d.Timestamp)).ToList();

        // v42 first appears exactly at the republish.
        Assert.Equal(Schedule.Cutover, v42.Min());

        // Not a clean cut: v41 decisions keep landing after v42 has started, so
        // modification time alone cannot separate the two versions.
        Assert.True(v41.Max() > v42.Min(), "expected v41 decisions later than the first v42 decision");
        Assert.True(v41.Count(t => t > Schedule.Cutover) > 0, "expected v41 decisions after the cutover");
    }

    [Fact]
    public void Taxonomy_versions_share_every_identifier_and_differ_only_in_guidance()
    {
        Harness.Reset();

        var v41 = Fixtures.V41.Terms;
        var v42 = Fixtures.V42.Terms;

        Assert.Equal(312, v41.Count);
        Assert.Equal(312, v42.Count);
        Assert.Equal(v41.Select(t => t.Id), v42.Select(t => t.Id));
        Assert.Equal(v41.Select(t => t.Parent), v42.Select(t => t.Parent));

        // Nothing was added, removed or renamed. That is why nothing threw.
        var changed = v41.Zip(v42)
            .Where(pair => pair.First.Guidance != pair.Second.Guidance)
            .Select(pair => pair.First.Id)
            .ToList();

        Assert.Equal(["community-events", "public-safety"], changed.Order());
    }

    [Fact]
    public void Named_slide_item_exists_and_sits_on_the_superseded_side()
    {
        Harness.Reset();

        Assert.True(Fixtures.ItemsById.ContainsKey(Fixtures.SlideItemId));

        var decision = Seeder.Load().Single(d => d.ItemId == Fixtures.SlideItemId);
        Assert.Equal(41, decision.TaxonomyVersion);
        Assert.Equal("community-events", decision.Chosen[0]);
    }

    [Fact]
    public void Canary_categories_sum_to_twenty_and_score_as_scripted()
    {
        Harness.Reset();
        var cases = Fixtures.KnownAnswerCases;

        Assert.Equal(20, cases.Count);
        Assert.Equal(6, cases.Count(c => c.Category == "OBVIOUS"));
        Assert.Equal(6, cases.Count(c => c.Category == "HUMAN-VERIFIED"));
        Assert.Equal(8, cases.Count(c => c.Category == "ARGUED"));

        Assert.Equal(20, cases.Count(c => KnownAnswers.Passes(c, 17)));
        Assert.Equal(14, cases.Count(c => KnownAnswers.Passes(c, 18)));
        Assert.Equal(2, cases.Count(c => c.Category == "ARGUED" && KnownAnswers.Passes(c, 18)));
    }

    [Fact]
    public void Every_release_18_failure_chose_the_parent_term()
    {
        Harness.Reset();

        var failures = Fixtures.KnownAnswerCases.Where(c => !KnownAnswers.Passes(c, 18)).ToList();

        Assert.Equal(6, failures.Count);
        Assert.All(failures, c => Assert.Equal(c.ParentTerm, KnownAnswers.Answer(c, 18)));
        Assert.All(failures, c => Assert.NotNull(c.ParentTerm));
    }

    [Fact]
    public void Committed_counts_for_the_first_three_tenants_match_the_corpus()
    {
        Harness.Reset();
        var tenants = Fixtures.Tenants;

        Assert.Equal(428, tenants[0].ItemCount);
        Assert.Equal(356, tenants[1].ItemCount);
        Assert.Equal(511, tenants[2].ItemCount);
        Assert.Equal(4182, tenants.Sum(t => t.ItemCount));
        Assert.Equal(12, tenants.Count);

        // And the same numbers appear on screen.
        var output = Harness.Run("run", "--lot", "tenant", "--canary", "40", "--release", "18").Output;
        Assert.Contains("COMMITTED  428 items", output);
        Assert.Contains("COMMITTED  356 items", output);
        Assert.Contains("COMMITTED  511 items", output);
    }
}
