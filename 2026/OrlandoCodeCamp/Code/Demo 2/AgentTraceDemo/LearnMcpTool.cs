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