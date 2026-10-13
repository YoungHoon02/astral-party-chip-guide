namespace ChipGuide.Core;

public enum Grade { None = 0, Blue = 1, Purple = 2, Orange = 3 }

// 같은 구체성 단계에서는 값이 큰 판정이 이긴다(보류 > 코어 > 무난).
public enum Verdict { Good = 1, Core = 2, Avoid = 3 }

public enum Role { Dealer, Support, Flex }

public sealed record ChipOption(int RelicId, Grade Grade);

public sealed record Situation(int HeroId, IReadOnlySet<int> OwnedRelics, IReadOnlySet<int> AllyHeroes, int MapId, IReadOnlySet<int> AllyRelics);

public sealed record Note(Verdict Verdict, string Why);

public sealed record Advice(ChipOption Option, Verdict Verdict, string Reason, IReadOnlyList<Note> Notes);
