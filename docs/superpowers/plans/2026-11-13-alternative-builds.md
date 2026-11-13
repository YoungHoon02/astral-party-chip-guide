# 분기 빌드(코어 A·B·C) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 캐릭터마다 코어 A·B·C 빌드를 두고, 갈림길 전에는 모든 빌드 코어를 글자 블럭과 함께 보여 주며, 한 빌드 칩을 가지면 다른 빌드 코어를 보류로 바꾼다.

**Architecture:** `rules.json` 캐릭터 항목의 `builds`를 `RuleBook`이 읽어 빌드 정보(`BuildDef`)와 빌드 글자가 달린 규칙(`ChipRule.Build`)을 만든다. `ChipAdvisor`는 가진 칩으로 열린 빌드를 계산하고, 지금 판정 뒤에 닫힌 빌드 덮어쓰기를 적용해 `Advice.Builds`와 제목 띠용 `BuildLine` 목록을 낸다. 플러그인은 그 결과로 배지 아래 글자 블럭, 툴팁 첫 줄, 코어 줄 글자, 제목 띠 빌드 설명을 그린다.

**Tech Stack:** C# (Core net 표준 라이브러리, 테스트 net8.0 xunit 2.9), BepInEx IL2CPP 플러그인(FairyGUI 객체를 `RuntimeObject`로 다룸)

**Spec:** `docs/superpowers/specs/2026-11-13-alternative-builds-design.md`

## Global Constraints

- 코드 주석은 복잡한 알고리즘이나 이해하기 어려운 이유("왜")에만 쓴다. 단순 코드에는 쓰지 않는다.
- 사용자에게 보이는 문구·커밋 메시지는 한국어. 커밋 형식은 `<type>: <행동 중심 요약>`이고, 변수·함수 이름보다 사용자 영향을 쓴다. 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` 줄을 붙인다.
- `rules.json`의 `version`은 1 그대로 둔다. `builds`를 모르는 이전 플러그인은 이 키를 무시해야 한다(지금 `RuleBook`은 모르는 키를 무시하므로 그대로 둔다).
- 빌드 키는 `A`, `B`, `C`만 허용한다. 빌드는 2개 이상이어야 하며, `label`은 공백을 뺀 길이가 1~10자여야 한다.
- 배지 글자는 `★ 코어` / `○ 무난` / `× 보류` 그대로다. 빌드 정보는 배지 아래 글자 블럭으로만 보인다.
- 닫힌 빌드는 회색으로 남기지 않는다. 글자 블럭과 빌드 설명에서 아예 뺀다.
- 새 화면 요소는 `Ui`/`GameUi` 도우미를 쓰고 `BadgeView.Clear()` 경로에서 정리한다. 그리다가 실패하면 그 요소만 빠지고 나머지 표시는 유지한다.
- PvE에서만 표시한다(지금 게이트 그대로).
- 작업은 `main`이 아닌 새 브랜치 `feat/alternative-builds`에서 한다.

## Review Focus

1. **칩 선택 창을 리롤하거나 다시 열 때** 글자 블럭·빌드 설명이 정리되지 않고 겹쳐 남으면 안 된다. 새로 그리기 전에 이전 것을 모두 걷어내야 한다(Task 4·5 수동 확인 단계).
2. **같은 칩의 다른 등급을 가졌을 때**(예: `아드레날린 - 고효율`): 이름이 같은 모든 등급이 그 빌드 칩이므로 빌드를 고른 것으로 봐야 한다(Task 2 테스트 `다른_등급을_가져도_빌드를_고른_것으로_본다`).
3. **빌드 코어 칩이 등급 조건으로 보류가 될 때**(`보라 이상 권장`): 글자 블럭이 붙지 않아야 하지만, 그 칩을 가지면 빌드는 고른 것으로 본다(Task 2 테스트 `최종_판정이_코어가_아니면_글자를_붙이지_않는다`).
4. **표시 전환 키(F10)로 숨길 때** 빌드 설명도 함께 숨고, 다시 켜면 돌아와야 한다(Task 5 수동 확인 단계).
5. **사용자가 고친 `rules.json`의 `builds`가 잘못됐을 때**: 오류를 로그에 남기고 그 빌드(또는 `builds` 전체)만 빼며, 나머지 규칙은 그대로 동작해야 한다(Task 1 테스트 `잘못된_빌드는_오류를_남기고_뺀다`, `빌드가_하나만_남으면_builds_전체를_무시한다`).

---

### Task 1: 규칙 파일에서 빌드 읽기

**Files:**
- Modify: `src/Core/RuleSet.cs`
- Modify: `src/Core/RuleBook.cs` (`ReadCharacters`, 새 `ReadBuilds`)
- Test: `tests/ChipGuide.Core.Tests/RuleBookTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record BuildDef(char Letter, string Label, IReadOnlySet<int> Core);` (`RuleSet.cs`)
  - `ChipRule`에 마지막 선택 매개변수 `char? Build = null` 추가
  - `RuleSet(IReadOnlyList<ChipRule> Rules, IReadOnlyDictionary<int, Role> Roles, IReadOnlyDictionary<int, IReadOnlyList<BuildDef>>? Builds = null)`. 그리고 `public IReadOnlyList<BuildDef> BuildsOf(int heroId)`: 빌드가 없으면 빈 목록, 있으면 글자 순(A→C)
  - 빌드 `core` 규칙: `ChipRule(ids, Verdict.Core, why, 2, new Condition(Character: heroes), minGrade, Build: letter)`
  - 빌드 `good`/`avoid` 규칙: `ChipRule(ids, verdict, why, 2, new Condition(Owned: build.Core, Character: heroes), minGrade, Build: letter)`. 그 빌드의 코어 칩을 가져야만 걸린다
  - 기본 이유: 코어 `"{캐릭터} 코어 {글자}"`, 무난 `"{캐릭터} 빌드 {글자} 무난"`, 보류 `"{캐릭터} 빌드 {글자} 보류"`. 이유 우선순위는 항목의 `why` → 빌드 `why` → 캐릭터 `why` → 기본 이유

- [ ] **Step 1: 브랜치를 만들고 설계·계획 문서를 커밋**

```bash
git switch -c feat/alternative-builds
git add docs/superpowers/specs/2026-11-13-alternative-builds-design.md docs/superpowers/plans/2026-11-13-alternative-builds.md
git commit -m "docs: 캐릭터별 분기 빌드(코어 A·B·C) 설계와 구현 계획 추가"
```

- [ ] **Step 2: 실패하는 테스트 작성**

`RuleBookTests`에 추가한다. `Names`는 `NameTableTests.Sample`(파파라 1, 루루 4, 미미 3 / 아드레날린 100, 손풍기 200·201, 커터칼 300·301)이다.

```csharp
private const string BuildJson = """
{
  "version": 1,
  "characters": {
    "파파라": {
      "role": "dealer",
      "builds": {
        "A": { "label": "아드 반격", "core": ["아드레날린"], "avoid": [{ "chip": "손풍기", "why": "A 보류" }] },
        "B": { "label": "힐 탱커", "core": ["커터칼"], "why": { "커터칼": "B 코어 이유" } }
      }
    }
  }
}
""";

