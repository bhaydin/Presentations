# Demo 2 Setup Guide: Follow the Tool Call (Trace Inspection)

## Quick Setup (10-15 minutes)

### Prerequisites
- .NET 8 SDK installed
- Visual Studio Code or Visual Studio
- Azure OpenAI access OR OpenAI API key
- Docker Desktop (for Aspire Dashboard) OR use console logging

---

## Step 1: Create the Project (2 minutes)

```bash
# Create new console app
dotnet new console -n AgentTraceDemo
cd AgentTraceDemo

# Add required packages
dotnet add package Microsoft.Extensions.AI
dotnet add package Microsoft.Extensions.AI.OpenAI
dotnet add package Azure.AI.OpenAI
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package Microsoft.Extensions.Logging
dotnet add package Microsoft.Extensions.Logging.Console
```

---

## Step 2: Create MCP Tool Integration (4 minutes)

Create `LearnMcpTool.cs` (simulated Microsoft Learn search):

```csharp
using System.ComponentModel;
using System.Text.Json;

public class LearnMcpTool
{
    // Simulated Microsoft Learn documentation search
    [Description("Search Microsoft Learn documentation for Azure best practices and guidance")]
    public static string SearchLearnDocs(
        [Description("The search query for Azure documentation")] string query)
    {
        Console.WriteLine($"  🔧 Tool Called: SearchLearnDocs");
        Console.WriteLine($"  📝 Query: {query}");
        
        // Simulate API latency
        Thread.Sleep(230);

        // Simulated search results based on query
        var results = query.ToLower() switch
        {
            var q when q.Contains("function") || q.Contains("serverless") => 
                @"Azure Functions Best Practices:
                
1. Use consumption plan for variable workloads
2. Implement proper error handling with try-catch blocks
3. Use durable functions for stateful workflows
4. Enable Application Insights for monitoring
5. Keep functions small and focused (single responsibility)
6. Use managed identities for authentication
7. Implement retry policies for transient failures
8. Monitor cold starts and optimize startup time",

            var q when q.Contains("storage") || q.Contains("blob") =>
                @"Azure Blob Storage Best Practices:
                
1. Use hot/cool/archive tiers based on access patterns
2. Enable versioning for data protection
3. Implement lifecycle management policies
4. Use private endpoints for security
5. Enable soft delete for accidental deletion protection",

            var q when q.Contains("ai") || q.Contains("openai") =>
                @"Azure OpenAI Best Practices:
                
1. Implement content filtering for safety
2. Use managed identity for authentication
3. Monitor token consumption and costs
4. Implement retry logic with exponential backoff
5. Cache responses when appropriate
6. Use prompt engineering for better results",

            _ => $"Found general documentation for: {query}\n\nRefer to docs.microsoft.com/azure for detailed guidance."
        };

        Console.WriteLine($"  ✅ Tool Result: {results.Length} characters returned");
        return results;
    }

    [Description("Get code examples from Microsoft Learn documentation")]
    public static string GetCodeExample(
        [Description("The technology or API to get examples for")] string technology)
    {
        Console.WriteLine($"  🔧 Tool Called: GetCodeExample");
        Console.WriteLine($"  📝 Technology: {technology}");
        
        Thread.Sleep(180);

        var example = technology.ToLower() switch
        {
            var t when t.Contains("function") => 
                @"```csharp
[FunctionName(""HttpTrigger"")]
public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, ""get"", ""post"")] HttpRequest req,
    ILogger log)
{
    log.LogInformation(""Processing request"");
    var response = new { message = ""Hello from Azure Functions!"" };
    return new OkObjectResult(response);
}
```",
            _ => $"// Code example for {technology}\n// See documentation for details"
        };

        Console.WriteLine($"  ✅ Tool Result: Code example returned");
        return example;
    }
}
```

---

## Step 3: Create Agent with OpenTelemetry Tracing (5 minutes)

Create `TracedAgent.cs`:

