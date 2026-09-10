using System.Globalization;

namespace Retag;

/// <summary>Hand-rolled argument parsing. No CLI framework: there is nothing here worth a dependency.</summary>
public sealed class Args
{
    public List<string> Positional { get; } = [];
    public Dictionary<string, string?> Flags { get; } = new(StringComparer.Ordinal);

    public static Args Parse(string[] argv)
    {
        var args = new Args();

        for (var i = 0; i < argv.Length; i++)
        {
            var token = argv[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                args.Positional.Add(token);
                continue;
            }

            var name = token;
            string? value = null;
            if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = argv[++i];
            }

            args.Flags[name] = value;
        }

        return args;
    }

    public bool Has(string name) => Flags.ContainsKey(name);

    public string? Value(string name) => Flags.TryGetValue(name, out var v) ? v : null;

    public string Value(string name, string fallback) => Value(name) ?? fallback;

    public int Int(string name, int fallback) =>
        int.TryParse(Value(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public string? At(int index) => index < Positional.Count ? Positional[index] : null;
}
