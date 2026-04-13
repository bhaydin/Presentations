using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.Diagnostics;

Console.WriteLine(@"
╔════════════════════════════════════════════════════════════════╗
║  Demo 2: Follow the Tool Call (Trace Inspection)              ║
║  The final answer is the receipt. The trace is the footage.   ║
╚════════════════════════════════════════════════════════════════╝
");

// Configuration
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") 
    ?? throw new Exception("Set AZURE_OPENAI_ENDPOINT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY") 
    ?? throw new Exception("Set AZURE_OPENAI_KEY");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4";

// Create client
var azureClient = new AzureOpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(apiKey));
var chatClient = azureClient.AsChatClient(deploymentName);

// Create traced agent
using var agent = new TracedAgent(chatClient, useConsoleExporter: true);

// Demo question
var question = "What are the best practices for implementing Azure Functions?";

Console.WriteLine("Press ENTER to run the agent and see the trace...");
Console.ReadLine();

// Run agent
var stopwatch = Stopwatch.StartNew();
var answer = await agent.AskQuestion(question);
stopwatch.Stop();

// Summary
Console.WriteLine("\n📊 TRACE SUMMARY:");
Console.WriteLine($"   Total Time: {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"   Response Length: {answer.Length} characters");

Console.WriteLine("\n💡 KEY INSIGHTS:");
Console.WriteLine("   • The final answer looks polished");
Console.WriteLine("   • But the trace shows exactly what happened:");
Console.WriteLine("     - Which tool was called");
Console.WriteLine("     - What parameters were passed");
Console.WriteLine("     - How long each step took");
Console.WriteLine("     - Token consumption per phase");

Console.WriteLine("\n🎯 WHY THIS MATTERS:");
Console.WriteLine("   • Debugging: See where failures occur");
Console.WriteLine("   • Performance: Identify slow steps");
Console.WriteLine("   • Cost: Track token usage per operation");
Console.WriteLine("   • Replay: Re-run with same inputs to test fixes");

Console.WriteLine("\n\nPress ENTER to show a simulated failure trace...");
Console.ReadLine();

// Simulate a failure scenario
Console.WriteLine($"\n{new string('=', 70)}");
Console.WriteLine("🔴 SIMULATED FAILURE SCENARIO");
Console.WriteLine(new string('=', 70));
Console.WriteLine("\nLet's see what a trace looks like when things go wrong...\n");

ShowFailureTrace();

Console.WriteLine("\n\n✅ Demo complete! The trace gives you security camera footage of agent behavior.");

// Helper to show what a failure trace looks like
static void ShowFailureTrace()
{
    Console.WriteLine("📊 TRACE WATERFALL (Failed Run):\n");
    Console.WriteLine($"{"Span",-25} {"Duration",-12} {"Status",-10}");
    Console.WriteLine(new string('-', 70));
    
    Console.WriteLine($"{"User Input",-25} {"12.3ms",-12} {"✓ Success",-10}");
    Console.WriteLine($"{"Orchestration",-25} {"45.1ms",-12} {"✓ Success",-10}");
    Console.WriteLine($"{"Model Call",-25} {"892.4ms",-12} {"✓ Success",-10}");
    Console.WriteLine($"{"Tool: SearchDocs",-25} {"2341.2ms",-12} {"\x1b[31m✗ Timeout\x1b[0m",-10}");
    Console.WriteLine($"  └─ Error: Request timeout after 2000ms");
    Console.WriteLine($"{"Fallback Response",-25} {"156.7ms",-12} {"✓ Success",-10}");
    Console.WriteLine($"{"Final Response",-25} {"23.1ms",-12} {"✓ Success",-10}");
    
    Console.WriteLine(new string('-', 70));
    Console.WriteLine($"Total Duration: 3470.8ms | Status: Degraded\n");
    
    Console.WriteLine("🔍 TRACE SHOWS:");
    Console.WriteLine("   • Tool call failed at step 4");
    Console.WriteLine("   • Failure reason: Request timeout");
    Console.WriteLine("   • Agent gracefully fell back");
    Console.WriteLine("   • User got an answer, but we know it's incomplete");
}