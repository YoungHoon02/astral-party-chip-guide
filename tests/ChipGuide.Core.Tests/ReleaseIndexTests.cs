using System.Text;
using ChipGuide.Updater;

namespace ChipGuide.Core.Tests;

public class ReleaseIndexTests
{
    private static readonly Version Running = new(0, 1, 0, 0);

    private static byte[] Index(string version, string? url = null, int schema = 1, string? sha = null, long size = 1) =>
        Encoding.UTF8.GetBytes(
            $"{{\"schemaVersion\":{schema},\"version\":\"{version}\",\"downloadUrl\":\""
            + (url ?? $"https://github.com/{ReleaseIndex.Repository}/releases/download/v{version}/AstralPartyChipGuide-v{version}.zip")
            + $"\",\"sha256\":\"{sha ?? new string('a', 64)}\",\"size\":{size}}}");

    [Fact]
    public void U1_더_새_버전이면_알린다() =>
        Assert.Equal("0.1.1", ReleaseIndex.Newer(Index("0.1.1"), Running));

    [Fact]
    public void U2_같은_버전이면_알리지_않는다() =>
        Assert.Null(ReleaseIndex.Newer(Index("0.1.0"), Running));

    [Fact]
    public void U3_다른_주소를_가리키면_무시한다() =>
        Assert.Equal("unexpected download url",
            Assert.Throws<InvalidDataException>(() => ReleaseIndex.Newer(Index("0.2.0", url: "https://example.com/x.zip"), Running)).Message);

    [Fact]
    public void U4_모르는_스키마는_무시한다() =>
        Assert.Equal("unknown index schema",
            Assert.Throws<InvalidDataException>(() => ReleaseIndex.Newer(Index("0.2.0", schema: 2), Running)).Message);

    [Fact]
    public void U5_해시_형식이_틀리면_무시한다() =>
        Assert.Equal("bad package digest",
            Assert.Throws<InvalidDataException>(() => ReleaseIndex.Newer(Index("0.2.0", sha: "zz"), Running)).Message);

    [Fact]
    public void U6_베타_설치본은_같은_번호의_정식판을_알린다()
    {
        Assert.Equal("0.1.0", ReleaseIndex.Newer(Index("0.1.0"), Running, prerelease: true));
        Assert.Null(ReleaseIndex.Newer(Index("0.0.9"), Running, prerelease: true));
    }

    [Theory]
    [InlineData("0.1.0-beta.1+1f1000b", "0.1.0-beta.1")]
    [InlineData("0.1.0-rc.2", "0.1.0-rc.2")]
    [InlineData("0.1.0+1f1000b", null)]
    [InlineData("0.1.0", null)]
    [InlineData("0.2.0-beta.1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void U7_제품_버전에서_베타_꼬리표를_읽는다(string? product, string? expected) =>
        Assert.Equal(expected, ReleaseIndex.Prerelease(product, Running));
}
