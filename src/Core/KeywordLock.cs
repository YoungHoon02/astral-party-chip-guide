namespace ChipGuide.Core;

public enum Keyword { None = 0, Cure = 1, Salary = 2, Mark = 3, Energy = 4 }

// 서로 다른 키워드 칩을 두 종류 고르면 그 판에서 나머지 두 키워드의 칩은 선택지에 나오지 않는다.
// 선택지는 서버가 정하므로 클라이언트 코드에는 이 규칙이 없다.
public static class KeywordLock
{
    public const int Limit = 2;

    private static readonly Keyword[] All = { Keyword.Cure, Keyword.Salary, Keyword.Mark, Keyword.Energy };

    public static string Name(Keyword keyword) => keyword switch
    {
        Keyword.Cure => "회복",
        Keyword.Salary => "별빛",
        Keyword.Mark => "표식",
        Keyword.Energy => "에너지",
        _ => "",
    };

    public static IReadOnlySet<Keyword> Owned(IEnumerable<Keyword> keywords) => keywords.Where(k => k != Keyword.None).ToHashSet();

    public static string? Warning(IReadOnlySet<Keyword> owned, Keyword chip)
    {
        if (chip == Keyword.None || owned.Contains(chip) || owned.Count != Limit - 1) return null;
        var kept = owned.Append(chip).OrderBy(k => k).Select(Name).ToList();
        var lost = All.Where(k => k != chip && !owned.Contains(k)).Select(Name);
        var last = kept[^1];
        return $"고르면 키워드가 {string.Join("·", kept)}{Josa(last)} 고정되어 {string.Join("·", lost)} 칩이 더 나오지 않음";
    }

    // 받침이 없거나 ㄹ 받침이면 "로", 그 밖의 받침이면 "으로".
    private static string Josa(string word)
    {
        var c = word[^1];
        if (c is < '가' or > '힣') return "(으)로";
        var final = (c - '가') % 28;
        return final is 0 or 8 ? "로" : "으로";
    }
}
