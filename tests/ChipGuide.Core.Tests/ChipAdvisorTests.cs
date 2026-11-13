namespace ChipGuide.Core.Tests;

public class ChipAdvisorTests
{
    private const int Papara = 1, Megas = 2, Mimi = 3, Lulu = 4, Dealer = 9;
    private const int Adrenaline = 100, FanBlue = 200, FanPurple = 201, Cutter = 300, Glove = 400;
    private const int Souri = 10;
    private const int Adr = 110, AdrHigh = 111, Whet = 112, Buffer = 120, Kit = 121, Battery = 140, Carp = 150, Mark = 160;

    private static HashSet<int> Ids(params int[] ids) => new(ids);

    private static Situation At(int hero, int[]? owned = null, int[]? allies = null, int map = 0, int[]? allyOwned = null) =>
        new(hero, Ids(owned ?? Array.Empty<int>()), Ids(allies ?? Array.Empty<int>()), map, Ids(allyOwned ?? Array.Empty<int>()));

    private static ChipRule ForCharacter(int hero, int[] chips, Verdict verdict, string why, Grade? min = null) =>
        new(Ids(chips), verdict, why, 2, new Condition(Character: Ids(hero)), min);

    private static RuleSet Rules(params ChipRule[] rules) => new(rules, new Dictionary<int, Role>());

    private static RuleSet Builds(IEnumerable<BuildDef> builds, params ChipRule[] extra)
    {
        var defs = builds.ToList();
        var rules = new List<ChipRule> { ForCharacter(Souri, new[] { Battery }, Verdict.Core, "공용 코어") };
        rules.AddRange(defs.Select(b => new ChipRule(b.Core, Verdict.Core, $"{b.Letter} 코어", 2, new Condition(Character: Ids(Souri)), null, b.Letter)));
        rules.AddRange(extra);
        return new RuleSet(rules, new Dictionary<int, Role> { [Souri] = Role.Dealer }, new Dictionary<int, IReadOnlyList<BuildDef>> { [Souri] = defs });
    }

    private static readonly BuildDef A = new('A', "아드 반격", Ids(Adr, AdrHigh, Whet));
    private static readonly BuildDef B = new('B', "힐 탱커", Ids(Buffer, Kit));
    private static readonly BuildDef C = new('C', "표식 에너지", Ids(Mark));

    private static IReadOnlyList<Advice?> Judge(RuleSet rules, Situation at, params int[] chips) =>
        ChipAdvisor.Advise(rules, at, chips.Select(id => new ChipOption(id, Grade.Purple)).ToList());

    [Fact]
    public void 더_구체적인_규칙이_이긴다()
    {
        var rules = Rules(
            ForCharacter(Papara, new[] { Adrenaline }, Verdict.Core, "파파라 코어"),
            new ChipRule(Ids(Adrenaline), Verdict.Avoid, "아군 회복이 아드 조건을 깸", 3, new Condition(Ally: Ids(Lulu))));

        var advice = ChipAdvisor.Advise(rules, At(Papara, allies: new[] { Lulu }), new[] { new ChipOption(Adrenaline, Grade.Purple) });

        var result = Assert.Single(advice)!;
        Assert.Equal(Verdict.Avoid, result.Verdict);
        Assert.Equal("아군 회복이 아드 조건을 깸", result.Reason);
        Assert.Equal(new[] { new Note(Verdict.Core, "파파라 코어") }, result.Notes);
        Assert.Empty(result.Builds);
    }

    [Fact]
    public void 더_구체적인_규칙은_판정_값이_낮아도_이긴다()
    {
        var roles = new Dictionary<int, Role> { [Papara] = Role.Dealer };
        var rules = new RuleSet(new[]
        {
            new ChipRule(Ids(Glove), Verdict.Avoid, "딜러 공용 보류", 1, new Condition(Roles: new HashSet<Role> { Role.Dealer, Role.Flex })),
            ForCharacter(Papara, new[] { Glove }, Verdict.Core, "파파라 코어"),
        }, roles);

        var advice = ChipAdvisor.Advise(rules, At(Papara), new[] { new ChipOption(Glove, Grade.Purple) });

        Assert.Equal(Verdict.Core, advice[0]!.Verdict);
        Assert.Equal("파파라 코어", advice[0]!.Reason);
        Assert.Equal(new[] { new Note(Verdict.Avoid, "딜러 공용 보류") }, advice[0]!.Notes);
    }

