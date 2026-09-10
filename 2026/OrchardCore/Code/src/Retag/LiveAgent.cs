using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace Retag;

/// <summary>
/// The only part of this demo that is not a fixture. The content store, the
/// taxonomy and the 4,182-decision corpus are all generated. The judgment here
/// is a real model call.
/// </summary>
public sealed class LiveAgent
{
    private readonly IChatClient _chat;

    public string Deployment { get; }

    private LiveAgent(IChatClient chat, string deployment)
    {
        _chat = chat;
        Deployment = deployment;
    }

    public static LiveAgent Create(Config config)
    {
        if (!config.IsConfigured)
            throw new InvalidOperationException(
                "AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT are required for --live.");

        var endpoint = new Uri(config.Endpoint!);
        var client = string.IsNullOrWhiteSpace(config.ApiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(config.ApiKey!));

        var chat = client.GetChatClient(config.Deployment!).AsIChatClient();
        return new LiveAgent(chat, config.Deployment!);
    }

    /// <summary>
    /// Classify one item against a shortlist of candidate terms. The taxonomy
    /// guidance goes in the prompt, which is exactly why the guidance change
    /// moved the answer without anything erroring.
    /// </summary>
    public async Task<string> ClassifyAsync(
        string title, string body, IReadOnlyList<string> candidates, Taxonomy taxonomy, int release)
    {
        var guidance = string.Join("\n", candidates
            .Where(taxonomy.Has)
            .Select(id => $"- {id}: {taxonomy[id].Guidance}"));

        var releaseRules = release >= 18
            ? "\n" + KnownAnswers.Release18Instruction
            : "";

        var system =
            "You classify municipal content into a controlled taxonomy. "
            + "Reply with exactly one term id from the candidate list and nothing else."
            + releaseRules;

        var user =
            $"Title: {title}\n\nBody: {body}\n\nCandidate terms:\n{guidance}\n\nTerm id:";

        var response = await _chat.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, system),
                new ChatMessage(ChatRole.User, user),
            ],
            new ChatOptions { Temperature = 0f, MaxOutputTokens = 32 });

        return Normalise(response.Text, candidates);
    }

    /// <summary>Models add punctuation and prose. Pull the term id back out.</summary>
    public static string Normalise(string? text, IReadOnlyList<string> candidates)
    {
        var cleaned = (text ?? "").Trim().Trim('.', ',', '"', '\'', '`').Trim();

        foreach (var candidate in candidates)
            if (cleaned.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                return candidate;

        foreach (var candidate in candidates)
            if (cleaned.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return cleaned.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    }
}