[Fact]
public void 빌드를_읽어_빌드_정보와_규칙을_만든다()
{
    var load = RuleBook.Load(BuildJson, Names);

    Assert.Empty(load.Errors);
    var builds = load.Rules.BuildsOf(1);
    Assert.Equal(new[] { 'A', 'B' }, builds.Select(b => b.Letter));
    Assert.Equal(new[] { "아드 반격", "힐 탱커" }, builds.Select(b => b.Label));
    Assert.Equal(new[] { 300, 301 }, builds[1].Core.OrderBy(i => i));
    var adrenaline = Assert.Single(load.Rules.Rules, r => r.RelicIds.Contains(100));
    Assert.Equal(((char?)'A', Verdict.Core, 2, "파파라 코어 A"), (adrenaline.Build, adrenaline.Verdict, adrenaline.Specificity, adrenaline.Why));
    Assert.Equal("B 코어 이유", Assert.Single(load.Rules.Rules, r => r.RelicIds.Contains(300)).Why);
    Assert.Empty(load.Rules.BuildsOf(4));
}

[Fact]
public void 빌드_무난과_보류는_그_빌드를_골랐을_때만_걸린다()
{
    Assert.Null(Judge(BuildJson, At(1), 200, Grade.Purple));
    var chosen = Judge(BuildJson, At(1, owned: new[] { 100 }), 200, Grade.Purple)!;
    Assert.Equal((Verdict.Avoid, "A 보류"), (chosen.Verdict, chosen.Reason));
}

[Theory]
[InlineData("""{ "A": { "label": "가", "core": ["아드레날린"] }, "B": { "label": "나", "core": ["커터칼"] }, "D": { "label": "다", "core": ["손풍기"] } }""", "characters.파파라.builds.D", "A, B, C 중 하나")]
[InlineData("""{ "A": { "label": "가", "core": ["아드레날린"] }, "B": { "label": "나", "core": ["커터칼"] }, "C": { "core": ["손풍기"] } }""", "characters.파파라.builds.C.label", "빌드 설명이 필요")]
[InlineData("""{ "A": { "label": "가", "core": ["아드레날린"] }, "B": { "label": "나", "core": ["커터칼"] }, "C": { "label": "열한글자짜리빌드설명임", "core": ["손풍기"] } }""", "characters.파파라.builds.C.label", "10자 이하")]
[InlineData("""{ "A": { "label": "가", "core": ["아드레날린"] }, "B": { "label": "나", "core": ["커터칼"] }, "C": { "label": "다", "core": [] } }""", "characters.파파라.builds.C.core", "하나 이상")]
[InlineData("""{ "A": { "label": "가", "core": ["아드레날린"] }, "B": { "label": "나", "core": ["커터칼"] }, "C": { "label": "다", "core": ["없는칩"] } }""", "characters.파파라.builds.C.core[0]", "없는칩")]
public void 잘못된_빌드는_오류를_남기고_뺀다(string builds, string path, string message)
{
    var json = $$"""{ "version": 1, "characters": { "파파라": { "role": "dealer", "builds": {{builds}} } } }""";

    var load = RuleBook.Load(json, Names);

    Assert.Contains(load.Errors, e => e.Contains(path) && e.Contains(message));
    Assert.Equal(new[] { 'A', 'B' }, load.Rules.BuildsOf(1).Select(b => b.Letter));
    Assert.DoesNotContain(load.Rules.Rules, r => r.RelicIds.Contains(200));
}