    [Fact]
    public void 등급이_모자라면_권장_등급을_알린다()
    {
        var rules = Rules(ForCharacter(Megas, new[] { FanBlue, FanPurple }, Verdict.Core, "메가스 코어", Grade.Purple));

        var advice = ChipAdvisor.Advise(rules, At(Megas),
            new[] { new ChipOption(FanBlue, Grade.Blue), new ChipOption(FanPurple, Grade.Purple) });

        Assert.Equal(Verdict.Avoid, advice[0]!.Verdict);
        Assert.Equal("보라 이상 권장", advice[0]!.Reason);
        Assert.Equal(Verdict.Core, advice[1]!.Verdict);
    }

    [Fact]
    public void 등급_없는_칩은_최소_등급으로_깎지_않는다()
    {
        var rules = Rules(ForCharacter(Megas, new[] { FanBlue }, Verdict.Core, "메가스 코어", Grade.Purple));

        var advice = ChipAdvisor.Advise(rules, At(Megas), new[] { new ChipOption(FanBlue, Grade.None) });

        Assert.Equal(Verdict.Core, advice[0]!.Verdict);
    }

    [Fact]
    public void 걸리는_규칙이_없으면_판정하지_않는다()
    {
        var advice = ChipAdvisor.Advise(Rules(), At(Mimi), new[] { new ChipOption(999, Grade.Blue) });

        Assert.Null(advice[0]);
    }

    [Fact]
    public void 보유_칩_조건이_역할_기본값보다_우선한다()
    {
        var roles = new Dictionary<int, Role> { [Dealer] = Role.Dealer };
        var rules = new RuleSet(new[]
        {
            new ChipRule(Ids(Cutter), Verdict.Good, "딜러 공용 무난", 1, new Condition(Roles: new HashSet<Role> { Role.Dealer, Role.Flex })),
            new ChipRule(Ids(Cutter), Verdict.Core, "아드와 세트", 3, new Condition(Owned: Ids(Adrenaline))),
        }, roles);

        var advice = ChipAdvisor.Advise(rules, At(Dealer, owned: new[] { Adrenaline }), new[] { new ChipOption(Cutter, Grade.Purple) });

        Assert.Equal(Verdict.Core, advice[0]!.Verdict);
        Assert.Equal("아드와 세트", advice[0]!.Reason);
    }

    [Fact]
    public void 조건부_무난은_낮은_단계의_코어를_내리지_않는다()
    {
        var rules = Rules(
            ForCharacter(Mimi, new[] { Glove }, Verdict.Core, "미미 코어"),
            new ChipRule(Ids(Glove), Verdict.Good, "맵 무난", 3, new Condition(Map: Ids(7))));

        var advice = ChipAdvisor.Advise(rules, At(Mimi, map: 7), new[] { new ChipOption(Glove, Grade.Blue) });

        Assert.Equal(Verdict.Core, advice[0]!.Verdict);
        Assert.Equal("미미 코어", advice[0]!.Reason);
        Assert.Equal(new[] { new Note(Verdict.Good, "맵 무난") }, advice[0]!.Notes);
    }

    [Fact]
    public void 조건부_주의_무난은_낮은_단계의_코어를_이긴다()
    {
        var rules = Rules(
            ForCharacter(Mimi, new[] { Glove }, Verdict.Core, "미미 코어"),
            new ChipRule(Ids(Glove), Verdict.Good, "주의: 일부 턴에만 효과", 3, new Condition(Map: Ids(7))));

        var advice = ChipAdvisor.Advise(rules, At(Mimi, map: 7), new[] { new ChipOption(Glove, Grade.Blue) });

        Assert.Equal(Verdict.Good, advice[0]!.Verdict);
        Assert.Equal("주의: 일부 턴에만 효과", advice[0]!.Reason);
    }

