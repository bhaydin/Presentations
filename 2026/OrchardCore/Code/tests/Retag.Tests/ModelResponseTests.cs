using Retag;

namespace Retag.Tests;

public class ModelResponseTests
{
    private static readonly string[] Candidates = ["emergency-accommodation", "housing-support"];

    [Theory]
    [InlineData("emergency-accommodation")]
    [InlineData("  EMERGENCY-ACCOMMODATION\n")]
    [InlineData("\"emergency-accommodation\".")]
    [InlineData("`emergency-accommodation`")]
    [InlineData("'emergency-accommodation',")]
    public void Exact_candidate_ids_allow_harmless_formatting(string reply)
    {
        Assert.Equal("emergency-accommodation", LiveAgent.Normalise(reply, Candidates));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n ")]
    [InlineData("unlisted-term")]
    [InlineData("housing-support (not emergency-accommodation)")]
    [InlineData("emergency-accommodation, housing-support")]
    [InlineData("emergency-accommodation\nhousing-support")]
    [InlineData("not emergency-accommodation")]
    [InlineData("Choose emergency-accommodation.")]
    [InlineData("emergency-accommodation-extra")]
    [InlineData(".emergency-accommodation")]
    public void Anything_other_than_one_candidate_is_invalid(string? reply)
    {
        Assert.Null(LiveAgent.Normalise(reply, Candidates));
    }
}
