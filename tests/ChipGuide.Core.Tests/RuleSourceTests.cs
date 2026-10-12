namespace ChipGuide.Core.Tests;

public class RuleSourceTests
{
    private const string Rules = """{ "version": 1, "characters": { "미미": { "core": ["손풍기"] } } }""";

    private readonly List<string> _log = new();

    private RuleSource Source(Func<string?> rules, Func<string?>? names = null) =>
        new(names ?? (() => NameTableTests.Sample), rules, _log.Add);

    [Fact]
    public void 성공하면_규칙과_버전이_바뀐다()
    {
        var source = Source(() => Rules);

        Assert.True(source.Reload());
        Assert.Single(source.Current.Rules);
        Assert.Equal(1, source.Version);
    }

    [Fact]
    public void 깨진_파일이면_이전_규칙을_유지한다()
    {
        var text = Rules;
        var source = Source(() => text);
        source.Reload();

        text = "{ \"version\": 1, ";
        Assert.False(source.Reload());

        Assert.Single(source.Current.Rules);
        Assert.Equal(1, source.Version);
        Assert.Contains(_log, line => line.Contains("이전 규칙을 유지"));
    }

    [Fact]
    public void 파일이_잠겨_있으면_이전_규칙을_유지한다()
    {
        var locked = false;
        var source = Source(() => locked ? throw new IOException("다른 프로세스가 사용 중") : Rules);
        source.Reload();

        locked = true;
        Assert.False(source.Reload());
        Assert.Single(source.Current.Rules);
    }

    [Fact]
    public void 접근이_거부되면_이전_규칙을_유지한다()
    {
        var denied = false;
        var source = Source(() => denied ? throw new UnauthorizedAccessException("접근 거부") : Rules);
        source.Reload();

        denied = true;
        Assert.False(source.Reload());
        Assert.Single(source.Current.Rules);
        Assert.Equal(1, source.Version);
        Assert.Contains(_log, line => line.Contains("이전 규칙을 유지"));
    }

    [Fact]
    public void 실패한_뒤_다시_읽으면_새_규칙으로_바뀐다()
    {
        var text = Rules;
        var source = Source(() => text);
        source.Reload();

        text = "{ \"version\": 1, ";
        Assert.False(source.Reload());
        Assert.Equal(1, source.Version);

        text = """{ "version": 1, "characters": { "미미": { "core": ["손풍기", "커터칼"] } } }""";
        Assert.True(source.Reload());
        Assert.Equal(2, source.Version);
        Assert.Equal(2, source.Current.Rules.Count);
    }

    [Fact]
    public void 파일이_없으면_안내만_남긴다()
    {
        var source = Source(() => Rules, names: () => null);

        Assert.False(source.Reload());
        Assert.Empty(source.Current.Rules);
        Assert.Contains(_log, line => line.Contains("names.json"));
    }

    [Fact]
    public void 오류가_많으면_20건까지만_남기고_요약한다()
    {
        var chips = string.Join(",", Enumerable.Range(0, 30).Select(i => $"\"없는칩{i}\""));
        var source = Source(() => $$"""{ "version": 1, "characters": { "미미": { "core": [{{chips}}] } } }""");

        source.Reload();

        Assert.Equal(RuleSource.MaxErrorsLogged, _log.Count(line => line.Contains("없는칩")));
        Assert.Contains(_log, line => line.Contains("10건"));
    }
}
