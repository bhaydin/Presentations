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