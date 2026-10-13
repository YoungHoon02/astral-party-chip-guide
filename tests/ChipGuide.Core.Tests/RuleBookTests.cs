namespace ChipGuide.Core.Tests;

public class RuleBookTests
{
    private static readonly NameTable Names = NameTable.Parse(NameTableTests.Sample);

    private static Situation At(int hero, int[]? owned = null, int[]? allies = null, int map = 0, int[]? allyOwned = null) =>
        new(hero, new HashSet<int>(owned ?? Array.Empty<int>()), new HashSet<int>(allies ?? Array.Empty<int>()), map,
            new HashSet<int>(allyOwned ?? Array.Empty<int>()));

    private static Advice? Judge(string json, Situation situation, int relic, Grade grade)
    {
        var load = RuleBook.Load(json, Names);
        return ChipAdvisor.Advise(load.Rules, situation, new[] { new ChipOption(relic, grade) })[0];
    }

    [Fact]
    public void 역할과_캐릭터와_조건부_규칙을_읽는다()
    {
        const string json = """
        {
          "version": 1,
          // 주석 허용
          "roles": { "dealer": { "good": ["커터칼"] } },
          "characters": {
            "파파라": { "role": "dealer", "core": ["아드레날린"], "why": { "아드레날린": "피 절반 이하에서 공 +3과 맞물림" } }
          },
          "rules": [
            { "when": { "ally": ["루루"] }, "chip": "아드레날린", "verdict": "avoid", "why": "아군 회복이 아드 조건을 깸" },
          ],
        }
        """;

        var load = RuleBook.Load(json, Names);
        Assert.Empty(load.Errors);

        var alone = Judge(json, At(1), 100, Grade.Purple)!;
        Assert.Equal((Verdict.Core, "피 절반 이하에서 공 +3과 맞물림"), (alone.Verdict, alone.Reason));

        var withLulu = Judge(json, At(1, allies: new[] { 4 }), 100, Grade.Purple)!;
        Assert.Equal(Verdict.Avoid, withLulu.Verdict);

        var cutter = Judge(json, At(1), 300, Grade.Purple)!;
        Assert.Equal((Verdict.Good, "딜러 공용 무난"), (cutter.Verdict, cutter.Reason));
    }

    [Fact]
    public void 아군_보유_칩_조건을_읽는다()
    {
        const string json = """
        {
          "version": 1,
          "rules": [ { "when": { "allyOwned": ["커터칼"] }, "chip": "아드레날린", "verdict": "avoid", "why": "아군 커터칼" } ]
        }
        """;

        Assert.Empty(RuleBook.Load(json, Names).Errors);
        Assert.Equal(Verdict.Avoid, Judge(json, At(3, allyOwned: new[] { 300 }), 100, Grade.Purple)!.Verdict);
        Assert.Null(Judge(json, At(3, owned: new[] { 300 }), 100, Grade.Purple));
    }

    [Fact]
    public void 역할_조건은_캐릭터의_역할과_정확히_비교한다()
    {
        const string json = """
        {
          "version": 1,
          "characters": { "미미": { "role": "support" }, "파파라": { "role": "flex" } },
          "rules": [ { "when": { "role": ["support"] }, "chip": "커터칼", "verdict": "good", "why": "서포터만" } ]
        }
        """;

        Assert.Empty(RuleBook.Load(json, Names).Errors);
        Assert.Equal(Verdict.Good, Judge(json, At(3), 300, Grade.Purple)!.Verdict);
        Assert.Null(Judge(json, At(1), 300, Grade.Purple));
        Assert.Null(Judge(json, At(4), 300, Grade.Purple));
    }

    [Fact]
    public void 잘못된_역할_조건은_규칙을_뺀다()
    {
        const string json = """
        { "version": 1, "rules": [ { "when": { "role": ["tank"] }, "chip": "커터칼", "verdict": "good", "why": "x" } ] }
        """;

        var load = RuleBook.Load(json, Names);

        Assert.Empty(load.Rules.Rules);
        Assert.Contains(load.Errors, e => e.StartsWith("rules[0].when.role"));
    }

    [Fact]
    public void 최소_등급을_항목과_규칙_양쪽에서_읽는다()
    {
        const string json = """
        {
          "version": 1,
          "characters": { "미미": { "role": "support", "core": [ { "chip": "손풍기", "minGrade": "purple" } ] } },
          "rules": [ { "when": { "map": ["유령 골목"] }, "chip": "커터칼", "verdict": "good", "why": "맵", "minGrade": "orange" } ]
        }
        """;

        Assert.Equal(Verdict.Avoid, Judge(json, At(3), 200, Grade.Blue)!.Verdict);
        Assert.Equal(Verdict.Core, Judge(json, At(3), 201, Grade.Purple)!.Verdict);
        Assert.Equal("주황 이상 권장", Judge(json, At(3, map: 52), 300, Grade.Purple)!.Reason);
    }

    [Fact]
    public void 없는_이름은_그_항목만_빼고_경로를_남긴다()
    {
        const string json = """
        { "version": 1, "characters": { "미미": { "core": ["가방없음", "손풍기"] } } }
        """;

        var load = RuleBook.Load(json, Names);

        Assert.Contains(load.Errors, e => e.Contains("characters.미미.core[0]") && e.Contains("가방없음"));
        Assert.Single(load.Rules.Rules);
    }

    [Fact]
    public void 없는_캐릭터는_그_캐릭터만_뺀다()
    {
        var load = RuleBook.Load("""{ "version": 1, "characters": { "없는캐릭": { "core": ["손풍기"] } } }""", Names);

        Assert.Contains(load.Errors, e => e.Contains("characters.없는캐릭"));
        Assert.Empty(load.Rules.Rules);
    }

