namespace ChipGuide.Core;

public enum RoleKind { Support, Tank, Attack, Card }

// 캐릭터마다 역할군 기본 가중치가 있고, 칩을 고르면 그 칩의 추가 가중치가 더해진다.
// 선택지의 역할군 분포가 이 가중치를 따른다고 알려져 있으나 정확한 공식은 서버에 있다.
public sealed record RoleWeights(int Support, int Tank, int Attack, int Card)
{
    public static RoleWeights Zero { get; } = new(0, 0, 0, 0);

    public static IReadOnlyList<RoleKind> Kinds { get; } = new[] { RoleKind.Support, RoleKind.Tank, RoleKind.Attack, RoleKind.Card };

    public int this[RoleKind kind] => kind switch
    {
        RoleKind.Support => Support,
        RoleKind.Tank => Tank,
        RoleKind.Attack => Attack,
        _ => Card,
    };

    public bool IsZero => Support == 0 && Tank == 0 && Attack == 0 && Card == 0;

    public RoleWeights Add(RoleWeights other) =>
        new(Support + other.Support, Tank + other.Tank, Attack + other.Attack, Card + other.Card);

    public static string Name(RoleKind kind) => kind switch
    {
        RoleKind.Support => "지원",
        RoleKind.Tank => "방어",
        RoleKind.Attack => "공격",
        _ => "카드",
    };

    // 게임 설정의 역할군 번호: 1 = 없음, 2 = 공격, 3 = 카드, 4 = 지원, 5 = 방어
    public static RoleKind? FromTag(int tag) => tag switch
    {
        2 => RoleKind.Attack,
        3 => RoleKind.Card,
        4 => RoleKind.Support,
        5 => RoleKind.Tank,
        _ => null,
    };
}

public sealed record ChipRole(IReadOnlySet<RoleKind> Roles, RoleWeights Added)
{
    public static ChipRole None { get; } = new(new HashSet<RoleKind>(), RoleWeights.Zero);

    public string Label() => string.Join(" · ", RoleWeights.Kinds
        .Where(kind => Roles.Contains(kind) || Added[kind] > 0)
        .Select(kind => Added[kind] > 0 ? $"{RoleWeights.Name(kind)} +{Added[kind]}" : RoleWeights.Name(kind)));
}
