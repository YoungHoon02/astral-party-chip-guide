using System.Text;

namespace ChipGuide.Core;

public static class TextSafety
{
    public const int MaxLength = 200;

    // 공유받은 규칙 파일의 문구가 게임 UI 서식(UBB·HTML)으로 해석되지 않도록 괄호를 전각으로 바꾼다.
    public static string Clean(string? text, int max = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim())
        {
            builder.Append(c switch
            {
                '[' => '［',
                ']' => '］',
                '<' => '＜',
                '>' => '＞',
                _ when char.IsControl(c) => ' ',
                _ => c,
            });
        }
        var cleaned = builder.ToString();
        if (cleaned.Length <= max) return cleaned;
        var cut = max - 1;
        if (char.IsHighSurrogate(cleaned[cut - 1])) cut--;
        return cleaned[..cut] + "…";
    }
}
