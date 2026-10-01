using Microsoft.Extensions.AI;
using Retag;

namespace Retag.Tests;

[Collection("cli")]
public class LiveValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("unlisted-term")]
    [InlineData("community-events, public-safety")]
    [InlineData("public-safety (not community-events)")]
    public void Invalid_live_proposal_reports_an_error_without_emitting_a_decision(string reply)
    {
        Harness.Reset();
        using var chat = new FakeChatClient(_ => reply);
        var result = Harness.RunLive(chat, "propose", "--item", Fixtures.SlideItemId, "--live");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(1, chat.Calls);
        Assert.Contains("invalid model response", result.Output);
        Assert.Contains("ERRORS: 1     HELD BY POLICY: 0", result.Output);
        Assert.DoesNotContain("decision.chosen", result.Output);
        Assert.All(result.Lines, line => Assert.True(line.Length <= Ui.MaxWidth));
    }

    [Fact]
    public void Valid_live_proposal_emits_the_validated_candidate()
    {
        Harness.Reset();
        using var chat = new FakeChatClient(_ => " `COMMUNITY-EVENTS`. ");
        var result = Harness.RunLive(chat, "propose", "--item", Fixtures.SlideItemId, "--live");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, chat.Calls);
        Assert.Contains("live model call", result.Output);
        Assert.Contains("decision.chosen", result.Output);
        Assert.Contains("community-events", result.Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unlisted-term")]
    [InlineData("housing-support (not emergency-accommodation)")]
    public void Invalid_canary_response_is_a_failed_case_and_holds_the_lot(string reply)
    {
        Harness.Reset();
        using var chat = new FakeChatClient(i =>
        {
            var expected = Fixtures.KnownAnswerCases[i].ExpectedTerm;
            return expected == "emergency-accommodation" ? reply : expected;
        });
        var result = RunCanary(chat);

        AssertHeld(result);
        Assert.Equal(20, chat.Calls);
        Assert.Contains("19/20", result.Output);
        Assert.Contains("1 invalid model response(s)", result.Output);
        Assert.DoesNotContain("using seeded canary", result.Output);
    }

    [Fact]
    public void A_later_transport_failure_does_not_erase_an_invalid_response()
    {
        Harness.Reset();
        using var chat = new FakeChatClient(i => i == 0 ? "unlisted-term" : throw new HttpRequestException());
        var result = RunCanary(chat);

        AssertHeld(result);
        Assert.Equal(2, chat.Calls);
        Assert.Contains("19/20", result.Output);
        Assert.Contains("1 invalid model response(s)", result.Output);
        Assert.Contains("live call unavailable - using seeded canary", result.Output);
    }

    [Fact]
    public void Transport_fallback_is_announced_once_and_does_not_disable_the_next_run()
    {
        Harness.Reset();
        using var unavailable = new FakeChatClient(_ => throw new HttpRequestException());
        var fallback = RunCanary(unavailable);
        Assert.Equal(0, fallback.ExitCode);
        Assert.Equal(1, unavailable.Calls);
        Assert.Single(fallback.Lines, l => l.Contains("using seeded canary"));
        Assert.Equal(12, fallback.Lines.Count(l => l.Contains("COMMITTED")));

        using var working = new FakeChatClient(i => Fixtures.KnownAnswerCases[i % 20].ExpectedTerm);
        var live = RunCanary(working);
        Assert.Equal(0, live.ExitCode);
        Assert.Equal(240, working.Calls);
        Assert.Equal(12, live.Lines.Count(l => l.Contains("COMMITTED")));
        Assert.DoesNotContain("using seeded canary", live.Output);
    }

    private static Harness.Result RunCanary(IChatClient chat) =>
        Harness.RunLive(chat, "run", "--lot", "tenant", "--canary", "40", "--release", "17", "--live");

    private static void AssertHeld(Harness.Result result)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CANARY HELD", result.Output);
        Assert.Contains("0 items committed for this tenant.", result.Output);
        Assert.Contains("ERRORS: 0     HELD BY POLICY: 1", result.Output);
        Assert.DoesNotContain("COMMITTED", result.Output);
        Assert.All(result.Lines, line => Assert.True(line.Length <= Ui.MaxWidth));
    }

    private sealed class FakeChatClient(Func<int, string?> reply) : IChatClient
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply(Calls++))));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
