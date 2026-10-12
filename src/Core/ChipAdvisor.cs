namespace ChipGuide.Core;

public static class ChipAdvisor
{
    public const int MaxNotes = 3;

    public static IReadOnlyList<Advice?> Advise(RuleSet rules, Situation situation, IReadOnlyList<ChipOption> options)
    {
        Role? role = rules.Roles.TryGetValue(situation.HeroId, out var known) ? known : null;
        return options.Select(option => AdviseOne(rules, situation, role, option)).ToList();
    }

    public static IReadOnlyList<int> CoreChips(RuleSet rules, Situation situation)
    {
        var ids = rules.Rules.Where(rule => rule.Verdict == Verdict.Core).SelectMany(rule => rule.RelicIds).Distinct().ToList();
        // 등급 조건은 실제 선택지에서만 의미가 있어 등급 없이 판정한다.
        var advice = Advise(rules, situation, ids.Select(id => new ChipOption(id, Grade.None)).ToList());
        return ids.Where((_, i) => advice[i]?.Verdict == Verdict.Core).ToList();
    }

    public static string GradeName(Grade grade) => grade switch
    {
        Grade.Blue => "파랑",
        Grade.Purple => "보라",
        Grade.Orange => "주황",
        _ => "등급 없음",
    };

    private static Advice? AdviseOne(RuleSet rules, Situation situation, Role? role, ChipOption option)
    {
        var hits = rules.Rules
            .Where(rule => rule.RelicIds.Contains(option.RelicId) && rule.When.Matches(situation, role))
            .Select(rule => Apply(rule, option))
            .OrderByDescending(hit => hit.Specificity)
            .ThenByDescending(hit => hit.Verdict)
            .ToList();
        if (hits.Count == 0) return null;

        var top = hits[0];
        var notes = hits.Skip(1)
            .Select(hit => new Note(hit.Verdict, hit.Why))
            .Where(note => note.Why != top.Why)
            .Distinct()
            .Take(MaxNotes)
            .ToList();
        return new Advice(option, top.Verdict, top.Why, notes);
    }

    private static (int Specificity, Verdict Verdict, string Why) Apply(ChipRule rule, ChipOption option) =>
        rule.MinGrade is { } min && option.Grade != Grade.None && option.Grade < min
            ? (rule.Specificity, Verdict.Avoid, $"{GradeName(min)} 이상 권장")
            : (rule.Specificity, rule.Verdict, rule.Why);
}
