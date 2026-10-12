# Astral Party 칩 선택 가이드

[![최신 릴리스](https://img.shields.io/github/v/release/YoungHoon02/astral-party-chip-guide?label=%EC%B5%9C%EC%8B%A0%20%EB%A6%B4%EB%A6%AC%EC%8A%A4&color=2ea44f)](https://github.com/YoungHoon02/astral-party-chip-guide/releases/latest)
[![다운로드](https://img.shields.io/github/downloads/YoungHoon02/astral-party-chip-guide/total?label=%EB%8B%A4%EC%9A%B4%EB%A1%9C%EB%93%9C)](https://github.com/YoungHoon02/astral-party-chip-guide/releases)
[![BepInEx 6 IL2CPP](https://img.shields.io/badge/BepInEx-6%20IL2CPP-blue)](https://builds.bepinex.dev/projects/bepinex_be)
[![라이선스](https://img.shields.io/github/license/YoungHoon02/astral-party-chip-guide?label=%EB%9D%BC%EC%9D%B4%EC%84%A0%EC%8A%A4)](LICENSE)

PvE에서 레벨업 칩을 고를 때, **지금 내 상황에 어떤 칩이 좋은지** 칩 카드마다 알려 주는 모드입니다.

<!-- 스크린샷 -->

## 이런 걸 알려 줘요

**칩마다 추천 배지**
- 칩 아이콘 옆에 `★ 코어` / `○ 무난` / `× 비추` 배지가 붙습니다.
- 배지에 마우스를 올리면 **왜 그런지** 이유가 나옵니다.
- 내 캐릭터, 이미 가진 칩, 같이 하는 아군, 맵을 보고 판정합니다. 예를 들어 아군에 회복 서포터가 있으면 아드레날린은 비추입니다.
- 낮은 등급이 나오면 "보라 이상 권장"처럼 알려 줍니다.
- 쓸 만하지만 효과가 일부만 나는 칩은 무난으로 두고 `주의:`로 이유를 알려 줍니다.

**키워드 칩 고정 경고**
- 키워드 칩(회복·별빛·표식·에너지)은 **두 종류를 고르면 나머지 두 종류가 그 판에서 더 나오지 않습니다.**
- 키워드를 한 종류만 가졌을 때 다른 키워드 칩이 나오면 `※`를 붙여, 고르면 무엇이 막히는지 알려 줍니다. 그 선택으로 내 캐릭터의 코어 칩까지 막히면 함께 알려 줍니다.

**키워드 패널**
- 칩 선택 창 제목 위에 키워드 4종의 상태가 한 줄로 늘 보입니다.
  - `●` 가진 키워드(개수)
  - `○` 아직 얻을 수 있는 키워드
  - `×` 더 나오지 않는 키워드 (회색)
- `※` 카드에 마우스를 올리면, 고르면 막힐 키워드가 주황색으로 미리 보입니다.
- 막혔거나 막힐 키워드 옆에는 그 칩들의 아이콘이 나오고, 아이콘에 마우스를 올리면 이름이 보입니다.

## 설치

1. [BepInEx 6 IL2CPP](https://builds.bepinex.dev/projects/bepinex_be)를 Astral Party(INT 클라이언트)에 설치합니다.
2. [최신 릴리스](https://github.com/YoungHoon02/astral-party-chip-guide/releases/latest)에서 ZIP을 받습니다.
3. ZIP을 게임의 `BepInEx\plugins\` 안에 풉니다. `BepInEx\plugins\AstralPartyChipGuide\` 폴더가 생기면 됩니다.
4. 게임을 켜고 로비에 한 번 들어가면 준비가 끝납니다. 그다음부터 PvE 칩 선택 창에 배지가 나옵니다.

## 알아 두면 좋은 점

- **PvE에서만** 나옵니다. PvP, 관전, 리플레이에서는 아무것도 표시하지 않습니다.
- 화면에 정보만 더할 뿐, 칩을 대신 고르거나 게임의 원래 추천 표시를 바꾸지 않습니다.
- 게임 안에서는 인터넷을 쓰지 않습니다. 새 버전 확인만 게임 밖 프로그램이 따로 합니다([아래](#새-버전-알림)).
- 게임이 업데이트되거나 언어를 바꿔도 다음 실행 때 자동으로 맞춥니다.

## 설정

처음 실행하면 `BepInEx\config\kr.younghoon02.astralparty.chipguide.cfg`가 생깁니다. 메모장으로 열어 `true`/`false`를 바꾸세요.

| 항목 | 기본값 | 설명 |
|---|---|---|
| 일반 > 활성화 | 켬 | 배지 표시를 켜고 끔 |
| 일반 > 키워드 패널 | 켬 | 제목 위 키워드 패널 |
| 일반 > 키워드 고정 경고 | 켬 | `※` 경고 |
| 일반 > 템플릿 생성 키 | F10 | 칩 이름 표를 직접 다시 만들 때(평소엔 자동) |
| 진단 > 상세 로그 | 끔 | 문제를 알려 주실 때 켜 주세요. 칩 창이 열릴 때 읽은 값이 `BepInEx\LogOutput.log`에 남습니다 |
| 업데이트 > 새 버전 확인 | 켬 | 게임을 켤 때 새 버전 확인 |

## 판정이 이상하다면

- 판정의 근거와 순서는 [추천 로직 문서](docs/RECOMMENDATION.md)에 모두 공개해 두었습니다.
- 틀린 판정이나 빠진 조합을 발견하면 [이슈](https://github.com/YoungHoon02/astral-party-chip-guide/issues)로 알려 주세요. 캐릭터, 칩, 상황(아군·맵·가진 칩)과 칩 선택 창 캡처가 있으면 큰 도움이 됩니다.
- 내 게임에서만 바꾸고 싶으면 플러그인 폴더의 `rules.json`을 고치세요. 게임을 켠 채 저장해도 몇 초 안에 반영되고, 잘못 고쳐도 직전 규칙이 유지됩니다. 쓰는 법은 [규칙 작성 안내](docs/RULES.md)에 있습니다.

## 새 버전 알림

게임을 켤 때 새 버전이 있으면 게임 밖에 작은 알림 창이 뜹니다. `업데이트하러 가기`를 누르면 릴리스 페이지가 열리고, `이번 버전은 무시`를 누르면 그 버전은 다시 알리지 않습니다.

- 플러그인 폴더의 `AstralPartyChipGuideUpdater.exe`가 GitHub에 요청 하나만 보냅니다. 계정이나 게임 정보는 보내지 않습니다.
- 알림만 하고, 내려받거나 설치하지는 않습니다.
- 게임을 켜지 않고 확인하려면 그 exe를 직접 실행하세요. 끄려면 설정의 `업데이트 > 새 버전 확인`을 `false`로 바꾸세요.

## 출처와 감사

- 기본 추천: Feiz님의 [「캐릭터별 나왔을 때 집어가면 좋을 코어 칩 및 플레이 방법 가이드」](https://arca.live/b/astralparty/171581722). 작성자의 허락을 받아 사용했습니다.
- 공략에 없는 조합은 칩 설명과 캐릭터 스킬([Astral Party 커뮤니티 위키](https://astralparty.miraheze.org/wiki/Characters))로 추정했고, 이유가 `추정:`으로 시작합니다.

## v0.1.0

첫 릴리스입니다.

- PvE 칩 선택 창에서 칩마다 ★ 코어 / ○ 무난 / × 비추 배지와 이유를 보여 줍니다.
- 내 캐릭터, 가진 칩, 아군, 맵에 따라 판정이 달라집니다. 캐릭터 35명의 추천은 Feiz님 공략(허락 받음)을 바탕으로 했고, 공략에 없는 조합은 칩 설명과 캐릭터 스킬로 추정해 보탰습니다.
- 키워드 칩 두 종류를 고르면 나머지가 막힌다는 점을 `※` 경고와 제목 위 키워드 패널로 알려 줍니다.
- `rules.json`을 고치면 게임을 켠 채 몇 초 안에 반영됩니다.
- 게임을 켤 때 새 버전이 있으면 게임 밖 창으로 알려 줍니다.

## 라이선스

MIT 라이선스입니다([LICENSE](LICENSE)). 개발 관련 내용은 [구조 설명](docs/ARCHITECTURE.md)에 있습니다.
