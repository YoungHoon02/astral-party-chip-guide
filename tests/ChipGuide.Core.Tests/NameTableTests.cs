namespace ChipGuide.Core.Tests;

public class NameTableTests
{
    internal const string Sample = """
    {
      "version": 1,
      "chips": [
        { "id": 200, "name": "손풍기", "base": "손풍기", "grade": "blue", "keyword": 0 },
        { "id": 201, "name": "손풍기", "base": "손풍기", "grade": "purple", "keyword": 0 },
        { "id": 300, "name": "커터칼 - 예리함", "base": "커터칼", "grade": "purple" },
        { "id": 301, "name": "커터칼 - 무딤", "base": "커터칼", "grade": "blue" },
        { "id": 100, "name": "아드레날린", "base": "아드레날린", "grade": "purple" },
        { "id": 999, "name": "", "base": "", "grade": "none" }
      ],
      "heroes": [ { "id": 1, "name": "파파라", "tag": 2 }, { "id": 4, "name": "루루", "tag": 4 }, { "id": 3, "name": "미미", "tag": 4 } ],
      "maps": [ { "id": 51, "name": "유령 골목" }, { "id": 52, "name": "유령 골목" } ]
    }
    """;

    [Fact]
    public void 같은_이름의_등급별_칩을_모두_가리킨다()
    {
        var names = NameTable.Parse(Sample);

        Assert.Equal(new[] { 200, 201 }, names.Chip("손풍기")!.OrderBy(i => i));
    }

    [Fact]
    public void 전체_이름이_우선이고_없으면_기본_이름으로_찾는다()
    {
        var names = NameTable.Parse(Sample);

        Assert.Equal(new[] { 300 }, names.Chip("커터칼 - 예리함")!);
        Assert.Equal(new[] { 300, 301 }, names.Chip("커터칼")!.OrderBy(i => i));
    }

    [Fact]
    public void 없는_이름은_null이다()
    {
        var names = NameTable.Parse(Sample);

        Assert.Null(names.Chip("가뱡"));
        Assert.Null(names.Hero("없는캐릭"));
        Assert.Null(names.Chip(""));
    }

    [Fact]
    public void 캐릭터와_맵을_찾는다()
    {
        var names = NameTable.Parse(Sample);

        Assert.Equal(new[] { 1 }, names.Hero("파파라")!);
        Assert.Equal(new[] { 51, 52 }, names.Map("유령 골목")!.OrderBy(i => i));
    }

    [Fact]
    public void 등급을_기록한다()
    {
        var names = NameTable.Parse(Sample);

        Assert.Equal(Grade.Blue, names.ChipGrades[200]);
        Assert.Equal(Grade.Purple, names.ChipGrades[201]);
    }

    [Fact]
    public void 기본_이름은_구분자_앞부분이다()
    {
        Assert.Equal("커터칼", NameTable.BaseName("커터칼 - 예리함"));
        Assert.Equal("가방", NameTable.BaseName("가방"));
    }

    [Fact]
    public void BOM과_주석과_마지막_쉼표를_허용한다()
    {
        var names = NameTable.Parse("\uFEFF{ // 주석\n \"version\": 1, \"chips\": [ { \"id\": 1, \"name\": \"가방\", \"grade\": \"purple\" }, ], }");

        Assert.NotNull(names.Chip("가방"));
    }

    [Fact]
    public void 버전이_다르면_예외다() =>
        Assert.Throws<RuleFormatException>(() => NameTable.Parse("{ \"version\": 2 }"));

    [Fact]
    public void 문법_오류는_행_번호와_함께_예외다()
    {
        var ex = Assert.Throws<RuleFormatException>(() => NameTable.Parse("{\n\"version\": 1,\n\"chips\": [ {\"id\": } ]\n}"));

        Assert.Contains("names.json 3행", ex.Message);
    }

    [Fact]
    public void 너무_크면_예외다() =>
        Assert.Throws<RuleFormatException>(() => NameTable.Parse(new string(' ', Json.MaxBytes + 1)));

    [Fact]
    public void 잘못된_유니코드_이스케이프는_형식_오류다() =>
        Assert.Throws<RuleFormatException>(() => NameTable.Parse("{ \"version\": 1, \"chips\": [ { \"id\": 1, \"name\": \"\\ud800\" } ] }"));
}
