# 조사 결과

게임 코드 경로를 어떻게 확인했는지와 그 결과를 기록합니다. 게임이 업데이트되면 이 문서의 경로부터 다시 확인합니다. 아래 내용은 2026-10-14 칩 선택 화면 개편(보유 칩 세트·등급별 확률 표시) 이전 버전 기준입니다.

## 조사 방법 (2026-10-12)

1. Addressables 다운로드 캐시에서 핫업데이트 어셈블리를 추출했습니다.
   - 대상: `%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_INT\com.unity.addressables\AssetBundles`
   - 결과: `AstralParty.Runtime.dll` (11.2MB, 타입 9,803개)
   - 추출한 DLL은 게임 코드의 사본이므로 로컬에만 두고 저장소에 커밋하거나 재배포하지 않습니다.
2. 추출한 DLL의 메타데이터로 클래스, 필드, 메서드 시그니처를 확인했습니다.
3. 메서드 본문은 보지 않았습니다. 동작 순서나 값의 의미는 게임 실행 중 진단 로그로 확인했습니다(아래 "실행 중 확인 결과").

## 칩 선택 창

| 대상 | 경로 | 비고 |
|---|---|---|
| 창 (로직) | `UI.RelicWindow : UI.BaseWindow` | `GRoot`의 직계 자식으로 붙음(`GRoot.ShowWindow`) |
| 창 (화면) | `RelicWindow.contentPane` → `UI.UIRelicWindow` | |
| 칩 카드 목록 | `UIRelicWindow.list_Relic` (`FairyGUI.GList`) | |
| 칩 카드 | `UI.UIRelic_Button_Item : FairyGUI.GButton` | `txt_tltle`, `txt_Desc`, `loader_Frame`, `RecommendType`(Controller), `txt_Recommend`, `txt_ParticularlyRecommend` |
| 선택지 칩 ID | `RelicWindow.vailRelics` (private, `RepeatedField<int>`) | 목록 렌더러 `RendererRelicItem(int index, GObject item)`가 순번으로 대응시킴 |
| 게임 자체 추천 | `RelicWindow.relicScores` (`List<(int, RecommendedType)>`) | `NotRecommended`, `Recommended`, `ParticularlyRecommend`. 점수 함수: `RelicCharacterMatchScore`, `RelicKeyWordsMatchScore`, `RelicGrowthMatchScore`, `RelicTagMatchScore`, `RelicBasicScore` |
| 리롤·선택 버튼 | `UIRelicWindow.btn_Reset`, `btn_Select` | 모드는 누르지 않음 |

## 칩 상점

- `UI.LandRelicWindow`(`ShowPurchase(BattlePlayerData, Action)`)와 화면 `UI.UILandRelicWindow`는 구매 확인 창이다. 칩 카드 목록이 없습니다(`btn_Sure`, `btn_Cancel`, `txt_HideTip`).
- 결론: 선택지 비교라는 이 모드의 형태와 맞지 않으므로 **v1은 레벨업 칩 선택 창만 지원합니다.**

## 게임 상태

| 값 | 경로 |
|---|---|
| 로직 루트 | `GameLogic.GameLogicManager._inst` |
| 나 | `.battle`(`GameLogic.BattleLogic`) → `GetSelfPlayerData()` → `BattlePlayerData` |
| 플레이어 목록 | `BattleLogic.PlayerDatas` (public 필드, `List<BattlePlayerData>`) |
| 보유 칩 | `BattlePlayerData.GetRelicIds()` → `List<int>` |
| 캐릭터 ID | `BattlePlayerData.player`(public 필드, `RoomPlayer`) → `get_Hero()` → `HeroId` |
| 계정 ID | `RoomPlayer.get_Id()` (long). 나를 구분하는 데만 쓰고 저장하지 않음 |
| 몬스터 구분 | `BattlePlayerData.get_characterType()`. 2가 몬스터 |
| 맵 ID | `.room` → `curRoomInfo`(`GameLogic.RoomInfo`) → `get_MapId()` |
| PvE 판정 | `match.matchData`, `room.roomController` (`src/Plugin/Game/PveGate.cs`) |

## 설정 테이블