    [Fact]
    public void 조건_목록에_없는_이름이_있으면_규칙_전체를_뺀다()
    {
        const string json = """
        { "version": 1, "rules": [ { "when": { "ally": ["루루", "없는캐릭"] }, "chip": "손풍기", "verdict": "avoid", "why": "x" } ] }
        """;

        var load = RuleBook.Load(json, Names);

        Assert.Empty(load.Rules.Rules);
        Assert.Contains(load.Errors, e => e.Contains("rules[0].when.ally"));
    }

    [Fact]
    public void 빈_조건은_오류다()
    {
        var load = RuleBook.Load("""{ "version": 1, "rules": [ { "when": {}, "chip": "손풍기", "verdict": "core", "why": "x" } ] }""", Names);

        Assert.Empty(load.Rules.Rules);
        Assert.Contains(load.Errors, e => e.Contains("rules[0].when") && e.Contains("비어"));
    }

    [Fact]
    public void 이유가_없는_조건부_규칙은_오류다()
    {
        var load = RuleBook.Load("""{ "version": 1, "rules": [ { "when": { "map": ["유령 골목"] }, "chip": "손풍기", "verdict": "core" } ] }""", Names);

        Assert.Empty(load.Rules.Rules);
        Assert.Contains(load.Errors, e => e.Contains("rules[0].why"));
    }

    [Fact]
    public void 잘못된_판정과_등급과_역할은_오류다()
    {
        const string json = """
        {
          "version": 1,
          "roles": { "tank": { "good": ["손풍기"] } },
          "characters": { "미미": { "role": "healer", "core": [ { "chip": "손풍기", "minGrade": "gold" } ] } },
          "rules": [ { "when": { "map": ["유령 골목"] }, "chip": "손풍기", "verdict": "best", "why": "x" } ]
        }
        """;

        var load = RuleBook.Load(json, Names);

        Assert.Contains(load.Errors, e => e.Contains("roles.tank"));
        Assert.Contains(load.Errors, e => e.Contains("characters.미미.role"));
        Assert.Contains(load.Errors, e => e.Contains("characters.미미.core[0].minGrade"));
        Assert.Contains(load.Errors, e => e.Contains("rules[0].verdict"));
    }

    [Fact]
    public void 이유_문구를_정리한다()
    {
        const string json = """
        { "version": 1, "rules": [ { "when": { "map": ["유령 골목"] }, "chip": "손풍기", "verdict": "core", "why": "[b]굵게[/b]" } ] }
        """;

        Assert.Equal("［b］굵게［/b］", RuleBook.Load(json, Names).Rules.Rules[0].Why);
    }

    [Fact]
    public void 플렉스_역할은_딜러와_서포터_기본값을_모두_받는다()
    {
        const string json = """
        {
          "version": 1,
          "roles": { "dealer": { "good": ["커터칼"] }, "support": { "good": ["손풍기"] } },
          "characters": { "미미": { "role": "flex" } }
        }
        """;

        var cutter = Judge(json, At(3), 300, Grade.Purple)!;
        Assert.Equal((Verdict.Good, "딜러 공용 무난"), (cutter.Verdict, cutter.Reason));

        var fan = Judge(json, At(3), 200, Grade.Blue)!;
        Assert.Equal((Verdict.Good, "서포터 공용 무난"), (fan.Verdict, fan.Reason));
    }

    [Fact]
    public void 역할이_없는_캐릭터는_공용만_받는다()
    {
        const string json = """
        {
          "version": 1,
          "roles": { "common": { "good": ["아드레날린"] }, "dealer": { "good": ["커터칼"] } },
          "characters": { "미미": { "core": ["손풍기"] } }
        }
        """;

        var common = Judge(json, At(3), 100, Grade.Purple)!;
        Assert.Equal((Verdict.Good, "공용 무난"), (common.Verdict, common.Reason));

        Assert.Null(Judge(json, At(3), 300, Grade.Purple));
    }

    [Fact]
    public void 보유_칩과_캐릭터_조건을_함께_읽는다()
    {
        const string json = """
        {
          "version": 1,
          "rules": [
            { "when": { "owned": ["아드레날린"], "character": ["파파라"] }, "chip": "커터칼", "verdict": "core", "why": "아드와 세트" },
            { "when": { "owned": ["아드레날린"] }, "chip": "커터칼", "verdict": "avoid", "why": "일반" }
          ]
        }
        """;

        var withSet = Judge(json, At(1, owned: new[] { 100 }), 300, Grade.Purple)!;
        Assert.Equal((Verdict.Core, "아드와 세트"), (withSet.Verdict, withSet.Reason));
        Assert.Equal(new[] { new Note(Verdict.Avoid, "일반") }, withSet.Notes);

        var otherHero = Judge(json, At(3, owned: new[] { 100 }), 300, Grade.Purple)!;
        Assert.Equal((Verdict.Avoid, "일반"), (otherHero.Verdict, otherHero.Reason));
    }

    [Fact]
    public void 중복된_조건_키는_오류다()
    {
        const string json = """
        { "version": 1, "rules": [ { "when": { "ally": ["루루"], "ally": ["미미"] }, "chip": "손풍기", "verdict": "avoid", "why": "x" } ] }
        """;

        var load = RuleBook.Load(json, Names);

        Assert.Empty(load.Rules.Rules);
        Assert.Contains(load.Errors, e => e.Contains("rules[0].when.ally") && e.Contains("중복"));
    }

    [Fact]
    public void 버전이_없으면_예외다() =>
        Assert.Throws<RuleFormatException>(() => RuleBook.Load("{}", Names));

    [Fact]
    public void BOM이_붙어도_읽는다() =>
        Assert.Empty(RuleBook.Load("\uFEFF{ \"version\": 1 }", Names).Errors);
}
