using Microsoft.Extensions.AI;
using System.ComponentModel;

public class SupportAgent
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions _options;

    public SupportAgent(IChatClient chatClient)
    {
        _chatClient = chatClient;
        _options = new ChatOptions
        {
            Tools = [
                AIFunctionFactory.Create(RouteToBilling),
                AIFunctionFactory.Create(RouteToTechSupport),
                AIFunctionFactory.Create(RouteToRetention),
                AIFunctionFactory.Create(SearchPolicyDocs)
            ]
        };
    }

    public async Task<string> HandleRequest(string userMessage, string systemPrompt)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
        };

        var response = await _chatClient.CompleteAsync(messages, _options);
        return response.Message?.Text ?? "";
    }

    [Description("Route ticket to billing team for payment and subscription issues")]
    private static string RouteToBilling(string reason)
    {
        return $"Routed to billing team. Reason: {reason}";
    }

    [Description("Route ticket to technical support for bugs and technical issues")]
    private static string RouteToTechSupport(string reason)
    {
        return $"Routed to technical support. Reason: {reason}";
    }

    [Description("Route to retention team for cancellation requests")]
    private static string RouteToRetention(string reason)
    {
        return $"Routed to retention team. Reason: {reason}";
    }

    [Description("Search internal policy documentation")]
    private static string SearchPolicyDocs(string query)
    {
        // Simulated policy search
        if (query.ToLower().Contains("refund"))
            return "Policy: 30-day money-back guarantee for unused subscriptions.";
        return "No relevant policy found.";
    }
}