[Fact]
public void 공용_코어와_겹치는_빌드_코어는_빌드에서_뺀다()
{
    const string json = """
    { "version": 1, "characters": { "파파라": { "role": "dealer", "core": ["손풍기"],
      "builds": { "A": { "label": "가", "core": ["아드레날린", "손풍기"] }, "B": { "label": "나", "core": ["커터칼"] } } } } }
    """;

    var load = RuleBook.Load(json, Names);

    Assert.Contains(load.Errors, e => e.Contains("characters.파파라.builds.A.core[1]") && e.Contains("공용 코어"));
    Assert.Equal(new[] { 100 }, load.Rules.BuildsOf(1)[0].Core);
    Assert.DoesNotContain(load.Rules.Rules, r => r.Build == 'A' && r.RelicIds.Contains(200));
}

[Fact]
public void 빌드가_하나만_남으면_builds_전체를_무시한다()
{
    const string json = """
    { "version": 1, "characters": { "파파라": { "role": "dealer",
      "builds": { "A": { "label": "가", "core": ["아드레날린"] }, "B": { "label": "나", "core": [] } } } } }
    """;

    var load = RuleBook.Load(json, Names);

    Assert.Contains(load.Errors, e => e.Contains("characters.파파라.builds") && e.Contains("둘 이상"));
    Assert.Empty(load.Rules.BuildsOf(1));
    Assert.DoesNotContain(load.Rules.Rules, r => r.Build is not null);
}
```

- [ ] **Step 3: 테스트가 실패하는지 확인**

Run: `dotnet test tests/ChipGuide.Core.Tests --filter "FullyQualifiedName~RuleBookTests"`
Expected: 컴파일 오류(`BuildsOf`, `ChipRule.Build` 없음)

- [ ] **Step 4: `RuleSet`·`ChipRule`에 빌드 정보를 더하고 `RuleBook`에 `ReadBuilds` 구현**

- `ReadCharacters`에서 캐릭터 `core`를 읽을 때 칩 id를 모아 두고(공용 코어 중복 검사용), `builds`가 있으면 `ReadBuilds(body, path, character.Name, heroes, sharedCore, whyMap)`를 부른다.
- 빌드 규칙은 먼저 지역 목록에 모은다. 남은 빌드가 2개 이상일 때만 `Rules`와 빌드 사전에 넣는다. 빌드 하나 안의 칩 이름 오류는 그 칩만 빼고, 그래서 `core`가 비면 그 빌드를 뺀다.
- 오류 문구(경로는 위 테스트와 같다):
  - `{path}.builds.{키}: A, B, C 중 하나여야 합니다`
  - `{path}.builds.{키}.label: 빌드 설명이 필요합니다`
  - `{path}.builds.{키}.label: 10자 이하여야 합니다`
  - `{path}.builds.{키}.core: 칩이 하나 이상 필요합니다`
  - `{path}.builds.{키}.core[{i}]: '{이름}'은(는) 공용 코어에 이미 있습니다`
  - `{path}.builds: 빌드가 둘 이상 필요합니다`
- `label`은 `TextSafety.Clean`을 거친 뒤 길이를 잰다.
- `RuleBook.Load` 끝에서 `new RuleSet(builder.Rules, builder.Roles, builder.Builds)`로 넘긴다.

- [ ] **Step 5: 테스트 통과 확인**

Run: `dotnet test tests/ChipGuide.Core.Tests`
Expected: 모두 통과(기존 167개 + 새 테스트)

- [ ] **Step 6: 커밋**

```bash
git add src/Core/RuleSet.cs src/Core/RuleBook.cs tests/ChipGuide.Core.Tests/RuleBookTests.cs
git commit -m "feat: 규칙 파일에 캐릭터별 빌드(코어 A·B·C)를 적을 수 있게 함"
```

---

### Task 2: 열린 빌드 계산과 닫힌 빌드 덮어쓰기

**Files:**
- Modify: `src/Core/Model.cs`
- Modify: `src/Core/ChipAdvisor.cs`
- Test: `tests/ChipGuide.Core.Tests/ChipAdvisorTests.cs`

**Interfaces:**
- Consumes: Task 1의 `BuildDef`, `ChipRule.Build`, `RuleSet.BuildsOf(int)`
- Produces:
  - `Advice(ChipOption Option, Verdict Verdict, string Reason, IReadOnlyList<Note> Notes, IReadOnlyList<char> Builds)`: `Builds`는 마지막에 붙는 필수 매개변수
  - `public sealed record BuildLine(char Letter, string Label);` (`Model.cs`)
  - `ChipAdvisor.Builds(RuleSet rules, Situation situation) -> IReadOnlyList<BuildLine>?`: 빌드 없는 캐릭터는 null, 갈림길 전은 모든 빌드, 고른 뒤는 열린 빌드만(글자 순)
  - `ChipAdvisor.CoreAdvice(RuleSet rules, Situation situation) -> IReadOnlyList<Advice>`: 최종 판정이 코어인 칩의 판정(등급 없음). `CoreChips`는 `CoreAdvice(...).Select(a => a.Option.RelicId)`로 바꾸고 시그니처는 그대로 둔다
  - 덮어쓰기 이유 문구: `코어 {열린 글자들 "·"로 연결}를 골라 보류 ({이 칩이 속한 닫힌 빌드마다 "{글자} {label}"을 "·"로 연결} 빌드 칩)`. 예: `코어 A를 골라 보류 (B 힐 탱커 빌드 칩)`

- [ ] **Step 1: 실패하는 테스트 작성**

`ChipAdvisorTests`에 빌드용 상수와 도우미를 더한다.

```csharp
private const int Souri = 10;
private const int Adr = 110, AdrHigh = 111, Whet = 112, Buffer = 120, Kit = 121, Battery = 140, Carp = 150, Mark = 160;

