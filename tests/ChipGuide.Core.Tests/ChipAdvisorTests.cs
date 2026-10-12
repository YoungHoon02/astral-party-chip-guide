namespace ChipGuide.Core.Tests;

public class ChipAdvisorTests
{
    private const int Papara = 1, Megas = 2, Mimi = 3, Lulu = 4, Dealer = 9;
    private const int Adrenaline = 100, FanBlue = 200, FanPurple = 201, Cutter = 300, Glove = 400;

    private static HashSet<int> Ids(params int[] ids) => new(ids);

    private static Situation At(int hero, int[]? owned = null, int[]? allies = null, int map = 0) =>
        new(hero, Ids(owned ?? Array.Empty<int>()), Ids(allies ?? Array.Empty<int>()), map);

    private static ChipRule ForCharacter(int hero, int[] chips, Verdict verdict, string why, Grade? min = null) =>
        new(Ids(chips), verdict, why, 2, new Condition(Character: Ids(hero)), min);

    private static RuleSet Rules(params ChipRule[] rules) => new(rules, new Dictionary<int, Role>());

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
    }

    [Fact]
    public void 더_구체적인_규칙은_판정_값이_낮아도_이긴다()
    {
        var roles = new Dictionary<int, Role> { [Papara] = Role.Dealer };
        var rules = new RuleSet(new[]
        {
            new ChipRule(Ids(Glove), Verdict.Avoid, "딜러 공용 비추천", 1, new Condition(Roles: new HashSet<Role> { Role.Dealer, Role.Flex })),
            ForCharacter(Papara, new[] { Glove }, Verdict.Core, "파파라 코어"),
        }, roles);

        var advice = ChipAdvisor.Advise(rules, At(Papara), new[] { new ChipOption(Glove, Grade.Purple) });

        Assert.Equal(Verdict.Core, advice[0]!.Verdict);
        Assert.Equal("파파라 코어", advice[0]!.Reason);
        Assert.Equal(new[] { new Note(Verdict.Avoid, "딜러 공용 비추천") }, advice[0]!.Notes);
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
    public void 같은_단계에서는_비추천이_이긴다()
    {
        var rules = Rules(
            ForCharacter(Mimi, new[] { Glove }, Verdict.Core, "코어"),
            ForCharacter(Mimi, new[] { Glove }, Verdict.Avoid, "비추천"));

        var advice = ChipAdvisor.Advise(rules, At(Mimi), new[] { new ChipOption(Glove, Grade.Blue) });

        Assert.Equal(Verdict.Avoid, advice[0]!.Verdict);
    }

    [Fact]
    public void 역할을_모르면_역할_규칙은_건너뛴다()
    {
        var rules = new RuleSet(new[]
        {
            new ChipRule(Ids(Glove), Verdict.Good, "공용 무난", 0, new Condition()),
            new ChipRule(Ids(Glove), Verdict.Avoid, "서포터 공용 비추천", 1, new Condition(Roles: new HashSet<Role> { Role.Support, Role.Flex })),
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
}
