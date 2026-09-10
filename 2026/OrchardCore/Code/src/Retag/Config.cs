namespace Retag;

/// <summary>
/// Azure OpenAI configuration. Absent credentials are a supported, first-class
/// state: every command except the --live ones runs fully without them.
/// </summary>
public sealed record Config(string? Endpoint, string? Deployment, string? ApiKey)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Deployment);

    public static Config Load()
    {
        LoadDotEnv();
        return new Config(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"),
            Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"),
            // Both spellings are in the wild; regional endpoints need the key,
            // because token auth there requires a custom subdomain.
            Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY"));
    }

    private static void LoadDotEnv()
    {
        var path = Path.Combine(Paths.Root, ".env");
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"', '\'');

            // Real environment variables win over the file.
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