private static RuleSet Builds(IEnumerable<BuildDef> builds, params ChipRule[] extra)
{
    var defs = builds.ToList();
    var rules = new List<ChipRule> { ForCharacter(Souri, new[] { Battery }, Verdict.Core, "공용 코어") };
    rules.AddRange(defs.Select(b => new ChipRule(b.Core, Verdict.Core, $"{b.Letter} 코어", 2, new Condition(Character: Ids(Souri)), null, b.Letter)));
    rules.AddRange(extra);
    return new RuleSet(rules, new Dictionary<int, Role> { [Souri] = Role.Dealer }, new Dictionary<int, IReadOnlyList<BuildDef>> { [Souri] = defs });
}

private static readonly BuildDef A = new('A', "아드 반격", Ids(Adr, AdrHigh, Whet));
private static readonly BuildDef B = new('B', "힐 탱커", Ids(Buffer, Kit));
private static readonly BuildDef C = new('C', "표식 에너지", Ids(Mark));

private static IReadOnlyList<Advice?> Judge(RuleSet rules, Situation at, params int[] chips) =>
    ChipAdvisor.Advise(rules, at, chips.Select(id => new ChipOption(id, Grade.Purple)).ToList());
```

테스트:

```csharp
[Fact]
public void 갈림길_전에는_모든_빌드_코어가_코어이고_글자가_붙는다()
{
    var advice = Judge(Builds(new[] { A, B }), At(Souri), Adr, Buffer, Battery);

    Assert.Equal(Verdict.Core, advice[0]!.Verdict);
    Assert.Equal(new[] { 'A' }, advice[0]!.Builds);
    Assert.Equal(Verdict.Core, advice[1]!.Verdict);
    Assert.Equal(new[] { 'B' }, advice[1]!.Builds);
    Assert.Equal(Verdict.Core, advice[2]!.Verdict);
    Assert.Empty(advice[2]!.Builds);
}

[Fact]
public void 한_빌드_칩을_가지면_다른_빌드_코어는_보류가_되고_글자가_없다()
{
    var advice = Judge(Builds(new[] { A, B }), At(Souri, owned: new[] { Whet }), Buffer, Adr);

    Assert.Equal(Verdict.Avoid, advice[0]!.Verdict);
    Assert.Equal("코어 A를 골라 보류 (B 힐 탱커 빌드 칩)", advice[0]!.Reason);
    Assert.Empty(advice[0]!.Builds);
    Assert.Contains(new Note(Verdict.Core, "B 코어"), advice[0]!.Notes);
    Assert.Equal(Verdict.Core, advice[1]!.Verdict);
    Assert.Equal(new[] { 'A' }, advice[1]!.Builds);
}

[Fact]
public void 다른_등급을_가져도_빌드를_고른_것으로_본다()
{
    var advice = Judge(Builds(new[] { A, B }), At(Souri, owned: new[] { AdrHigh }), Buffer);

    Assert.Equal(Verdict.Avoid, advice[0]!.Verdict);
}

[Fact]
public void 두_빌드_칩을_모두_가지면_둘_다_열리고_나머지만_닫힌다()
{
    var advice = Judge(Builds(new[] { A, B, C }), At(Souri, owned: new[] { Whet, Kit }), Adr, Buffer, Mark);

    Assert.Equal(new[] { 'A' }, advice[0]!.Builds);
    Assert.Equal(new[] { 'B' }, advice[1]!.Builds);
    Assert.Equal((Verdict.Avoid, "코어 A·B를 골라 보류 (C 표식 에너지 빌드 칩)"), (advice[2]!.Verdict, advice[2]!.Reason));
}

[Fact]
public void 빌드와_상관없이_코어인_칩은_덮어쓰지_않는다()
{
    var withExtras = new BuildDef('A', "아드 반격", Ids(Adr, Glove, Carp));
    var rules = Builds(new[] { withExtras, B },
        new ChipRule(Ids(Glove), Verdict.Core, "딜러 공용 코어", 1, new Condition(Roles: new HashSet<Role> { Role.Dealer })),
        new ChipRule(Ids(Carp), Verdict.Core, "맵 코어", 4, new Condition(Character: Ids(Souri), Map: Ids(7))));

    var advice = Judge(rules, At(Souri, owned: new[] { Kit }, map: 7), Glove, Carp, Adr);

    Assert.Equal(Verdict.Core, advice[0]!.Verdict);
    Assert.Empty(advice[0]!.Builds);
    Assert.Equal(Verdict.Core, advice[1]!.Verdict);
    Assert.Empty(advice[1]!.Builds);
    Assert.Equal(Verdict.Avoid, advice[2]!.Verdict);
}