| 테이블 | 경로 | 주요 필드 |
|---|---|---|
| 칩 | `StaticConfigure.get_Relic()` → `RelicConfigure.get_Infos()` | `Id`, `NameID`, `RelicQualityType`(None, Blue, Purple, Orange), `KeyWordType`, `TagType`(목록), `MapLimit`(목록), `AttackPt`, `CardPt`, `SupportPt`, `TankPt`, `RelicRecBaseScore`, `RelicRecGrowth` |
| 칩 단건 | `UI.UIHelper.GetRelicInfoConfigure(int)` | |
| 캐릭터 | `StaticConfigure.get_Character()` → `CharacterConfigure.get_Infos()` | `Id`, `NameID`, `NickID`, `TagType`(None, Generic, Attack, Card, Support, Tank) |
| 맵 | `StaticConfigure.get_Map()` → `MapConfigure.get_Infos()` | `Id`, `MapName`(문자열 ID), `MapSpecialRelic`(목록) |
| 현지화 문자열 | `UI.UIHelper.GetLocal(int id, UI.UIStringType type)` | 타입 값은 `UIStringType`의 정적 필드(`Relic`, `Character`, `Map`)에서 실행 중에 읽는다 |

## 역할군 가중치 데이터

플러그인이 표시하는 값은 모두 게임 설정 데이터에서 읽은 것이며, 읽는 경로는 확인했습니다.

- 칩마다 역할군(지원, 방어, 카드, 공격)과 고르면 더해지는 추가 가중치가 있습니다. 데이터는 `RelicInfoConfigure`의 `SupportPt`, `TankPt`, `CardPt`, `AttackPt`입니다.
- 캐릭터마다 기본 가중치가 있습니다. 데이터는 `RelicConfigure.CharacterRelics`(`RelicCharacterRelicConfigure`: `Id`, `SupportPt`, `TankPt`, `CardPt`, `AttackPt`)이고, `Id`는 캐릭터 ID와 같습니다(아래 확인 결과 6). 예: 유메는 지원/방어/카드/공격 = 15/100/15/15.
- **이 값이 선택지 등장에 어떻게 쓰이는지는 확인하지 못했습니다.** 커뮤니티 글에는 "가장 왼쪽 칩은 현재 가중치 비율로 역할군이 정해진다", "레벨별 등장 확률(예: 1레벨 방어 파랑 70%)" 같은 설명이 있지만 검증되지 않았습니다. 선택지는 서버가 정하므로 클라이언트 코드로 확인할 수 없고, 모드는 이 설명을 판정에 쓰지 않습니다.
- 키워드 칩(회복, 스타라이트, 표식, 에너지)은 서로 다른 두 종류를 고르면 나머지 키워드 칩이 선택지에서 빠집니다. 플레이로 확인한 사실입니다.

## 실행 중 확인 결과

정적 분석만으로 확정할 수 없던 항목을 진단 로그(설정 `진단 > 상세 로그`)로 확인했습니다. 6개 모두 확인을 마쳤습니다.

### 확인 (2026-10-12, 연습 모드 이변 도서관, 캐릭터 미미)

1. **`list_Relic`의 자식 순서와 `vailRelics` 순서가 일치합니다.** 진단 로그의 카드 순서(50035, 50029, 50047)가 화면 왼쪽부터의 칩(돼지 저금통, 마법 비전서, 마법 화살통)과 같았습니다. 게임 채팅 로그의 획득 칩과 다음 창의 `보유` 목록도 맞았습니다.
2. **칩 카드 객체는 창이 바뀌어도 재사용됩니다.** 세 번의 레벨업 창에서 카드 포인터 3개가 매번 같았습니다. 리롤해도 같습니다: 리롤 전 50016·50041·50083이 리롤 뒤 50028·50027·50013으로 바뀌었고 카드 포인터 3개와 `보유` 목록은 그대로였습니다. 따라서 카드 식별은 포인터가 아니라 포인터와 칩 ID 조합으로 해야 합니다(현재 구현 방식). 리롤 뒤 진단 줄은 0.2초 주기 검사에서 한 번 더 찍혔습니다.
3. **`PlayerDatas`에는 다른 종류가 섞이지 않습니다.** 4인 판에서 `characterType`은 1(플레이어) 4개, 2(몬스터) 10개뿐이었습니다.
4. **`RelicQualityType`은 Blue=1, Purple=2, Orange=3입니다.** 화면 테두리 색과 `names.json`의 `grade`가 일치했습니다(blue 28, purple 34, orange 28).
5. **강화 단계가 있는 칩은 `이름 - 부제` 형식입니다**(예: `복싱 글러브 - 초급`). 단계 없는 칩은 접미사가 없습니다(`추가 배터리`).
6. **`RelicCharacterRelicConfigure.Id`는 캐릭터 ID와 같습니다.** 유메(123)의 `weights`가 지원/방어/카드/공격 = 15/100/15/15였습니다.

기타
- `names.json`을 만들어 칩 90개, 캐릭터 35개, 맵 22개를 확인했습니다. `UIStringType` 대체값 경고는 없었습니다.
- 연습 모드(솔로 → 연습 모드 → 협동 챌린지 맵)도 `PvE=True`로 판정됩니다.
- 레벨업 창은 일정 시간 안에 고르지 않으면 첫 번째 칩이 자동 선택됩니다.
