namespace ChipGuide.Core.Tests;

public class RoleWeightsTests
{
    private static ChipRole Role(RoleWeights added, params RoleKind[] roles) => new(roles.ToHashSet(), added);

    [Fact]
    public void 추가_가중치가_있으면_역할군과_값을_게임_순서로_보여준다()
    {
        var cutter = Role(new RoleWeights(0, 10, 30, 0), RoleKind.Attack, RoleKind.Tank);

        Assert.Equal("방어 +10 · 공격 +30", cutter.Label());
    }

    [Fact]
    public void 추가_가중치가_없으면_역할군_이름만_보여준다()
    {
        Assert.Equal("방어", Role(RoleWeights.Zero, RoleKind.Tank).Label());
        Assert.Equal("", ChipRole.None.Label());
    }

    [Fact]
    public void 게임_역할군_번호를_역할군으로_바꾼다()
    {
        Assert.Equal(RoleKind.Attack, RoleWeights.FromTag(2));
        Assert.Equal(RoleKind.Card, RoleWeights.FromTag(3));
        Assert.Equal(RoleKind.Support, RoleWeights.FromTag(4));
        Assert.Equal(RoleKind.Tank, RoleWeights.FromTag(5));
        Assert.Null(RoleWeights.FromTag(1));
    }

    [Fact]
    public void 가진_칩의_추가_가중치를_기본_가중치에_더한다()
    {
        var mimi = new RoleWeights(80, 15, 15, 30);

        Assert.Equal(new RoleWeights(90, 15, 15, 40), mimi.Add(new RoleWeights(10, 0, 0, 10)));
    }
}
