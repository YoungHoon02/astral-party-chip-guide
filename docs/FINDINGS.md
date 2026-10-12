# 조사 결과

게임 코드 경로를 어떻게 확인했는지와 그 결과를 기록한다. 게임이 업데이트되면 이 문서의 경로부터 다시 확인한다.

## 조사 방법 (2026-10-12)

1. Addressables 다운로드 캐시에서 핫업데이트 어셈블리를 추출했다.
   - 대상: `%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_INT\com.unity.addressables\AssetBundles`
   - 결과: `AstralParty.Runtime.dll` (11.2MB, 타입 9,803개)
   - 추출한 DLL은 게임 코드의 사본이므로 로컬에만 두고 저장소에 커밋하거나 재배포하지 않는다.
2. 추출한 DLL의 메타데이터로 클래스, 필드, 메서드 시그니처를 확인했다.
3. 메서드 본문은 보지 않았다. 동작 순서나 값의 의미는 게임 실행 중 진단 로그로 확인한다(아래 "실행 중 확인 필요").

## 칩 선택 창

| 대상 | 경로 | 비고 |
|---|---|---|
| 창 (로직) | `UI.RelicWindow : UI.BaseWindow` | `GRoot`의 직계 자식으로 붙는다(`GRoot.ShowWindow`) |
| 창 (화면) | `RelicWindow.contentPane` → `UI.UIRelicWindow` | |
| 칩 카드 목록 | `UIRelicWindow.list_Relic` (`FairyGUI.GList`) | |
| 칩 카드 | `UI.UIRelic_Button_Item : FairyGUI.GButton` | `txt_tltle`, `txt_Desc`, `loader_Frame`, `RecommendType`(Controller), `txt_Recommend`, `txt_ParticularlyRecommend` |
| 선택지 칩 ID | `RelicWindow.vailRelics` (private, `RepeatedField<int>`) | 목록 렌더러 `RendererRelicItem(int index, GObject item)`가 순번으로 대응시킨다 |
| 게임 자체 추천 | `RelicWindow.relicScores` (`List<(int, RecommendedType)>`) | `NotRecommended`, `Recommended`, `ParticularlyRecommend`. 점수 함수: `RelicCharacterMatchScore`, `RelicKeyWordsMatchScore`, `RelicGrowthMatchScore`, `RelicTagMatchScore`, `RelicBasicScore` |
| 리롤·선택 버튼 | `UIRelicWindow.btn_Reset`, `btn_Select` | 모드는 누르지 않는다 |

## 칩 상점

- `UI.LandRelicWindow`(`ShowPurchase(BattlePlayerData, Action)`)와 화면 `UI.UILandRelicWindow`는 구매 확인 창이다. 칩 카드 목록이 없다(`btn_Sure`, `btn_Cancel`, `txt_HideTip`).
- 결론: 선택지 비교라는 이 모드의 형태와 맞지 않으므로 **v1은 레벨업 칩 선택 창만 지원한다.**

## 게임 상태

| 값 | 경로 |
|---|---|
| 로직 루트 | `GameLogic.GameLogicManager._inst` |
| 나 | `.battle`(`GameLogic.BattleLogic`) → `GetSelfPlayerData()` → `BattlePlayerData` |
| 플레이어 목록 | `BattleLogic.PlayerDatas` (public 필드, `List<BattlePlayerData>`) |
| 보유 칩 | `BattlePlayerData.GetRelicIds()` → `List<int>` |
| 캐릭터 ID | `BattlePlayerData.player`(public 필드, `RoomPlayer`) → `get_Hero()` → `HeroId` |
| 계정 ID | `RoomPlayer.get_Id()` (long). 나를 구분하는 데만 쓰고 저장하지 않는다 |
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

## 칩 제시 가중치 (커뮤니티 정리, 미검증)

커뮤니티 정리 자료를 옮긴 것이다. 게임 코드로 검증하지 않았다.

