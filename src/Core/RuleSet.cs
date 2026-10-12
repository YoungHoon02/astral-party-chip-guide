namespace ChipGuide.Core;

public sealed record Condition(
    IReadOnlySet<int>? Owned = null,
    IReadOnlySet<int>? Ally = null,
    IReadOnlySet<int>? Map = null,
    IReadOnlySet<int>? Character = null,
    IReadOnlySet<Role>? Roles = null)
{
    public bool Matches(Situation situation, Role? role) =>
        (Owned is null || Owned.Overlaps(situation.OwnedRelics))
        && (Ally is null || Ally.Overlaps(situation.AllyHeroes))
        && (Map is null || Map.Contains(situation.MapId))
        && (Character is null || Character.Contains(situation.HeroId))
        && (Roles is null || role is { } known && Roles.Contains(known));
}

public sealed record ChipRule(
    IReadOnlySet<int> RelicIds,
    Verdict Verdict,
    string Why,
    int Specificity,
    Condition When,
    Grade? MinGrade = null);

public sealed record RuleSet(IReadOnlyList<ChipRule> Rules, IReadOnlyDictionary<int, Role> Roles)
{
    public static RuleSet Empty { get; } = new(Array.Empty<ChipRule>(), new Dictionary<int, Role>());
}
