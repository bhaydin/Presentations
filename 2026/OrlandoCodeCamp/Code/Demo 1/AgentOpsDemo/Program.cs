using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

// Configuration
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
    ?? throw new Exception("Set AZURE_OPENAI_ENDPOINT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")     ?? throw new Exception("Set AZURE_OPENAI_KEY");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4";

// Create client
var azureClient = new AzureOpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
var chatClient = azureClient.AsChatClient(deploymentName);


// System prompt (baseline version)
var systemPromptBaseline = @"You are a helpful customer support agent. 
Route tickets to the appropriate team using available tools.
Be concise and professional.";

Console.WriteLine("=== BASELINE EVALUATION ===\n");

// Run baseline evaluation
var agent = new SupportAgent(chatClient);
var evaluator = new AgentEvaluator(agent, chatClient);
var baselineResults = await evaluator.RunEvaluation(systemPromptBaseline, "golden-prompts.json");
baselineResults.PrintReport();

// Now make a "harmless" change
var systemPromptModified = @"You are a super helpful and friendly customer support agent! 
Always be enthusiastic and offer to help with anything the customer needs.
Route tickets to the appropriate team using available tools.
Be warm, engaging, and go the extra mile!";

Console.WriteLine("\n\n=== MODIFIED EVALUATION (After 'Improvement') ===\n");

// Run modified evaluation
var modifiedResults = await evaluator.RunEvaluation(systemPromptModified, "golden-prompts.json");
modifiedResults.PrintReport();

// CI/CD simulation
if (!modifiedResults.AllPassed)
{
    Console.WriteLine("\n❌ BUILD FAILED: Agent regression detected!");
    Console.WriteLine("The agent would NOT be deployed to production.");
    Environment.Exit(1);
}
else
{
    Console.WriteLine("\n✅ BUILD PASSED: Agent meets all quality thresholds.");
    Environment.Exit(0);
}