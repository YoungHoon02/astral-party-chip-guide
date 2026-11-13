namespace ChipGuide.Core;

public static class ChipAdvisor
{
    public const int MaxNotes = 3;
    public const string CautionPrefix = "주의:";
    private const int CharacterSpecificity = 2;

    public static IReadOnlyList<Advice?> Advise(RuleSet rules, Situation situation, IReadOnlyList<ChipOption> options)
    {
        Role? role = rules.Roles.TryGetValue(situation.HeroId, out var known) ? known : null;
        return options.Select(option => AdviseOne(rules, situation, role, option)).ToList();
    }

    public static IReadOnlyList<int> CoreChips(RuleSet rules, Situation situation) =>
        CoreAdvice(rules, situation).Select(advice => advice.Option.RelicId).ToList();

    public static IReadOnlyList<Advice> CoreAdvice(RuleSet rules, Situation situation)
    {
        var ids = rules.Rules.Where(rule => rule.Verdict == Verdict.Core).SelectMany(rule => rule.RelicIds).Distinct().ToList();
        // 등급 조건은 실제 선택지에서만 의미가 있어 등급 없이 판정한다.
        var advice = Advise(rules, situation, ids.Select(id => new ChipOption(id, Grade.None)).ToList());
        return advice.Where(a => a?.Verdict == Verdict.Core).Select(a => a!).ToList();
    }

    public static IReadOnlyList<BuildLine>? Builds(RuleSet rules, Situation situation)
    {
        var builds = rules.BuildsOf(situation.HeroId);
        if (builds.Count == 0) return null;
        return OpenBuilds(builds, situation).Select(build => new BuildLine(build.Letter, build.Label)).ToList();
    }

    public static string GradeName(Grade grade) => grade switch
    {
        Grade.Blue => "파랑",
        Grade.Purple => "보라",
        Grade.Orange => "주황",
        _ => "등급 없음",
    };

    private static IReadOnlyList<BuildDef> OpenBuilds(IReadOnlyList<BuildDef> builds, Situation situation)
    {
        var open = builds.Where(build => build.Core.Overlaps(situation.OwnedRelics)).ToList();
        return open.Count > 0 ? open : builds;
    }

    private static Advice? AdviseOne(RuleSet rules, Situation situation, Role? role, ChipOption option)
    {
        var hits = rules.Rules
            .Where(rule => rule.RelicIds.Contains(option.RelicId) && rule.When.Matches(situation, role))
            .Select(rule => (Hit: Apply(rule, option), rule.Build))
            .ToList();
        if (hits.Count == 0) return null;

        var all = Sort(hits.Select(h => h.Hit));
        var top = Resolve(all)!.Value;
        var builds = rules.BuildsOf(situation.HeroId);
        var open = OpenBuilds(builds, situation);
        var member = builds.Where(build => build.Core.Contains(option.RelicId)).ToList();
        var closed = member.Where(build => !open.Contains(build)).ToList();
        var notes = all;

        // 빌드와 무관하게 코어인 칩은 닫힌 빌드 소속이어도 덮어쓰지 않아서, 빌드 규칙 없이 한 번 더 판정한다.
        if (member.Count > 0 && member.All(closed.Contains)
            && Resolve(Sort(hits.Where(h => h.Build == null).Select(h => h.Hit)))?.Verdict != Verdict.Core)
        {
            var reason = $"코어 {string.Join("·", open.Select(build => build.Letter))}를 골라 보류 ({string.Join("·", closed.Select(build => $"{build.Letter} {build.Label}"))} 빌드 칩)";
            notes = all.Prepend(top).ToList();
            top = (CharacterSpecificity, Verdict.Avoid, reason);
        }

        var letters = top.Verdict == Verdict.Core
            ? member.Where(open.Contains).Select(build => build.Letter).OrderBy(letter => letter).ToList()
            : new List<char>();
        var noteList = notes.Where(hit => hit != top)
            .Select(hit => new Note(hit.Verdict, hit.Why))
            .Where(note => note.Why != top.Why)
            .Distinct()
            .Take(MaxNotes)
            .ToList();
        return new Advice(option, top.Verdict, top.Why, noteList, letters);
    }

    private static List<(int Specificity, Verdict Verdict, string Why)> Sort(IEnumerable<(int Specificity, Verdict Verdict, string Why)> hits) =>
        hits.OrderByDescending(hit => hit.Specificity).ThenByDescending(hit => hit.Verdict).ToList();

    private static (int Specificity, Verdict Verdict, string Why)? Resolve(List<(int Specificity, Verdict Verdict, string Why)> hits)
    {
        if (hits.Count == 0) return null;
        var top = hits[0];
        // 조건부 규칙의 무난은 덧붙는 정보라서, 주의가 아니면 더 낮은 단계에서 정해진 코어를 내리지 않는다.
        if (top.Verdict == Verdict.Good && top.Specificity > CharacterSpecificity && !top.Why.StartsWith(CautionPrefix, StringComparison.Ordinal)
            && hits.FindIndex(hit => hit.Verdict == Verdict.Core) is var coreIndex and >= 0)
            top = hits[coreIndex];
        return top;
    }

    private static (int Specificity, Verdict Verdict, string Why) Apply(ChipRule rule, ChipOption option) =>
        rule.MinGrade is { } min && option.Grade != Grade.None && option.Grade < min
            ? (rule.Specificity, Verdict.Avoid, $"{GradeName(min)} 이상 권장")
            : (rule.Specificity, rule.Verdict, rule.Why);
}
