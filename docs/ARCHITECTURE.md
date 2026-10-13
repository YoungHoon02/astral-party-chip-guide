# 구조

게임 UI와 게임 상태를 이름으로 찾아 **읽기만** 합니다.

```
EventSystem.Update Postfix (프레임당 1회)
 └ PvE 게이트 ── PvP·관전·확인 불가면 배지를 모두 지우고 종료
 └ ChipScreenWatcher   칩 선택 창 감지 → 칩 카드 목록 + 칩 ID·등급
 │    (선택지가 바뀔 때만 다음 단계 실행: 리롤, 창 다시 열기)
 └ GameStateReader     내 캐릭터 ID, 보유 칩 ID들, 아군 캐릭터 ID들, 맵 ID
 └ ChipAdvisor         (순수 C#) 상황 + 선택지 + 규칙 → 칩별 판정과 이유
 └ BadgeView           칩 카드에 배지, 선택한 칩의 이유 툴팁
 └ KeywordPanel        제목 띠 위 키워드·역할군 가중치 패널
 └ CoreStrip           확인 버튼 아래 코어 칩 아이콘 줄

RuleBook      rules.json 로드·검증, 파일이 바뀌면 다시 읽음
TemplateTool  게임 데이터가 준비되면 칩·캐릭터·맵 테이블을 names.json으로 자동 출력
```

## 구성 요소

| 구성 요소 | 책임 | 코드 |
|---|---|---|
| ChipScreenWatcher | 칩 선택 창과 칩 카드 찾기, 선택지 변경 감지 | `src/Plugin/Game/ChipScreen.cs` |
| GameStateReader | 캐릭터·보유 칩·아군·아군 보유 칩·맵 읽기 | `src/Plugin/Game/GameState.cs` |
| PvE 게이트 | PvE 여부 판정 | `src/Plugin/Game/PveGate.cs` |
| TemplateTool | 게임 테이블을 `names.json`으로 출력 | `src/Plugin/Game/GameTables.cs` |
| ChipAdvisor | 판정 계산 (게임 의존 없음) | `src/Core/ChipAdvisor.cs` |
| KeywordLock | 키워드 고정 경고 문구. 규칙 파일과 별개로 칩의 `KeyWordType`만으로 판단 | `src/Core/KeywordLock.cs` |
| RuleBook / RuleSource | 규칙 해석·검증, 실패 시 이전 규칙 유지 | `src/Core/RuleBook.cs`, `RuleSource.cs` |
| NameTable | 이름 ↔ ID 변환 | `src/Core/NameTable.cs` |
| BadgeView | 배지·툴팁 표시와 제거, 선택한 칩 판정 | `src/Plugin/View/BadgeView.cs` |
| KeywordPanel / CoreStrip | 키워드·가중치 패널, 코어 칩 아이콘 줄 | `src/Plugin/View/KeywordPanel.cs`, `CoreStrip.cs` |
| ReleaseCheck / Updater | 게임 시작 때 게임 밖 새 버전 확인 프로그램 실행 (아래 "릴리스 알림") | `src/Plugin/ReleaseCheck.cs`, `src/Updater/` |
| Guide | 위 요소를 묶는 틱 루프. 칩 창 탐색 0.2초, 규칙 파일 변경 확인 2초 간격 | `src/Plugin/Guide.cs` |

`src/Core`는 게임 DLL을 참조하지 않으므로 `tests/ChipGuide.Core.Tests`에서 단위 테스트합니다. `src/Plugin`은 게임과 닿는 부분만 담습니다. 각 기능은 `Feature`로 감싸 예외가 나면 그 기능만 끄고 로그를 한 번 남깁니다.

## 지켜야 할 제약