[Fact]
public void 최종_판정이_코어가_아니면_글자를_붙이지_않는다()
{
    var rules = Builds(new[] { A, B },
        new ChipRule(Ids(Adr), Verdict.Avoid, "아군 회복", 3, new Condition(Ally: Ids(Lulu))),
        new ChipRule(Ids(Kit), Verdict.Core, "보라 이상", 2, new Condition(Character: Ids(Souri)), Grade.Purple, 'B'));

    var ally = Judge(rules, At(Souri, allies: new[] { Lulu }), Adr);
    var blue = ChipAdvisor.Advise(rules, At(Souri), new[] { new ChipOption(Kit, Grade.Blue) });

    Assert.Equal((Verdict.Avoid, 0), (ally[0]!.Verdict, ally[0]!.Builds.Count));
    Assert.Equal((Verdict.Avoid, 0), (blue[0]!.Verdict, blue[0]!.Builds.Count));
    Assert.Equal(new[] { 'A', 'B' }, ChipAdvisor.Builds(rules, At(Souri, allies: new[] { Lulu }))!.Select(b => b.Letter));
}

[Fact]
public void 제목_띠_빌드는_갈림길_전_모두_고른_뒤_열린_빌드만()
{
    var rules = Builds(new[] { A, B });

    Assert.Null(ChipAdvisor.Builds(rules, At(Papara)));
    Assert.Equal(new[] { new BuildLine('A', "아드 반격"), new BuildLine('B', "힐 탱커") }, ChipAdvisor.Builds(rules, At(Souri)));
    Assert.Equal(new[] { new BuildLine('A', "아드 반격") }, ChipAdvisor.Builds(rules, At(Souri, owned: new[] { Whet })));
}

[Fact]
public void 코어_판정은_열린_빌드_글자와_함께_코어_칩을_돌려준다()
{
    var rules = Builds(new[] { A, B });

    var before = ChipAdvisor.CoreAdvice(rules, At(Souri)).ToDictionary(a => a.Option.RelicId, a => a.Builds.ToArray());
    var after = ChipAdvisor.CoreAdvice(rules, At(Souri, owned: new[] { Whet })).Select(a => a.Option.RelicId);

    Assert.Equal(Array.Empty<char>(), before[Battery]);
    Assert.Equal(new[] { 'B' }, before[Kit]);
    Assert.Equal(new[] { Adr, AdrHigh, Whet, Battery }, after.OrderBy(i => i));
}
```

기존 `걸리는_규칙이_없으면_판정하지_않는다` 옆의 일반 판정 테스트 하나(`더_구체적인_규칙이_이긴다`)에 `Assert.Empty(result.Builds);`를 더해, 빌드 없는 캐릭터는 글자가 비어 있음을 확인한다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: `dotnet test tests/ChipGuide.Core.Tests --filter "FullyQualifiedName~ChipAdvisorTests"`
Expected: 컴파일 오류(`Advice.Builds`, `BuildLine`, `ChipAdvisor.Builds`, `CoreAdvice` 없음)

- [ ] **Step 3: `ChipAdvisor`에 빌드 판정 구현**

판정 순서. 지금 `AdviseOne`의 "단계·판정 정렬 + 조건부 무난이 코어를 내리지 않음" 부분을 `Resolve(hits)`로 빼서 두 번 쓴다.

```
open = 가진 칩이 Core와 겹치는 빌드들; 없으면 모든 빌드
top  = Resolve(걸린 모든 규칙)
member = 이 칩이 Core에 든 빌드들
if member가 있고 member ∩ open이 비었고 Resolve(Build == null인 규칙만)?.Verdict != Core:
    top = (Avoid, 덮어쓰기 이유), 원래 top은 참고 줄 맨 앞으로
