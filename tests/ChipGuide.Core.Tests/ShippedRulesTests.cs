using System.Text.Json;

namespace ChipGuide.Core.Tests;

public class ShippedRulesTests
{
    private static readonly string Root = FindRoot();
    internal static readonly string RulesPath = Path.Combine(Root, "rules", "rules.json");
    private static readonly string NamesJson = File.ReadAllText(Path.Combine(Root, "tests", "ChipGuide.Core.Tests", "Fixtures", "names.json"));
    internal static readonly NameTable Names = NameTable.Parse(NamesJson);
    private static readonly RuleLoad Load = RuleBook.Load(File.ReadAllText(RulesPath), Names);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ChipGuide.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("ChipGuide.sln이 있는 폴더를 찾을 수 없습니다");
    }

    private static int Hero(string name) => Names.Hero(name)!.Single();

    private static HashSet<int> ChipIds(IEnumerable<string> names) => names.SelectMany(name => Names.Chip(name)!).ToHashSet();

    private static Situation At(string hero, string when)
    {
        var parts = when.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split(':')).ToDictionary(p => p[0], p => p[1]);
        string[] Value(string key) => parts.TryGetValue(key, out var value) ? new[] { value } : Array.Empty<string>();
        return new Situation(Hero(hero),
            ChipIds(Value("owned")),
            Value("ally").Select(Hero).ToHashSet(),
            Value("map") is [var map] ? Names.Map(map)!.First() : 0,
            ChipIds(Value("allyOwned")));
    }

    [Fact]
    public void 기본_규칙_파일은_오류_없이_읽힌다() => Assert.Empty(Load.Errors);

    [Fact]
    public void 모든_캐릭터는_코어가_넷_이상이고_코어_키워드는_두_종류를_넘지_않는다()
    {
        using var document = JsonDocument.Parse(NamesJson);
        var chips = document.RootElement.GetProperty("chips").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetInt32(), c => (Base: c.GetProperty("base").GetString()!, Keyword: (Keyword)c.GetProperty("keyword").GetInt32()));
        var heroes = document.RootElement.GetProperty("heroes").EnumerateArray().Select(h => h.GetProperty("name").GetString()!);

        foreach (var hero in heroes)
        {
            var core = ChipAdvisor.CoreChips(Load.Rules, At(hero, "")).Select(id => chips[id]).ToList();
            Assert.True(core.Select(c => c.Base).Distinct().Count() >= 4, $"{hero}: 코어 {core.Select(c => c.Base).Distinct().Count()}개");
            Assert.True(KeywordLock.Owned(core.Select(c => c.Keyword)).Count <= KeywordLock.Limit, $"{hero}: 코어 키워드가 서로 막음");
        }
    }

    [Theory]
    [InlineData("파파라", "숫돌", Grade.Purple, "owned:커터칼", Verdict.Core)]
    [InlineData("파파라", "숫돌", Grade.Purple, "allyOwned:고기 가득 스튜", Verdict.Core)]
    [InlineData("알라나", "숫돌", Grade.Purple, "owned:커터칼", Verdict.Avoid)]
    [InlineData("알라나", "커터칼", Grade.Purple, "owned:아드레날린", Verdict.Avoid)]
    [InlineData("마무시", "커터칼", Grade.Purple, "", Verdict.Core)]
    [InlineData("마무시", "커터칼", Grade.Purple, "map:영혼 축제", Verdict.Good)]
    [InlineData("자스민", "표준 조준경", Grade.Purple, "", Verdict.Good)]
    [InlineData("자스민", "복싱 글러브", Grade.Blue, "", Verdict.Core)]
    [InlineData("도로시 헤이즈", "이글 아이 조준경", Grade.Orange, "", Verdict.Good)]
    [InlineData("미미", "휴대용 선풍기", Grade.Blue, "", Verdict.Avoid)]
    [InlineData("미미", "휴대용 선풍기", Grade.Purple, "", Verdict.Core)]
    [InlineData("테루", "이글 아이 조준경", Grade.Orange, "", Verdict.Avoid)]
    [InlineData("루카", "표준 조준경", Grade.Purple, "", Verdict.Core)]
    [InlineData("루카", "이글 아이 조준경", Grade.Orange, "", Verdict.Avoid)]
    [InlineData("패니", "휴대용 선풍기", Grade.Purple, "ally:린", Verdict.Core)]
    [InlineData("패니", "휴대용 선풍기", Grade.Purple, "", Verdict.Good)]
    [InlineData("패니", "휴대용 선풍기", Grade.Blue, "ally:보니", Verdict.Good)]
    [InlineData("패니", "휴대용 선풍기", Grade.Purple, "ally:보니", Verdict.Core)]
    [InlineData("루루", "휴대용 선풍기", Grade.Blue, "", Verdict.Avoid)]
    [InlineData("루루", "휴대용 선풍기", Grade.Blue, "ally:보니", Verdict.Good)]
    [InlineData("미미", "휴대용 선풍기", Grade.Purple, "ally:보니", Verdict.Core)]
    [InlineData("루루", "마법 화살통", Grade.Purple, "ally:린", Verdict.Good)]
    [InlineData("루루", "마법 화살통", Grade.Purple, "ally:보니", Verdict.Good)]
    [InlineData("질 스팅레이", "우정 배지", Grade.Purple, "allyOwned:아드레날린", Verdict.Good)]
    [InlineData("토노 한나", "우정 배지", Grade.Purple, "allyOwned:아드레날린", Verdict.Avoid)]
    [InlineData("알라나", "아드레날린", Grade.Orange, "ally:질 스팅레이", Verdict.Good)]
    [InlineData("알라나", "아드레날린", Grade.Orange, "ally:도로시 헤이즈", Verdict.Avoid)]
    [InlineData("알라나", "숫돌", Grade.Purple, "ally:판다맨", Verdict.Avoid)]
    [InlineData("파파라", "숫돌", Grade.Purple, "ally:루루", Verdict.Core)]
    [InlineData("테루", "슈팅 스타", Grade.Purple, "owned:복싱 글러브;map:마법 학교", Verdict.Avoid)]
    [InlineData("마무시", "ATM기", Grade.Blue, "allyOwned:ATM기", Verdict.Good)]
    [InlineData("루루", "ATM기", Grade.Blue, "allyOwned:ATM기", Verdict.Core)]
    [InlineData("유메", "책갈피", Grade.Purple, "", null)]
    [InlineData("나르디스", "커터칼", Grade.Purple, "", Verdict.Good)]
    [InlineData("나르디스", "커터칼", Grade.Purple, "owned:비타민 알약", Verdict.Core)]
    [InlineData("린", "책갈피", Grade.Purple, "", Verdict.Core)]
    [InlineData("린", "전기 코어", Grade.Purple, "", Verdict.Good)]
    [InlineData("타치바나 셰리", "쇼크 글러브", Grade.Purple, "", Verdict.Core)]
    [InlineData("코마치", "휴대용 선풍기", Grade.Blue, "", Verdict.Good)]
    [InlineData("토노 한나", "한나 인형", Grade.Orange, "map:이변 도서관", Verdict.Good)]
    [InlineData("사이크스", "슈팅 스타", Grade.Orange, "map:유령 골목", Verdict.Core)]
    [InlineData("미미", "슈팅 스타", Grade.Orange, "map:유령 골목", Verdict.Good)]
    [InlineData("펭", "타겟 보드", Grade.Blue, "", Verdict.Good)]
    [InlineData("펭", "천공위성", Grade.Orange, "map:워터 타운", null)]
    [InlineData("미미", "천공위성", Grade.Orange, "map:워터 타운", Verdict.Good)]
    public void 상황별_판정(string hero, string chip, Grade grade, string when, Verdict? expected)
    {
        var id = Names.Chip(chip)!.Single(i => Names.ChipGrades[i] == grade);

        var advice = ChipAdvisor.Advise(Load.Rules, At(hero, when), new[] { new ChipOption(id, grade) })[0];

        Assert.Equal(expected, advice?.Verdict);
    }
}
