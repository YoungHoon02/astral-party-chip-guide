using ChipGuide.Core;

namespace ChipGuide.Plugin;

internal sealed class RuleFiles
{
    private readonly string _names;
    private readonly string _rules;
    private readonly string _default;
    private (DateTime Names, DateTime Rules) _stamp;
    private string? _fallback;

    internal RuleFiles(string folder)
    {
        _names = Path.Combine(folder, "names.json");
        _rules = Path.Combine(folder, "rules.json");
        _default = Path.Combine(folder, "rules.default.json");
    }

    internal string? ReadNames() => Read(_names);
    internal string? ReadRules() => _fallback ?? Read(_rules);

    // 파일을 못 바꾸면 이번 실행 동안 기본 규칙을 직접 쓴다. 옛 규칙으로 판정하는 것보다 낫다.
    internal void Update(string version, Action<string> log)
    {
        if (!File.Exists(_default))
        {
            log($"{Path.GetFileName(_default)}가 없어 rules.json을 그대로 씁니다.");
            return;
        }
        var bundled = File.ReadAllText(_default);
        try
        {
            var plan = RuleUpdate.Plan(File.Exists(_rules) ? File.ReadAllText(_rules) : null, bundled, version);
            if (plan.Action == RuleUpdateAction.Keep) return;
            if (plan.Backup) File.Copy(_rules, _rules + RuleUpdate.BackupSuffix, overwrite: true);
            var temp = _rules + ".tmp";
            File.WriteAllText(temp, plan.Content);
            File.Move(temp, _rules, overwrite: true);
            log(plan.Action == RuleUpdateAction.Install
                ? "기본 rules.json을 만들었습니다."
                : $"모드 업데이트에 맞춰 rules.json을 새 기본 규칙으로 바꿨습니다.{(plan.Backup ? $" 직접 고친 이전 파일은 rules.json{RuleUpdate.BackupSuffix}에 남겼습니다." : "")}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _fallback = bundled;
            log($"rules.json을 새 기본 규칙으로 바꾸지 못했습니다({ex.Message}). 이번 실행은 {Path.GetFileName(_default)}의 기본 규칙을 씁니다.");
        }
    }

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