Builds = top.Verdict == Core ? (member ∩ open의 글자, 글자 순) : 빈 목록
```

- 걸리는 규칙이 하나도 없으면 지금처럼 null을 돌려준다.
- `CoreAdvice`는 지금 `CoreChips`처럼 코어 규칙의 칩 id를 모아 등급 없이 판정하고, 코어인 것만 돌려준다.

- [ ] **Step 4: 테스트 통과 확인**

Run: `dotnet test tests/ChipGuide.Core.Tests`
Expected: 모두 통과

- [ ] **Step 5: 커밋**

```bash
git add src/Core/Model.cs src/Core/ChipAdvisor.cs tests/ChipGuide.Core.Tests/ChipAdvisorTests.cs
git commit -m "feat: 한 빌드의 코어 칩을 가지면 다른 빌드 코어를 보류로 바꿈"
```

---

### Task 3: 소우리 시험 데이터와 출시 규칙 검사, 규칙 문서

**Files:**
- Modify: `rules/rules.json` (`characters.소우리`)
- Modify: `tests/ChipGuide.Core.Tests/ShippedRulesTests.cs`
- Modify: `docs/RULES.md`, `docs/RECOMMENDATION.md`

**Interfaces:**
- Consumes: Task 1의 `builds` 형식과 `RuleSet.BuildsOf`, Task 2의 `ChipAdvisor.CoreChips`

- [ ] **Step 1: 출시 규칙 테스트를 먼저 고친다**

`[Theory]` 판정 목록에서 소우리 줄을 아래로 바꾸고 더한다.

```csharp
[InlineData("소우리", "완충 방패", Grade.Blue, "", Verdict.Core)]
[InlineData("소우리", "아드레날린", Grade.Orange, "", Verdict.Core)]
[InlineData("소우리", "완충 방패", Grade.Blue, "owned:숫돌", Verdict.Avoid)]
[InlineData("소우리", "구급 상자", Grade.Purple, "owned:숫돌", Verdict.Avoid)]
[InlineData("소우리", "아드레날린", Grade.Orange, "owned:완충 방패", Verdict.Avoid)]
[InlineData("소우리", "숫돌", Grade.Purple, "owned:아드레날린", Verdict.Core)]
[InlineData("소우리", "추가 배터리", Grade.Purple, "owned:숫돌", Verdict.Core)]
```

기존 `소우리 아드레날린 Orange "" Avoid` 줄은 지운다. 다른 소우리 줄(복싱 글러브, 우정 배지, 잉어 인형, 완벽한 나뭇가지)은 그대로 둔다.

`모든_캐릭터는_코어가_넷_이상이고_코어_키워드는_두_종류를_넘지_않는다`는 빌드가 있는 캐릭터를 빌드마다 검사하도록 바꾼다. 빌드가 없으면 지금처럼 `At(hero, "")` 한 번이다. 빌드가 있으면 빌드마다 `new Situation(heroId, new HashSet<int> { build.Core.Min() }, ...)`로 그 빌드만 연 상황에서 같은 두 단언을 하고, 메시지에 `{hero} 코어 {글자}`를 넣는다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: `dotnet test tests/ChipGuide.Core.Tests --filter "FullyQualifiedName~ShippedRulesTests"`
Expected: 새 소우리 줄들이 FAIL

- [ ] **Step 3: `rules.json`의 소우리 항목을 바꾼다**

- `core`: `["추가 배터리", "바이크 헬멧", "샌드 쿠키"]`
- `good`에서 `구급 상자` 줄을 지우고, `avoid`에서 `아드레날린`·`숫돌` 줄을 지운다.
- `why`에서 `완충 방패` 항목을 지운다(빌드 B로 옮긴다).
- `builds`를 더한다.

```jsonc
"builds": {
  "A": {
    "label": "아드 반격",
    "core": ["아드레날린", "숫돌"],
    "why": {
      "아드레날린": "맞으며 반격하는 키트라 체력이 절반 이하로 내려가기 쉬워, 그때 오르는 공격력이 반격 피해에 들어감",
      "숫돌": "체력 절반 이하에서 공격력 +4·받는 피해 −1이라 맞으며 반격하는 동안 세지고 오래 버팀"
    }
  },
  "B": {
    "label": "힐 탱커",
    "core": ["완충 방패", "구급 상자"],
    "why": {
      "완충 방패": "반격 직전 맞는 순간마다 회복 2중첩과 3코인을 얻어 반격 루프를 버팀",
      "구급 상자": "턴 시작 회복 중첩으로 맞으며 버티는 시간을 늘림. 완충 방패와 같은 회복 키워드"
    }
  }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `dotnet test tests/ChipGuide.Core.Tests`
Expected: 모두 통과(`기본_규칙_파일은_오류_없이_읽힌다`, 영어 참조표 테스트 포함)

- [ ] **Step 5: 규칙 문서를 고친다**

- `docs/RULES.md`
  - 형식 예시 아래에 `builds` 예시를 둔다.
  - 키 표에 `builds`를 더하고, 4절 검증 규칙 표를 옮긴다.
  - 오류 메시지 표에 Task 1의 오류 문구를 더한다.
  - "여러 규칙이 겹칠 때" 표 아래에 "닫힌 빌드 덮어쓰기는 마지막에 적용하고, 빌드 밖 규칙만으로 코어인 칩은 덮어쓰지 않는다"를 적는다.
- `docs/RECOMMENDATION.md`
  - 1절 표시 표에 `빌드 글자 블럭(배지 아래 A·B·C)`과 `빌드 설명(제목 띠 오른쪽)` 줄을 더한다.
  - 2절에 덮어쓰기 한 단락을 더한다.
  - 9절 소우리 행을 "코어 추가 배터리·바이크 헬멧·샌드 쿠키, 빌드 A 아드 반격(아드레날린·숫돌), 빌드 B 힐 탱커(완충 방패·구급 상자)"로 고치고, 빌드 내용은 후속 검증 전 임시값이라고 적는다.
  - 보류 표(`| 소우리 | 보류 |` 등)에서 아드레날린·숫돌 보류 언급을 지운다.

- [ ] **Step 6: 커밋**

```bash
git add rules/rules.json tests/ChipGuide.Core.Tests/ShippedRulesTests.cs docs/RULES.md docs/RECOMMENDATION.md
git commit -m "feat: 소우리에게 아드 반격·힐 탱커 두 빌드를 함께 추천"
```

---

### Task 4: 칩 카드의 빌드 글자 블럭, 툴팁 첫 줄, 코어 줄 글자

**Files:**
- Create: `src/Plugin/View/BuildTag.cs`
- Modify: `src/Plugin/View/BadgeView.cs` (`CardView`, `Show`, `Clear`, `TooltipLines`)
- Modify: `src/Plugin/View/CoreStrip.cs` (`CoreChip`, 아이콘 그리기)
- Modify: `src/Plugin/Guide.cs` (`Scan`, `CoreChips`)

**Interfaces:**
- Consumes: Task 2의 `Advice.Builds`, `ChipAdvisor.Builds`, `ChipAdvisor.CoreAdvice`, `BuildLine`
- Produces:
  - `internal static class BuildTag`: `internal static Color Fill(char letter)`, `internal static RuntimeObject Create(char letter, float size, int textSize)`. 정사각 `Ui.Graph`에 흰 글자 `Ui.Text`를 가운데 둔 `GComponent`이고 `touchable = false`. 색은 A `(0.27, 0.52, 0.95, 0.95)`, B `(0.85, 0.33, 0.62, 0.95)`, C `(0.13, 0.66, 0.72, 0.95)`. Task 5도 이 클래스를 쓴다
  - `CardView`에 마지막 매개변수 `string? BuildLine` 추가. 예: `코어 A (아드 반격)`, 두 빌드면 `코어 A·B (아드 반격·힐 탱커)`. `Advice.Builds`가 비면 null
  - `CoreChip(ChipIcon Chip, bool Owned, IReadOnlyList<char> Builds)`

- [ ] **Step 1: `BuildTag` 작성**

- [ ] **Step 2: 배지 아래 글자 블럭 그리기**

`BadgeView.Show`에서 배지를 만든 뒤 `card.Advice?.Builds`의 글자마다 `BuildTag.Create(letter, pillHeight, badgeTextSize)`를 배지와 같은 호스트에 붙인다.
- 첫 블럭 위쪽은 배지 아래쪽에 붙이고, 오른쪽 끝은 배지 오른쪽 끝에 맞춘다. 둘째부터는 그 아래로 쌓는다.
- 쌓은 블럭 아래쪽이 칩 아이콘 아래쪽을 넘으면 세로 대신 배지 아래에서 왼쪽으로 가로로 붙인다.
- 블럭은 새 목록 `BuildTags`에 모으고, `Clear()`에서 배지와 같이 정리한다. `Showing`에 `BuildTags.Count > 0`을 더한다.

- [ ] **Step 3: 툴팁 첫 줄**

`TooltipLines`에서 `card.BuildLine`이 있으면 맨 앞 줄로 넣는다.

- [ ] **Step 4: 코어 줄 글자**

- `Guide.CoreChips`는 `ChipAdvisor.CoreAdvice`를 쓴다. 같은 이름 칩은 지금처럼 합치고, 글자는 합친 칩들의 글자를 합집합으로 모은다.
- 정렬은 공용(글자 없음) → 첫 글자 A → B → C 순이고, 같은 그룹 안의 순서는 지금 순서를 따른다.
- `CoreStrip`은 `Builds`가 있는 아이콘의 오른쪽 아래에 한 변이 아이콘의 0.35배인 `BuildTag`를 붙인다. 글자가 여럿이면 오른쪽에서 왼쪽으로 붙인다.

- [ ] **Step 5: `Guide.Scan` 연결**

- `CardView`를 만들 때 `BuildLine`을 채운다. 글자는 `advice[i].Builds`, label은 `ChipAdvisor.Builds(_rules.Current, situation)`에서 찾는다.
- `상세 로그`가 켜져 있으면 진단 줄에 `빌드={열린 글자들 또는 없음}`을 더한다.

- [ ] **Step 6: 빌드와 테스트 확인**

Run: `dotnet build src/Plugin/ChipGuide.Plugin.csproj -c Release` 그리고 `dotnet test tests/ChipGuide.Core.Tests`
Expected: 경고·오류 0개, 테스트 모두 통과

- [ ] **Step 7: 게임에서 확인 (사용자 또는 게임을 켤 수 있는 실행자)**

`dotnet build src/Plugin/ChipGuide.Plugin.csproj -c Release -p:DeployToGame=true`로 설치한 뒤, PvE에서 소우리로 확인한다.
- 첫 칩 선택 창: 아드레날린·숫돌에 `★ 코어`와 파랑 `A` 블럭, 완충 방패·구급 상자에 분홍 `B` 블럭이 붙는다. 추가 배터리에는 블럭이 없다.
- 숫돌을 가진 뒤: 완충 방패는 `× 보류`이고 블럭이 없다. 툴팁에 `코어 A를 골라 보류 (B 힐 탱커 빌드 칩)`가 나온다.
- 리롤해도 블럭이 겹쳐 남지 않는다.
- 코어 줄 아이콘 귀퉁이에 글자가 붙고, 숫돌을 가진 뒤에는 B 아이콘이 사라진다.

- [ ] **Step 8: 커밋**

```bash
git add src/Plugin/View/BuildTag.cs src/Plugin/View/BadgeView.cs src/Plugin/View/CoreStrip.cs src/Plugin/Guide.cs
git commit -m "feat: 칩 카드와 코어 줄에 어느 빌드의 코어인지 A·B·C 글자로 표시"
```

---

### Task 5: 제목 띠 빌드 설명과 설정

**Files:**
- Create: `src/Plugin/View/TitleBand.cs`
- Create: `src/Plugin/View/BuildPanel.cs`
- Modify: `src/Plugin/View/KeywordPanel.cs` (`BandTop`을 `TitleBand.Find`로 교체)
- Modify: `src/Plugin/View/BadgeView.cs` (`Show`, `Clear`, `Showing`)
- Modify: `src/Plugin/Plugin.cs` (설정)
- Modify: `src/Plugin/Guide.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: Task 2의 `ChipAdvisor.Builds`, `BuildLine`, Task 4의 `BuildTag`
- Produces:
  - `internal static class TitleBand { internal static (float Top, float Bottom)? Find(RuntimeObject container); }`: 지금 `KeywordPanel.BandTop`의 탐색(`txt_Probability`를 세로로 가로지르고 폭이 컨테이너의 0.8배 이상인 보이는 자식 중 가장 아래 것)을 옮기고 아래쪽 좌표도 돌려준다. `KeywordPanel`은 `Find(container)?.Top`을 쓰고 동작은 그대로다
  - `internal static class BuildPanel`: `Show(RuntimeObject container, IReadOnlyList<BuildLine>? builds, int textSize)`, `Clear()`, `bool Alive`
  - `Plugin.BuildPanel`: `ConfigEntry<bool>`, 섹션 `일반`, 키 `빌드 설명`, 기본 `true`, 설명 `칩 선택 창 제목 띠 오른쪽에 이 캐릭터의 빌드 A·B·C 설명을 보여 줍니다. 빌드를 고르면 고른 빌드만 남습니다.`

- [ ] **Step 1: `TitleBand`로 띠 탐색을 옮기고 `KeywordPanel`이 쓰게 한다**

- [ ] **Step 2: `BuildPanel` 작성**

- `builds`가 null이거나 비었거나, `TitleBand.Find`가 null이면 그리지 않는다. 띠를 못 찾았을 때 `Plugin.Diagnostics`가 켜져 있으면 `[진단] 제목 띠를 찾지 못해 빌드 설명을 그리지 않습니다`를 남긴다.
- 빌드마다 `BuildTag.Create(letter, lineHeight, textSize)`와 흰 글자 `label`을 한 묶음으로 두고, 묶음 사이 간격은 글자 크기 1개다.
- 패널 최대 폭은 `container.width × 0.3`이다. 넘치면 다음 줄로 넘긴다. 제목이 가운데 정렬이라 이 폭이면 제목 글자와 겹치지 않는다.
- 오른쪽 끝은 `container.width - 여백`이고, 세로는 띠의 `Top`~`Bottom` 가운데에 맞춘다. 여백은 키워드 패널과 같은 `textSize × 0.5`를 쓴다.
- `touchable = false`로 두고 컨테이너에 붙인다.

- [ ] **Step 3: 연결과 설정**

- `BadgeView.Show`에 매개변수 `IReadOnlyList<BuildLine>? builds`를 더하고, 키워드 패널과 같은 `panelText`로 `BuildPanel.Show`를 부른다.
- `Clear()`와 `Showing`에 `BuildPanel`을 더한다.
- `Guide.Scan`은 `Plugin.BuildPanel.Value`일 때만 `ChipAdvisor.Builds(...)`를 넘기고, 아니면 null을 넘긴다.
- `표시 전환 키` 설명 문구의 괄호 목록에 `빌드 설명`을 더한다.

- [ ] **Step 4: 빌드와 테스트 확인**

Run: `dotnet build src/Plugin/ChipGuide.Plugin.csproj -c Release` 그리고 `dotnet test tests/ChipGuide.Core.Tests`
Expected: 경고·오류 0개, 테스트 모두 통과

- [ ] **Step 5: 게임에서 확인 (사용자 또는 게임을 켤 수 있는 실행자)**

- 소우리 첫 칩 선택 창: 제목 띠 오른쪽에 `[A] 아드 반격  [B] 힐 탱커`가 나오고, 제목 글자와 겹치지 않는다. 키워드 패널 위치는 전과 같다.
- 숫돌을 가진 뒤: `[A] 아드 반격`만 남는다.
- F10으로 숨기면 빌드 설명도 사라지고, 다시 누르면 돌아온다.
- 빌드가 없는 캐릭터(예: 알라나)는 빌드 설명이 나오지 않는다.
- 설정에서 `빌드 설명`을 끄면 띠 설명만 사라지고 글자 블럭은 남는다.

- [ ] **Step 6: README 화면 안내**

화면 안내 번호 설명에 빌드 설명(제목 띠 오른쪽)과 빌드 글자 블럭(배지 아래)을 한 줄씩 더한다. 이미지 교체는 실제 화면을 찍은 뒤 따로 한다.

- [ ] **Step 7: 커밋**

```bash
git add src/Plugin/View/TitleBand.cs src/Plugin/View/BuildPanel.cs src/Plugin/View/KeywordPanel.cs src/Plugin/View/BadgeView.cs src/Plugin/Plugin.cs src/Plugin/Guide.cs README.md
git commit -m "feat: 칩 선택 창 제목 띠에 빌드 A·B·C 설명 표시"
```