- **게임 로직에 Harmony를 걸지 않습니다.** 매 프레임 실행은 `EventSystem.Update` Postfix 하나만 씁니다. 게임 로직을 패치하면 게임 업데이트나 다른 모드와 충돌할 위험이 커집니다.
- **IL2CPP 쪽에 새 타입을 등록하거나 이벤트를 구독하지 않습니다.** 이런 등록은 실제 게임에서 불안정했기 때문에, 선택한 칩도 매 프레임 카드의 선택 상태를 읽어서 판단합니다.
- **읽기 전용입니다.** 칩 선택 동작을 호출하지 않고 게임 원래 UI 값을 바꾸지 않습니다. 모드가 만든 객체만 붙였다가 뗍니다.
- IL2CPP는 해제된 객체의 주소를 재사용하므로 칩 카드는 포인터와 칩 ID 조합으로 식별하고, 참조는 쓸 때마다 유효성을 확인합니다.
- 게임 프로세스는 외부 통신을 하지 않고 계정 정보를 읽거나 저장하지 않습니다. 새 버전 확인은 게임 밖 확인 프로그램이 작은 인덱스 하나만 읽으며, 파일을 내려받거나 바꾸지 않습니다(아래 "릴리스 알림").

## 릴리스 알림

새 릴리스 확인은 **게임 밖 프로그램** `AstralPartyChipGuideUpdater.exe`(`src/Updater`)가 합니다. 플러그인은 게임을 켤 때 이것을 `--auto`로 띄우기만 하고(`src/Plugin/ReleaseCheck.cs`, 설정 `업데이트 > 새 버전 확인`), 게임 프로세스는 네트워크도 창도 쓰지 않습니다.

```
publish-release.ps1 ─ 게시된 ZIP을 받아 sha256·크기 측정 ─> distribution 브랜치/release-index.json
플러그인 ─ 게임 시작 때 --auto로 실행 ─> Updater ─ raw.githubusercontent.com ─> 버전 비교 ─> 알림 창
```

- **새 버전이 있을 때만 창이 뜹니다.** 버튼은 `업데이트하러 가기`(릴리스 페이지를 브라우저로 엽니다)와 `이번 버전은 무시`(그 버전을 `update-skip.txt`에 적어 다시 알리지 않습니다) 둘이고, 창을 닫으면 다음 실행에 다시 알립니다. 최신이거나 확인에 실패하면 `--auto`에서는 아무것도 띄우지 않습니다.
- **이용자가 직접 실행하면** 무시한 버전도 다시 보여 주고, 최신이거나 실패했을 때도 결과를 알립니다. 게임을 켜지 않고 확인하는 경로입니다.
- 알림 창은 맨 위(`TopMost`)에 띄웁니다. 게임과 함께 뜨므로 그러지 않으면 게임 창 뒤에 깔립니다. 게임을 연달아 켜도 창이 겹치지 않게 이름 있는 뮤텍스로 한 번만 돕니다.
- 비교 기준은 **같은 폴더의 플러그인 DLL(`AstralPartyChipGuide.dll`) 버전**입니다. 확인 프로그램만 옛것으로 남은 설치에서도 실제 설치본과 비교하게 하려는 것입니다. DLL을 못 읽으면 자기 버전을 씁니다.
- **.NET Framework 4.8로 만듭니다.** Windows 10/11에 기본으로 들어 있어 런타임을 따로 설치하지 않아도 됩니다.
- 내려받거나 교체하지 않습니다. 배포물이 `BepInEx\plugins\…` 폴더라 교체가 중간에 실패하면 게임이 아예 안 켜질 수 있습니다. 새 버전 알림은 게임 화면(배지)에 띄우지 않습니다.
- **`api.github.com`을 쓰지 않습니다.** 미인증 IP당 시간당 60회라 공유기·PC방처럼 IP를 나눠 쓰는 곳에서 막힙니다. raw는 CDN이라 제한이 사실상 없고 응답도 200바이트 남짓입니다. 대신 5분 캐시가 있어 게시 직후 최대 5분은 이전 인덱스가 보입니다. 그래서 인덱스는 **자산이 받아지는 것을 확인한 뒤에** 올립니다. 반대로 하면 알림을 보고 받으러 간 사람이 404를 봅니다.
- **`distribution`은 인덱스 한 파일만 담는 브랜치입니다.** `main`에 두면 코드 커밋마다 CDN 캐시가 흔들리고 히스토리가 섞입니다. 스크립트가 작업 트리를 건드리지 않고 git plumbing으로 커밋을 만들어 밉니다.
- **인덱스를 믿지 않습니다** (`src/Updater/ReleaseIndex.cs`, 테스트 `ReleaseIndexTests` U1~U5). `schemaVersion`이 1이 아니거나, `downloadUrl`이 버전으로 다시 만든 주소(`…/releases/download/v<버전>/AstralPartyChipGuide-v<버전>.zip`)와 글자 단위로 다르거나, `sha256`이 64자리 16진수가 아니거나, 크기가 0 이하면 알리지 않습니다. 응답은 64KB까지만 읽습니다. 여는 주소는 인덱스가 아니라 코드에 고정한 `releases/latest`입니다.
- 어셈블리 버전은 `0.1.0.0`이고 인덱스는 `0.1.0`이라 네 번째 자리를 버리고 비교합니다.

