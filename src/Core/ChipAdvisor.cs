namespace ChipGuide.Core;

public static class ChipAdvisor
{
    public const int MaxNotes = 3;
    public const string CautionPrefix = "주의:";
    private const int CharacterSpecificity = 2;

    private sealed record BuildState(Role? Role, IReadOnlyList<BuildDef> All, IReadOnlyList<BuildDef> Chosen, IReadOnlyList<BuildDef> Open);

    public static IReadOnlyList<Advice?> Advise(RuleSet rules, Situation situation, IReadOnlyList<ChipOption> options)
    {
        var state = StateOf(rules, situation);
        return options.Select(option => AdviseOne(rules, situation, state, option)).ToList();
    }

    public static IReadOnlyList<int> CoreChips(RuleSet rules, Situation situation) =>
        CoreAdvice(rules, situation).Select(advice => advice.Option.RelicId).ToList();

    public static IReadOnlyList<Advice> CoreAdvice(RuleSet rules, Situation situation) => CoreAdvice(rules, situation, StateOf(rules, situation));

    public static IReadOnlyList<BuildLine>? Builds(RuleSet rules, Situation situation)
    {
        var state = StateOf(rules, situation);
        if (state.All.Count == 0) return null;
        // 열림·닫힘은 가진 칩으로 정하지만, 아군·맵 때문에 코어가 하나도 안 남은 빌드를 띠에 남기면 코어 줄과 말이 어긋난다.
        var live = CoreAdvice(rules, situation, state).SelectMany(advice => advice.Builds).ToHashSet();
        return state.Open.Where(build => live.Contains(build.Letter))
            .Select(build => new BuildLine(build.Letter, build.Label)).ToList();
    }

    public static string GradeName(Grade grade) => grade switch
    {
        Grade.Blue => "파랑",
        Grade.Purple => "보라",
        Grade.Orange => "주황",
        _ => "등급 없음",
    };

    private static IReadOnlyList<Advice> CoreAdvice(RuleSet rules, Situation situation, BuildState state)
    {
        var ids = rules.Rules.Where(rule => rule.Verdict == Verdict.Core).SelectMany(rule => rule.RelicIds).Distinct();
        // 등급 조건은 실제 선택지에서만 의미가 있어 등급 없이 판정한다.
        return ids.Select(id => AdviseOne(rules, situation, state, new ChipOption(id, Grade.None)))
            .Where(advice => advice?.Verdict == Verdict.Core).Select(advice => advice!).ToList();
    }

    private static BuildState StateOf(RuleSet rules, Situation situation)
    {
        Role? role = rules.Roles.TryGetValue(situation.HeroId, out var known) ? known : null;
        var all = rules.BuildsOf(situation.HeroId);
        var picked = all.Where(build => build.Core.Overlaps(situation.OwnedRelics)).ToList();
        var chosen = picked.Count > 0 ? picked : all;
        var state = new BuildState(role, all, chosen, chosen);
        if (situation.KeywordOf is not { } keywordOf || chosen.Count < 2) return state;

        // 가진 키워드에 빌드 코어 키워드를 더해 두 종을 넘으면 그 빌드 칩은 더 나오지 않는다.
        // 공용 코어 키워드도 지켜야 할 몫으로 세지만, 이미 두 종을 다 가져 공용 코어가 막혔으면 따지지 않는다.
        var core = CoreAdvice(rules, situation, state);
        IReadOnlySet<Keyword> KeywordsOf(IEnumerable<Advice> advice) => KeywordLock.Owned(advice.Select(a => keywordOf(a.Option.RelicId)));
        var owned = KeywordLock.Owned(situation.OwnedRelics.Select(keywordOf));
        var withShared = owned.Union(KeywordsOf(core.Where(advice => advice.Builds.Count == 0))).ToHashSet();
        var kept = withShared.Count <= KeywordLock.Limit ? withShared : owned;
        var reachable = chosen
            .Where(build => kept.Union(KeywordsOf(core.Where(advice => advice.Builds.Contains(build.Letter)))).Count() <= KeywordLock.Limit)
            .ToList();
        // 모든 빌드가 막히면 어느 쪽을 버릴지 알 수 없으니 그대로 둔다.
        return reachable.Count > 0 ? state with { Open = reachable } : state;
    }

    private static Advice? AdviseOne(RuleSet rules, Situation situation, BuildState state, ChipOption option)
    {
        var hits = rules.Rules
            .Where(rule => rule.RelicIds.Contains(option.RelicId) && rule.When.Matches(situation, state.Role))
            .Select(rule => (Hit: Apply(rule, option), rule.Build))
            .ToList();
        if (hits.Count == 0) return null;

        var all = Sort(hits.Select(h => h.Hit));
        var top = Resolve(all)!.Value;
        var open = state.Open;
        var member = state.All.Where(build => build.Core.Contains(option.RelicId)).ToList();
        var closed = member.Where(build => !open.Contains(build)).ToList();
        var notes = all;

        // 빌드와 무관하게 코어인 칩은 닫힌 빌드 소속이어도 덮어쓰지 않아서, 빌드 규칙 없이 한 번 더 판정한다.
        if (member.Count > 0 && member.All(closed.Contains))
        {
            if (Resolve(Sort(hits.Where(h => h.Build == null).Select(h => h.Hit)))?.Verdict == Verdict.Core)
            {
                // 닫힌 빌드의 코어 규칙이 이유로 남으면 고르지 않은 빌드 때문에 코어인 것처럼 보여서, 닫힌 빌드 규칙만 빼고 다시 정한다.
                top = Resolve(Sort(hits.Where(h => h.Build is not { } letter || open.Any(build => build.Letter == letter)).Select(h => h.Hit)))!.Value;
            }
            else
            {
                var locked = closed.Where(state.Chosen.Contains).ToList();
                var reason = locked.Count > 0
                    ? $"키워드 {KeywordLock.Limit}종 제한으로 갈 수 없는 빌드라 보류 ({Describe(locked)} 빌드 칩)"
                    : $"코어 {string.Join("·", state.Chosen.Select(build => build.Letter))}를 골라 보류 ({Describe(closed)} 빌드 칩)";
                notes = all.Prepend(top).ToList();
                top = (CharacterSpecificity, Verdict.Avoid, reason);
            }
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

    private static string Describe(IEnumerable<BuildDef> builds) => string.Join("·", builds.Select(build => $"{build.Letter} {build.Label}"));

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
