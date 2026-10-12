namespace ChipGuide.Core.Tests;

public class TextSafetyTests
{
    [Fact]
    public void 서식_문자를_전각으로_바꾼다() =>
        Assert.Equal("［color=#f00］빨강［/color］ ＜b＞", TextSafety.Clean("[color=#f00]빨강[/color] <b>"));

    [Fact]
    public void 제어_문자는_공백으로_바꾸고_앞뒤를_자른다() =>
        Assert.Equal("가 나", TextSafety.Clean("  가\n나\t "));

    [Fact]
    public void 비어_있으면_빈_문자열이다()
    {
        Assert.Equal("", TextSafety.Clean(null));
        Assert.Equal("", TextSafety.Clean("   "));
    }

    [Fact]
    public void 길면_말줄임표를_붙여_최대_길이에_맞춘다()
    {
        var result = TextSafety.Clean(new string('가', 250));

        Assert.Equal(TextSafety.MaxLength, result.Length);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void 자를_때_서로게이트_쌍을_깨뜨리지_않는다()
    {
        var result = TextSafety.Clean(new string('가', 198) + "😀😀", 200);

        Assert.False(char.IsHighSurrogate(result[^2]) && !char.IsLowSurrogate(result[^1]));
        Assert.EndsWith("…", result);
    }
}
