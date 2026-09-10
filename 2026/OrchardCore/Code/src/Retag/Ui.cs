using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Retag;

public enum Colour { Default, Green, Amber }

/// <summary>
/// The terminal output IS the deliverable. Every width here is deliberate and
/// slides depend on it. Colour is applied at write time and never participates
/// in layout: widths are measured on the plain text.
/// </summary>
public static partial class Ui
{
    public const int MaxWidth = 72;

    /// <summary>Two-space left indent on every output block.</summary>
    public const string Indent = "  ";

    private static bool? _useColour;
    private static bool? _animate;

    /// <summary>Colour is on for a terminal, off when redirected or NO_COLOR is set.</summary>
    public static bool UseColour
    {
        get => _useColour ?? DetectColour();
        set => _useColour = value;
    }

    /// <summary>Progress counters animate only for a live audience.</summary>
    public static bool Animate
    {
        get => _animate ?? !Console.IsOutputRedirected;
        set => _animate = value;
    }

    [GeneratedRegex(@"\u001b\[[0-9;]*m")]
    private static partial Regex AnsiPattern();

    private static bool DetectColour()
    {
        if (Environment.GetEnvironmentVariable("NO_COLOR") is not null) return false;
        if (Console.IsOutputRedirected) return false;
        return true;
    }

    public static string C(string text, Colour colour)
    {
        if (!UseColour || colour == Colour.Default) return text;
        var code = colour switch
        {
            Colour.Green => "\u001b[32m",
            Colour.Amber => "\u001b[33m",
            _ => "",
        };
        return code + text + "\u001b[0m";
    }

    public static string Green(string text) => C(text, Colour.Green);
    public static string Amber(string text) => C(text, Colour.Amber);

    public static int Width(string text) => AnsiPattern().Replace(text, "").Length;

    public static string Plain(string text) => AnsiPattern().Replace(text, "");

    public static void Line(string text = "") => Console.Out.Write(text + "\n");

    public static void Blank() => Line();

    /// <summary>Thousands separators, invariant, so 4182 always reads 4,182.</summary>
    public static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    /// "  label ....... value" with the value starting at <paramref name="valueColumn"/>.
    /// The dot leader absorbs the slack.
    /// </summary>
    public static string Leader(string label, string value, int valueColumn, char fill = '.')
    {
        var head = Indent + label + " ";
        var dots = valueColumn - 1 - head.Length;
        if (dots < 0) dots = 0;
        return head + new string(fill, dots) + " " + value;
    }

    /// <summary>Right-align a value so its last character lands on <paramref name="endColumn"/> - 1.</summary>
    public static string RightAt(string line, string value, int endColumn)
    {
        var pad = endColumn - value.Length - Width(line);
        if (pad < 0) pad = 0;
        return line + new string(' ', pad) + value;
    }

    public static string PadTo(string line, int column)
    {
        var pad = column - Width(line);
        return pad <= 0 ? line : line + new string(' ', pad);
    }

    /// <summary>
    /// Every command ends with both counters, always, even when both are zero.
    /// A hold is not an error, which is exactly why they are separate columns.
    /// </summary>
    public static void Footer(int errors, int heldByPolicy, string separator = "     ")
    {
        var errorText = errors == 0 ? Green("0") : Amber(N(errors));
        var heldText = heldByPolicy == 0 ? Green("0") : Amber(N(heldByPolicy));
        Line($"{Indent}ERRORS: {errorText}{separator}HELD BY POLICY: {heldText}");
    }

    /// <summary>
    /// Progress leaders animate for a live audience but collapse to a single
    /// final line when redirected, so golden files stay byte-identical.
    /// </summary>
    public static void Progress(string label, string finalValue, int valueColumn, int steps, int delayMs)
    {
        if (Animate && steps > 0 && delayMs > 0)
        {
            for (var i = 1; i <= steps; i++)
            {
                Console.Out.Write("\r" + Leader(label, finalValue, valueColumn));
                Thread.Sleep(delayMs);
            }
            Console.Out.Write("\r");
        }
        Line(Leader(label, finalValue, valueColumn));
    }

    public static string Bar(int passed, int total, int cells = 8)
    {
        var filled = total == 0 ? 0 : passed * cells / total;
        return "[" + new string('\u2588', filled) + new string('\u2591', cells - filled) + "]";
    }
}