```csharp
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

public class TracedAgent : IDisposable
{
    private readonly IChatClient _chatClient;
    private readonly TracerProvider _tracerProvider;
    private readonly ActivitySource _activitySource;

    public TracedAgent(IChatClient chatClient, bool useConsoleExporter = true)
    {
        _chatClient = chatClient;
        _activitySource = new ActivitySource("AgentTraceDemo");

        // Setup OpenTelemetry
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("AgentTraceDemo"))
            .AddSource("AgentTraceDemo")
            .AddSource("Microsoft.Extensions.AI");  // Capture AI SDK traces

        if (useConsoleExporter)
        {
            builder.AddConsoleExporter(options =>
            {
                options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
            });
        }

        _tracerProvider = builder.Build();
    }

    public async Task<string> AskQuestion(string question)
    {
        // Create top-level trace span
        using var activity = _activitySource.StartActivity("AgentRun", ActivityKind.Server);
        activity?.SetTag("user.question", question);

        Console.WriteLine($"\n{new string('=', 70)}");
        Console.WriteLine($"🤖 Agent Question: {question}");
        Console.WriteLine($"{new string('=', 70)}\n");

        try
        {
            // Create orchestration span
            using var orchestrationActivity = _activitySource.StartActivity("Orchestration");
            orchestrationActivity?.SetTag("phase", "planning");
            
            Console.WriteLine("📋 Phase: Orchestration/Planning");
            await Task.Delay(100); // Simulate planning time

            // Create model call span
            using var modelActivity = _activitySource.StartActivity("ModelCall");
            modelActivity?.SetTag("model", "gpt-4");
            modelActivity?.SetTag("phase", "inference");
            
            Console.WriteLine("🧠 Phase: Model Inference");

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, "You are a helpful Azure documentation assistant. Use available tools to search Microsoft Learn documentation."),
                new(ChatRole.User, question)
            };

            var options = new ChatOptions
            {
                Tools = [
                    AIFunctionFactory.Create(LearnMcpTool.SearchLearnDocs),
                    AIFunctionFactory.Create(LearnMcpTool.GetCodeExample)
                ]
            };

            var response = await _chatClient.CompleteAsync(messages, options);
            
            var toolCallCount = response.Message.Contents
                .OfType<FunctionCallContent>()
                .Count();

            modelActivity?.SetTag("tool_calls", toolCallCount);
            
            if (toolCallCount > 0)
            {
                Console.WriteLine($"\n🔧 Tool Calls Detected: {toolCallCount}");
            }

            // Create synthesis span
            using var synthesisActivity = _activitySource.StartActivity("Synthesis");
            synthesisActivity?.SetTag("phase", "response_generation");
            
            Console.WriteLine("\n✨ Phase: Response Synthesis");
            await Task.Delay(50); // Simulate synthesis time

            var finalAnswer = response.Message.Text ?? "No response generated.";
            
            activity?.SetTag("response.length", finalAnswer.Length);
            activity?.SetTag("status", "success");

            Console.WriteLine($"\n{new string('=', 70)}");
            Console.WriteLine("📤 Final Response:");
            Console.WriteLine($"{new string('=', 70)}");
            Console.WriteLine(finalAnswer);
            Console.WriteLine($"{new string('=', 70)}\n");

            return finalAnswer;
        }
        catch (Exception ex)
        {
            activity?.SetTag("status", "error");
            activity?.SetTag("error.message", ex.Message);
            
            Console.WriteLine($"\n❌ Error: {ex.Message}\n");
            throw;
        }
    }

    public void Dispose()
    {
        _tracerProvider?.Dispose();
        _activitySource?.Dispose();
    }
}
```

---

## Step 4: Create Trace Viewer (3 minutes)

Create `TraceVisualizer.cs`:

```csharp
using System.Diagnostics;

public class TraceVisualizer
{
    private readonly List<TraceSpan> _spans = new();
    private DateTime _startTime;

    public void StartCapture()
    {
        _startTime = DateTime.UtcNow;
        _spans.Clear();
    }

    public void CaptureSpan(string name, string phase, TimeSpan duration, Dictionary<string, string>? tags = null)
    {
        _spans.Add(new TraceSpan
        {
            Name = name,
            Phase = phase,
            Duration = duration,
            Tags = tags ?? new()
        });
    }

    public void DisplayWaterfall()
    {
        if (_spans.Count == 0)
        {
            Console.WriteLine("No trace spans captured.");
            return;
        }

        Console.WriteLine($"\n{new string('=', 80)}");
        Console.WriteLine("📊 TRACE WATERFALL VIEW");
        Console.WriteLine($"{new string('=', 80)}\n");

        var maxDuration = _spans.Max(s => s.Duration.TotalMilliseconds);
        var barWidth = 50;

        Console.WriteLine($"{"Span",-25} {"Duration",-12} {"Timeline",-50}");
        Console.WriteLine(new string('-', 80));

        foreach (var span in _spans)
        {
            var durationMs = span.Duration.TotalMilliseconds;
            var barLength = (int)((durationMs / maxDuration) * barWidth);
            var bar = new string('█', Math.Max(1, barLength));
            
            var colorCode = span.Phase switch
            {
                "planning" => "\x1b[36m",      // Cyan
                "inference" => "\x1b[33m",     // Yellow
                "tool_call" => "\x1b[32m",     // Green
                "synthesis" => "\x1b[35m",     // Magenta
                _ => "\x1b[37m"                // White
            };
            var resetCode = "\x1b[0m";

            Console.WriteLine($"{span.Name,-25} {durationMs,8:F1}ms   {colorCode}{bar}{resetCode}");

            // Show important tags
            if (span.Tags.ContainsKey("tool_name"))
            {
                Console.WriteLine($"  └─ Tool: {span.Tags["tool_name"]}");
            }
            if (span.Tags.ContainsKey("query"))
            {
                Console.WriteLine($"  └─ Query: {span.Tags["query"]}");
            }
        }

        var totalDuration = _spans.Sum(s => s.Duration.TotalMilliseconds);
        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"Total Duration: {totalDuration:F1}ms\n");

        Console.WriteLine("Legend:");
        Console.WriteLine("  \x1b[36m█\x1b[0m Planning  \x1b[33m█\x1b[0m Model Call  \x1b[32m█\x1b[0m Tool Call  \x1b[35m█\x1b[0m Synthesis");
        Console.WriteLine($"{new string('=', 80)}\n");
    }
}

public class TraceSpan
{
    public string Name { get; set; } = "";
    public string Phase { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}
```

---

## Step 5: Create Main Program (2 minutes)

Update `Program.cs`:

```csharp
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
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
```

---

## SIMPLIFIED VERSION (No Docker Required - 5 minutes)

If you want the fastest setup with zero dependencies:

Create `SimplifiedDemo.cs`:

```csharp
using System.Diagnostics;

public class SimplifiedDemo
{
    public static async Task Run()
    {
        Console.WriteLine("🎬 DEMO: Following a Tool Call\n");

        var question = "What are Azure Functions best practices?";
        Console.WriteLine($"❓ Question: {question}\n");

        // Simulate agent execution with manual trace capture
        var trace = new List<(string span, int durationMs, string? detail)>();
        var sw = Stopwatch.StartNew();

        // Step 1: User input
        await SimulateStep("User Input", 10, null, trace);

        // Step 2: Planning
        await SimulateStep("Orchestration/Planning", 45, null, trace);

        // Step 3: Model decides to call tool
        await SimulateStep("Model Call", 890, "Decides to call SearchLearnDocs tool", trace);

        // Step 4: Tool call (this is the key moment)
        Console.WriteLine("\n🔧 TOOL CALL DETECTED:");
        Console.WriteLine("   Function: SearchLearnDocs");
        Console.WriteLine("   Parameter: 'azure functions best practices'");
        Console.WriteLine("   Executing...\n");
        
        await SimulateStep("Tool: SearchLearnDocs", 230, "Query: azure functions best practices", trace);

        // Step 5: Synthesis
        await SimulateStep("Response Synthesis", 120, "Formatting answer from tool results", trace);

        sw.Stop();

        // Show final answer
        Console.WriteLine("\n✅ FINAL ANSWER:");
        Console.WriteLine("Azure Functions best practices include using consumption");
        Console.WriteLine("plan for variable workloads, implementing proper error");
        Console.WriteLine("handling, and enabling Application Insights...\n");

        // Show trace waterfall
        Console.WriteLine("📊 TRACE WATERFALL:\n");
        var maxDuration = trace.Max(t => t.durationMs);
        
        foreach (var (span, duration, detail) in trace)
        {
            var barLength = (int)((duration / (double)maxDuration) * 40);
            var bar = new string('█', Math.Max(1, barLength));
            Console.WriteLine($"{span,-30} {duration,5}ms  {bar}");
            if (detail != null)
                Console.WriteLine($"  └─ {detail}");
        }

        Console.WriteLine($"\nTotal: {sw.ElapsedMilliseconds}ms");

        // Key message
        Console.WriteLine("\n💡 THE KEY INSIGHT:");
        Console.WriteLine("   The final answer is the receipt.");
        Console.WriteLine("   The trace is the security footage.");
        Console.WriteLine("   You can see EXACTLY which tool was called and why.");
    }

    private static async Task SimulateStep(string name, int durationMs, string? detail, 
        List<(string, int, string?)> trace)
    {
        Console.WriteLine($"⚙️  {name}...");
        await Task.Delay(Math.Min(durationMs, 100)); // Don't actually wait full time
        trace.Add((name, durationMs, detail));
    }
}
```

Then your `Program.cs` becomes just:
```csharp
await SimplifiedDemo.Run();
```

Run with: `dotnet run`

---

## Demo Script for Presentation

**[Switch to terminal with large font]**

> "Demo 2: let's watch an agent call a tool and inspect the trace."

**[Show the question]**

> "I'm asking: 'What are Azure Functions best practices?' Simple question."

**[Run the agent]**

> "Watch the phases. Orchestration, model call... and here comes the tool call."

**[Point to tool call output]**

> "See that? The agent decided to call SearchLearnDocs. Here are the exact parameters it passed."

**[Point to tool result]**

> "Tool returned the docs. 230 milliseconds. Now the agent synthesizes the final answer."

**[Show final answer]**

> "Beautiful answer. Polished. Professional. But..."

**[Scroll up to trace]**

> "The trace tells us what actually happened. Which tool. What parameters. How long each step took."

**[Show failure scenario]**

> "And here's the magic: when things go wrong..."

**[Show failure trace]**

> "The trace shows us the tool call timed out. The agent fell back. User got an answer, but we know it's incomplete."

**[Pause for effect]**

> "The final answer is the receipt. The trace is the security camera footage. This is how you debug agent behavior."

---

## What You'll Demonstrate

1. **Normal execution**: See the complete trace waterfall
2. **Tool call visibility**: Exact function name and parameters
3. **Timing breakdown**: Where time is spent
4. **Failure scenario**: What a broken trace looks like

**Demo runs in 3-4 minutes** and shows why traces are essential!

---

## Pro Tips

1. **Increase terminal font** - make sure audience can read
2. **Use color output** - the color-coded waterfall is compelling
3. **Pause at the tool call** - that's your money shot
4. **Pre-run once** to verify everything works
5. **Have backup screenshots** in case of WiFi issues

---

## Even Simpler Option: Pre-recorded Screenshots

If you want ZERO live demo risk:

1. Run the demo locally
2. Take screenshots of:
   - The question
   - The tool call output
   - The trace waterfall
   - The failure scenario
3. Show screenshots and narrate

This is actually safer and lets you control timing perfectly!

---

## Files Summary

```
AgentTraceDemo/
├── Program.cs              (main demo runner)
├── TracedAgent.cs          (agent with OpenTelemetry)
├── LearnMcpTool.cs         (simulated MCP tool)
├── TraceVisualizer.cs      (waterfall display)
└── SimplifiedDemo.cs       (optional: no dependencies version)
```

Total setup time: **10-15 minutes** (or 5 with simplified version)
Demo runtime: **3-4 minutes**

🎯 This demo is the visual proof of why observability matters!