### 빌드 환경

플러그인은 게임의 BepInEx 폴더에 있는 DLL을 참조한다. 경로는 저장소에 넣지 않습니다.

- `Directory.Build.local.props.example`을 `Directory.Build.local.props`로 복사하고 `BepInExDir`을 자기 게임의 `...\Astral Party\8vJXnINT\BepInEx`로 고칩니다. 이 파일은 git이 무시합니다.
- 또는 환경 변수 `ASTRAL_PARTY_BEPINEX`에 같은 경로를 넣습니다.
- 경로가 없거나 틀리면 빌드가 이유를 알려 주며 멈춥니다. `-p:DeployToGame=true`를 붙이면 빌드 결과를 그 폴더의 `plugins\AstralPartyChipGuide\`에 복사합니다.

### 릴리스 절차

1. `src/Plugin/ChipGuide.Plugin.csproj`의 `<Version>`과 `Plugin.cs`의 `Version`을 함께 올립니다. 다르면 스크립트가 멈춥니다.
2. `README.md`에 `## v<버전>` 섹션을 사용자 관점으로 적습니다. 릴리스 본문이 여기서 나옵니다.
3. `main`을 `origin`에 민 뒤 `tools\publish-release.ps1`을 실행합니다. `tools\package.ps1`로 `dist\AstralPartyChipGuide-v<버전>.zip`을 만들고(확인 프로그램은 플러그인 버전으로 빌드, 같은 이름의 ZIP이 있으면 멈춤), 태그 `v<버전>`과 GitHub 릴리스를 만든 뒤 인덱스를 `distribution`에 올립니다. 인덱스만 실패했으면 `-IndexOnly`로 다시 올립니다.

ZIP만 만들 때는 `tools\package.ps1`만 실행합니다. 최상위 폴더 `AstralPartyChipGuide\`에 두 DLL, 확인 프로그램(`.exe`, `.exe.config`), `rules.json`, `README.md`, `LICENSE`가 들어갑니다.

## 게임 업데이트 뒤 깨졌을 때

1. `docs/FINDINGS.md`의 경로(클래스·필드 이름)가 아직 맞는지 대조합니다.
2. 설정의 `진단 > 상세 로그`를 켜고 칩 선택 창을 열어 읽은 값(캐릭터, 보유 칩, 아군, 맵, 판정)을 로그에서 확인합니다. 어느 단계에서 값이 비는지 보면 원인 구간이 좁혀집니다.
3. 경로가 바뀌었으면 DLL 분석 도구로 핫업데이트 어셈블리(`AstralParty.Runtime.dll`)를 다시 분석합니다. 추출 방법은 `docs/FINDINGS.md`의 조사 방법을 따르고, 추출한 DLL은 로컬에만 두고 커밋하지 않습니다.
