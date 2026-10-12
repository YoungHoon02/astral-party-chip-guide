using System.Text;
using System.Text.Json;

namespace ChipGuide.Core;

public sealed class RuleFormatException : Exception
{
    public RuleFormatException(string message) : base(message) { }
}

public static class Json
{
    public const int MaxBytes = 1_048_576;

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 32,
    };

    public static JsonDocument Parse(string text, string file)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
            throw new RuleFormatException($"{file}: 파일이 너무 큽니다(최대 {MaxBytes / 1024}KB)");
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text.TrimStart('\uFEFF'), Options);
        }
        catch (JsonException ex)
        {
            throw new RuleFormatException($"{file} {(ex.LineNumber ?? 0) + 1}행: JSON 형식 오류");
        }
        try
        {
            Validate(document.RootElement);
        }
        catch (InvalidOperationException)
        {
            document.Dispose();
            throw new RuleFormatException($"{file}: 잘못된 유니코드 문자열이 있습니다");
        }
        return document;
    }

    private static void Validate(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                element.GetString();
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    _ = property.Name;
                    Validate(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) Validate(item);
                break;
        }
    }

    public static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    public static string? String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
