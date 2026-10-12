using ChipGuide.Core;

namespace ChipGuide.Plugin;

internal sealed class RuleFiles
{
    private readonly string _names;
    private readonly string _rules;
    private (DateTime Names, DateTime Rules) _stamp;

    internal RuleFiles(string folder)
    {
        _names = Path.Combine(folder, "names.json");
        _rules = Path.Combine(folder, "rules.json");
    }

    internal string? ReadNames() => Read(_names);
    internal string? ReadRules() => Read(_rules);

    internal bool Changed()
    {
        var stamp = (Stamp(_names), Stamp(_rules));
        if (stamp == _stamp) return false;
        _stamp = stamp;
        return true;
    }

    private static DateTime Stamp(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

    private static string? Read(string path)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > Json.MaxBytes) throw new IOException($"{Path.GetFileName(path)}: 파일이 너무 큽니다(최대 {Json.MaxBytes / 1024}KB)");
        return File.ReadAllText(path);
    }
}