    [Fact]
    public void 캐릭터_항목의_무난은_역할_코어를_이긴다()
    {
        var roles = new Dictionary<int, Role> { [Megas] = Role.Flex };
        var rules = new RuleSet(new[]
        {
            new ChipRule(Ids(Glove), Verdict.Core, "딜러 공용 코어", 1, new Condition(Roles: new HashSet<Role> { Role.Dealer, Role.Flex })),
            ForCharacter(Megas, new[] { Glove }, Verdict.Good, "공격력 칩은 핵심이 아님"),
        }, roles);

        var advice = ChipAdvisor.Advise(rules, At(Megas), new[] { new ChipOption(Glove, Grade.Blue) });

        Assert.Equal(Verdict.Good, advice[0]!.Verdict);
    }

    [Fact]
    public void 같은_단계에서는_보류이_이긴다()
    {
        var rules = Rules(
            ForCharacter(Mimi, new[] { Glove }, Verdict.Core, "코어"),
            ForCharacter(Mimi, new[] { Glove }, Verdict.Avoid, "보류"));

        var advice = ChipAdvisor.Advise(rules, At(Mimi), new[] { new ChipOption(Glove, Grade.Blue) });

        Assert.Equal(Verdict.Avoid, advice[0]!.Verdict);
    }

    [Fact]
    public void 역할을_모르면_역할_규칙은_건너뛴다()
    {
        var rules = new RuleSet(new[]
        {
            new ChipRule(Ids(Glove), Verdict.Good, "공용 무난", 0, new Condition()),
            new ChipRule(Ids(Glove), Verdict.Avoid, "서포터 공용 보류", 1, new Condition(Roles: new HashSet<Role> { Role.Support, Role.Flex })),
        }, new Dictionary<int, Role>());

        var advice = ChipAdvisor.Advise(rules, At(Mimi), new[] { new ChipOption(Glove, Grade.Blue) });

        Assert.Equal(Verdict.Good, advice[0]!.Verdict);
    }

    [Fact]
    public void 플렉스_캐릭터는_서포터_기본값도_받는다()
    {
        var roles = new Dictionary<int, Role> { [Megas] = Role.Flex };
        var rules = new RuleSet(new[]
        {
            new ChipRule(Ids(Glove), Verdict.Good, "서포터 공용 무난", 1, new Condition(Roles: new HashSet<Role> { Role.Support, Role.Flex })),
        }, roles);

        var advice = ChipAdvisor.Advise(rules, At(Megas), new[] { new ChipOption(Glove, Grade.Blue) });

        Assert.Equal(Verdict.Good, advice[0]!.Verdict);
    }

    [Fact]
    public void 참고는_중복을_빼고_최대_3개다()
    {
        var rules = Rules(
            ForCharacter(Mimi, new[] { Glove }, Verdict.Avoid, "최우선"),
            ForCharacter(Mimi, new[] { Glove }, Verdict.Core, "a"),
            ForCharacter(Mimi, new[] { Glove }, Verdict.Core, "a"),
            ForCharacter(Mimi, new[] { Glove }, Verdict.Good, "b"),
            ForCharacter(Mimi, new[] { Glove }, Verdict.Good, "c"),
            ForCharacter(Mimi, new[] { Glove }, Verdict.Good, "d"));

        var advice = ChipAdvisor.Advise(rules, At(Mimi), new[] { new ChipOption(Glove, Grade.Blue) });

        Assert.Equal(3, advice[0]!.Notes.Count);
        Assert.Equal(new[] { "a", "b", "c" }, advice[0]!.Notes.Select(n => n.Why));
    }