- 칩마다 역할군(지원, 방어, 카드, 공격)과 고르면 더해지는 추가 가중치가 있다. 예: 아드레날린 - 고효율은 공격 +40, 마킹 스프레이는 카드 +50, 우정 배지는 방어 +50이다. 게임 데이터상으로는 `RelicInfoConfigure`의 `SupportPt`, `TankPt`, `CardPt`, `AttackPt`로 보인다.
- 캐릭터마다 기본 가중치가 있다. 예: 유메는 지원/방어/카드/공격 = 15/100/15/15. 게임 데이터상으로는 `RelicConfigure.CharacterRelics`(`RelicCharacterRelicConfigure`: `Id`, `SupportPt`, `TankPt`, `CardPt`, `AttackPt`)로 보인다. `Id`가 캐릭터 ID와 같은지는 확인이 필요하다.
- 레벨업 선택지의 가장 왼쪽 칩은 현재 가중치 비율로 역할군이 정해진다. 칩을 고를 때마다 그 칩의 추가 가중치가 더해져 이후 선택지 분포가 바뀐다.
- 키워드 칩(회복, 별빛, 표식, 에너지)은 서로 다른 두 종류를 고르면 나머지 키워드가 제외된다. 그래서 가중치를 쌓을 때는 키워드 없는 칩이 유리할 수 있다.
- 레벨별 등장 확률(예: 1레벨 방어 파랑 70%)은 서버가 계산하므로 클라이언트 코드로 확인할 수 없다.

## 실행 중 확인 필요

정적 분석으로 확정할 수 없는 항목이다. 플러그인의 진단 로그(설정 `진단.상세 로그`)로 확인하고 결과를 이 문서에 추가한다.

1. `list_Relic`의 자식 순서와 `vailRelics` 순서가 일치하는지(`ChildIndexToItemIndex` 사용)
2. 리롤했을 때 칩 카드 객체가 재사용되는지
3. `PlayerDatas`에 몬스터 외 다른 종류(관전자, NPC)가 섞이는지
4. `RelicQualityType` 정수값이 Blue=1, Purple=2, Orange=3인지
5. `GetLocal`로 얻은 칩 이름 형식(등급별 접미사 `이름 - 부제` 여부)
6. `RelicCharacterRelicConfigure.Id`가 캐릭터 ID와 같은지(`names.json`의 유메 `weights`가 15/100/15/15인지)

### 확인 결과 (2026-10-12, 연습 모드 이변 도서관, 캐릭터 미미)

1. **일치한다.** 진단 로그의 카드 순서(50035, 50029, 50047)가 화면 왼쪽부터의 칩(돼지 저금통, 마법 비전서, 마법 화살통)과 같았다. 게임 채팅 로그의 획득 칩과 다음 창의 `보유` 목록도 맞았다.
2. **칩 카드 객체는 창이 바뀌어도 재사용된다.** 세 번의 레벨업 창에서 카드 포인터 3개가 매번 같았다. 리롤해도 같다: 리롤 전 50016·50041·50083이 리롤 뒤 50028·50027·50013으로 바뀌었고 카드 포인터 3개와 `보유` 목록은 그대로였다. 따라서 카드 식별은 포인터가 아니라 포인터와 칩 ID 조합으로 해야 한다(현재 구현 방식). 리롤 뒤 진단 줄은 0.2초 주기 검사에서 한 번 더 찍혔다.
3. **섞이지 않는다.** 4인 판에서 `characterType`은 1(플레이어) 4개, 2(몬스터) 10개뿐이었다.
4. **Blue=1, Purple=2, Orange=3이 맞다.** 화면 테두리 색과 `names.json`의 `grade`가 일치했다(blue 28, purple 34, orange 28).
5. **강화 단계가 있는 칩은 `이름 - 부제` 형식이다**(예: `복싱 글러브 - 초급`). 단계 없는 칩은 접미사가 없다(`추가 배터리`).
6. **같다.** 유메(123)의 `weights`가 지원/방어/카드/공격 = 15/100/15/15였다.

기타
- F10으로 `names.json`을 만들었다: 칩 90개, 캐릭터 35개, 맵 22개. `UIStringType` 대체값 경고는 없었다.
- 연습 모드(솔로 → 연습 모드 → 협동 챌린지 맵)도 `PvE=True`로 판정된다.
- 레벨업 창은 일정 시간 안에 고르지 않으면 첫 번째 칩이 자동 선택된다.
