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