    [Fact]
    public void 코어_칩은_상황에서_최종_판정이_코어인_칩만_고른다()
    {
        var rules = Rules(
            ForCharacter(Papara, new[] { Adrenaline, Cutter }, Verdict.Core, "파파라 코어"),
            ForCharacter(Mimi, new[] { Glove }, Verdict.Core, "미미 코어"),
            new ChipRule(Ids(Adrenaline), Verdict.Avoid, "아군 회복이 아드 조건을 깸", 3, new Condition(Ally: Ids(Lulu))));

        Assert.Equal(new[] { Adrenaline, Cutter }, ChipAdvisor.CoreChips(rules, At(Papara)));
        Assert.Equal(new[] { Cutter }, ChipAdvisor.CoreChips(rules, At(Papara, allies: new[] { Lulu })));
    }

    [Fact]
    public void 갈림길_전에는_모든_빌드_코어가_코어이고_글자가_붙는다()
    {
        var advice = Judge(Builds(new[] { A, B }), At(Souri), Adr, Buffer, Battery);

        Assert.Equal(Verdict.Core, advice[0]!.Verdict);
        Assert.Equal(new[] { 'A' }, advice[0]!.Builds);
        Assert.Equal(Verdict.Core, advice[1]!.Verdict);
        Assert.Equal(new[] { 'B' }, advice[1]!.Builds);
        Assert.Equal(Verdict.Core, advice[2]!.Verdict);
        Assert.Empty(advice[2]!.Builds);
    }

    [Fact]
    public void 한_빌드_칩을_가지면_다른_빌드_코어는_보류가_되고_글자가_없다()
    {
        var advice = Judge(Builds(new[] { A, B }), At(Souri, owned: new[] { Whet }), Buffer, Adr);

        Assert.Equal(Verdict.Avoid, advice[0]!.Verdict);
        Assert.Equal("코어 A를 골라 보류 (B 힐 탱커 빌드 칩)", advice[0]!.Reason);
        Assert.Empty(advice[0]!.Builds);
        Assert.Contains(new Note(Verdict.Core, "B 코어"), advice[0]!.Notes);
        Assert.Equal(Verdict.Core, advice[1]!.Verdict);
        Assert.Equal(new[] { 'A' }, advice[1]!.Builds);
    }

    [Fact]
    public void 다른_등급을_가져도_빌드를_고른_것으로_본다()
    {
        var advice = Judge(Builds(new[] { A, B }), At(Souri, owned: new[] { AdrHigh }), Buffer);

        Assert.Equal(Verdict.Avoid, advice[0]!.Verdict);
    }

    [Fact]
    public void 두_빌드_칩을_모두_가지면_둘_다_열리고_나머지만_닫힌다()
    {
        var advice = Judge(Builds(new[] { A, B, C }), At(Souri, owned: new[] { Whet, Kit }), Adr, Buffer, Mark);

        Assert.Equal(new[] { 'A' }, advice[0]!.Builds);
        Assert.Equal(new[] { 'B' }, advice[1]!.Builds);
        Assert.Equal((Verdict.Avoid, "코어 A·B를 골라 보류 (C 표식 에너지 빌드 칩)"), (advice[2]!.Verdict, advice[2]!.Reason));
    }

    [Fact]
    public void 빌드와_상관없이_코어인_칩은_덮어쓰지_않는다()
    {
        var withExtras = new BuildDef('A', "아드 반격", Ids(Adr, Glove, Carp));
        var rules = Builds(new[] { withExtras, B },
            new ChipRule(Ids(Glove), Verdict.Core, "딜러 공용 코어", 1, new Condition(Roles: new HashSet<Role> { Role.Dealer })),
            new ChipRule(Ids(Carp), Verdict.Core, "맵 코어", 4, new Condition(Character: Ids(Souri), Map: Ids(7))));

        var advice = Judge(rules, At(Souri, owned: new[] { Kit }, map: 7), Glove, Carp, Adr);

        Assert.Equal((Verdict.Core, "딜러 공용 코어"), (advice[0]!.Verdict, advice[0]!.Reason));
        Assert.Empty(advice[0]!.Builds);
        Assert.Equal((Verdict.Core, "맵 코어"), (advice[1]!.Verdict, advice[1]!.Reason));
        Assert.Empty(advice[1]!.Builds);
        Assert.Equal(Verdict.Avoid, advice[2]!.Verdict);
    }

