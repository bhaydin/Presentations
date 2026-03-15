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