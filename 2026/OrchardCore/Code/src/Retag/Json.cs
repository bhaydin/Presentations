using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Retag;

/// <summary>Deterministic JSON I/O: LF endings, UTF-8 without BOM, stable shape.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void Write<T>(string path, T value)
    {
        var text = JsonSerializer.Serialize(value, Options).ReplaceLineEndings("\n") + "\n";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, Utf8NoBom);
    }

    public static T Read<T>(string path)
    {
        var text = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(text, Options)
               ?? throw new InvalidDataException($"empty or invalid fixture: {path}");
    }
}