    [Fact]
    public void 빌드_밖_조건부_무난과_보류가_걸려도_닫힌_빌드_칩은_덮어쓴다()
    {
        var rules = Builds(new[] { A, B },
            new ChipRule(Ids(Buffer), Verdict.Good, "아군 메가스와 무난", 3, new Condition(Ally: Ids(Megas))),
            new ChipRule(Ids(Kit), Verdict.Avoid, "맵 보류", 3, new Condition(Map: Ids(7))));

        var advice = Judge(rules, At(Souri, owned: new[] { Whet }, allies: new[] { Megas }, map: 7), Buffer, Kit);

        const string closed = "코어 A를 골라 보류 (B 힐 탱커 빌드 칩)";
        Assert.Equal((Verdict.Avoid, closed), (advice[0]!.Verdict, advice[0]!.Reason));
        Assert.Contains(new Note(Verdict.Good, "아군 메가스와 무난"), advice[0]!.Notes);
        Assert.Equal((Verdict.Avoid, closed), (advice[1]!.Verdict, advice[1]!.Reason));
        Assert.Contains(new Note(Verdict.Avoid, "맵 보류"), advice[1]!.Notes);
    }

    [Fact]
    public void 최종_판정이_코어가_아니면_글자를_붙이지_않는다()
    {
        var rules = Builds(new[] { A, B },
            new ChipRule(Ids(Adr), Verdict.Avoid, "아군 회복", 3, new Condition(Ally: Ids(Lulu))),
            new ChipRule(Ids(Kit), Verdict.Core, "보라 이상", 2, new Condition(Character: Ids(Souri)), Grade.Purple, 'B'));

        var ally = Judge(rules, At(Souri, allies: new[] { Lulu }), Adr);
        var blue = ChipAdvisor.Advise(rules, At(Souri), new[] { new ChipOption(Kit, Grade.Blue) });

        Assert.Equal((Verdict.Avoid, 0), (ally[0]!.Verdict, ally[0]!.Builds.Count));
        Assert.Equal((Verdict.Avoid, 0), (blue[0]!.Verdict, blue[0]!.Builds.Count));
        Assert.Equal(new[] { 'A', 'B' }, ChipAdvisor.Builds(rules, At(Souri, allies: new[] { Lulu }))!.Select(b => b.Letter));
    }

    [Fact]
    public void 제목_띠_빌드는_갈림길_전_모두_고른_뒤_열린_빌드만()
    {
        var rules = Builds(new[] { A, B });

        Assert.Null(ChipAdvisor.Builds(rules, At(Papara)));
        Assert.Equal(new[] { new BuildLine('A', "아드 반격"), new BuildLine('B', "힐 탱커") }, ChipAdvisor.Builds(rules, At(Souri)));
        Assert.Equal(new[] { new BuildLine('A', "아드 반격") }, ChipAdvisor.Builds(rules, At(Souri, owned: new[] { Whet })));
    }

    [Fact]
    public void 코어_판정은_열린_빌드_글자와_함께_코어_칩을_돌려준다()
    {
        var rules = Builds(new[] { A, B });

        var before = ChipAdvisor.CoreAdvice(rules, At(Souri)).ToDictionary(a => a.Option.RelicId, a => a.Builds.ToArray());
        var after = ChipAdvisor.CoreAdvice(rules, At(Souri, owned: new[] { Whet })).Select(a => a.Option.RelicId);

        Assert.Equal(Array.Empty<char>(), before[Battery]);
        Assert.Equal(new[] { 'B' }, before[Kit]);
        Assert.Equal(new[] { Adr, AdrHigh, Whet, Battery }, after.OrderBy(i => i));
    }
}
