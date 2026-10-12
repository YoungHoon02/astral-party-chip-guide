namespace ChipGuide.Core;

public sealed class RuleSource
{
    public const int MaxErrorsLogged = 20;

    private readonly Func<string?> _readNames;
    private readonly Func<string?> _readRules;
    private readonly Action<string> _log;

    public RuleSource(Func<string?> readNames, Func<string?> readRules, Action<string> log)
    {
        _readNames = readNames;
        _readRules = readRules;
        _log = log;
    }

    public RuleSet Current { get; private set; } = RuleSet.Empty;
    public int Version { get; private set; }

    public bool Reload()
    {
        try
        {
            var namesJson = _readNames();
            if (namesJson is null)
            {
                _log("names.json이 없습니다. 게임에서 템플릿 생성 키(기본 F10)를 눌러 만드세요.");
                return false;
            }
            var rulesJson = _readRules();
            if (rulesJson is null)
            {
                _log("rules.json이 없습니다.");
                return false;
            }
            var load = RuleBook.Load(rulesJson, NameTable.Parse(namesJson));
            foreach (var error in load.Errors.Take(MaxErrorsLogged)) _log(error);
            if (load.Errors.Count > MaxErrorsLogged)
                _log($"... 외 {load.Errors.Count - MaxErrorsLogged}건의 오류가 더 있습니다. 게임 언어와 names.json이 맞는지 확인하세요.");
            Current = load.Rules;
            Version++;
            _log($"규칙 {load.Rules.Rules.Count}개를 불러왔습니다(오류 {load.Errors.Count}건).");
            return true;
        }
        catch (RuleFormatException ex)
        {
            _log($"{ex.Message} — 이전 규칙을 유지합니다.");
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log($"규칙 파일을 읽지 못했습니다({ex.Message}) — 이전 규칙을 유지합니다.");
            return false;
        }
    }
}
