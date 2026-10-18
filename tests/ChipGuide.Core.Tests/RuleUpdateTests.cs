namespace ChipGuide.Core.Tests;

public class RuleUpdateTests
{
    private const string Old = "// 출처\n{ \"version\": 1 }\n";
    private const string New = "// 출처\n{ \"version\": 1, \"roles\": {} }\n";

    [Fact]
    public void 파일이_없으면_머리줄을_붙여_설치한다()
    {
        var plan = RuleUpdate.Plan(null, New, "0.3.2");

        Assert.Equal(RuleUpdateAction.Install, plan.Action);
        Assert.False(plan.Backup);
        Assert.StartsWith($"// 칩 가이드 v0.3.2 기본 규칙 [{RuleUpdate.Fingerprint(New)}]", plan.Content);
        Assert.EndsWith(New, plan.Content);
    }

    [Fact]
    public void 같은_기본_규칙이면_고친_내용을_그대로_둔다()
    {
        var installed = RuleUpdate.Plan(null, New, "0.3.2").Content.Replace("\"roles\": {}", "\"roles\": { \"common\": {} }");

        var plan = RuleUpdate.Plan(installed, New, "0.3.2");

        Assert.Equal(RuleUpdateAction.Keep, plan.Action);
        Assert.Equal(installed, plan.Content);
    }

    [Fact]
    public void 기본_규칙이_바뀌면_고치지_않은_파일은_백업_없이_바꾼다()
    {
        var installed = RuleUpdate.Plan(null, Old, "0.3.1").Content;

        var plan = RuleUpdate.Plan(installed, New, "0.3.2");

        Assert.Equal(RuleUpdateAction.Replace, plan.Action);
        Assert.False(plan.Backup);
        Assert.EndsWith(New, plan.Content);
    }

    [Fact]
    public void 기본_규칙이_바뀌면_고친_파일은_백업하고_바꾼다()
    {
        var installed = RuleUpdate.Plan(null, Old, "0.3.1").Content.Replace("\"version\": 1", "\"version\": 1, \"rules\": []");

        var plan = RuleUpdate.Plan(installed, New, "0.3.2");

        Assert.Equal(RuleUpdateAction.Replace, plan.Action);
        Assert.True(plan.Backup);
    }

    [Fact]
    public void 머리줄_없는_예전_파일은_내용이_다를_때만_백업한다()
    {
        Assert.False(RuleUpdate.Plan(New, New, "0.3.2").Backup);
        Assert.Equal(RuleUpdateAction.Replace, RuleUpdate.Plan(New, New, "0.3.2").Action);

        var plan = RuleUpdate.Plan(Old, New, "0.3.2");
        Assert.Equal(RuleUpdateAction.Replace, plan.Action);
        Assert.True(plan.Backup);
    }

    [Fact]
    public void 줄바꿈과_BOM만_다르면_같은_규칙으로_본다()
    {
        var installed = "﻿" + RuleUpdate.Plan(null, New, "0.3.2").Content.Replace("\n", "\r\n");

        Assert.Equal(RuleUpdateAction.Keep, RuleUpdate.Plan(installed, New.Replace("\n", "\r\n"), "0.3.2").Action);
    }

    [Fact]
    public void 설치한_내용은_규칙으로_읽힌다()
    {
        var shipped = File.ReadAllText(ShippedRulesTests.RulesPath);
        var content = RuleUpdate.Plan(null, shipped, "0.3.2").Content;

        var load = RuleBook.Load(content, ShippedRulesTests.Names);

        Assert.Empty(load.Errors);
    }
}
