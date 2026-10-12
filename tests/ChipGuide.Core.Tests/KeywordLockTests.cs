namespace ChipGuide.Core.Tests;

public class KeywordLockTests
{
    private static IReadOnlySet<Keyword> Owned(params Keyword[] keywords) => KeywordLock.Owned(keywords);

    [Fact]
    public void 두번째_키워드를_고르면_고정을_경고한다()
    {
        var warning = KeywordLock.Warning(Owned(Keyword.Cure, Keyword.Cure), Keyword.Mark);

        Assert.Equal("고르면 키워드가 회복·표식으로 고정되어 별빛·에너지 칩이 더 나오지 않음", warning);
    }

    [Fact]
    public void 받침_없는_키워드로_끝나면_로를_쓴다()
    {
        var warning = KeywordLock.Warning(Owned(Keyword.Salary), Keyword.Energy);

        Assert.StartsWith("고르면 키워드가 별빛·에너지로 고정되어 회복·표식 칩이", warning);
    }

    [Theory]
    [InlineData(Keyword.None)]
    [InlineData(Keyword.Cure)]
    public void 키워드가_없거나_이미_가진_키워드면_경고하지_않는다(Keyword chip) =>
        Assert.Null(KeywordLock.Warning(Owned(Keyword.Cure), chip));

    [Fact]
    public void 첫_키워드나_이미_고정된_뒤에는_경고하지_않는다()
    {
        Assert.Null(KeywordLock.Warning(Owned(Keyword.None), Keyword.Cure));
        Assert.Null(KeywordLock.Warning(Owned(Keyword.Cure, Keyword.Mark), Keyword.Cure));
    }

    [Fact]
    public void 막히는_키워드의_코어_칩을_함께_알린다()
    {
        var core = new[] { ("휴대용 선풍기", Keyword.Mark), ("닌자 수리검", Keyword.Mark), ("휴대용 선풍기", Keyword.Mark), ("비타민 알약", Keyword.Cure) };

        var warning = KeywordLock.Warning(Owned(Keyword.Cure), Keyword.Salary, core);

        Assert.EndsWith(". 이 캐릭터의 코어 칩 휴대용 선풍기·닌자 수리검도 막힘", warning);
    }

    [Fact]
    public void 막히는_코어_칩이_없으면_키워드만_알린다()
    {
        var warning = KeywordLock.Warning(Owned(Keyword.Cure), Keyword.Mark, new[] { ("비타민 알약", Keyword.Cure), ("ATM기", Keyword.Mark) });

        Assert.Equal("고르면 키워드가 회복·표식으로 고정되어 별빛·에너지 칩이 더 나오지 않음", warning);
    }
}
