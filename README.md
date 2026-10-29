# TankAttack — Iron Keep (철의 요새)

전차를 몰고 몰려오는 적 전차 웨이브로부터 **지휘 코어**를 지키는 3인칭 탱크 디펜스 게임입니다.
혼자 플레이하거나 Photon으로 최대 4명이 협동 플레이할 수 있습니다.

- 엔진: **Unity 6000.5.10f1** (Built-in Render Pipeline)
- 플랫폼: Windows (Standalone x64)
- 언어: 한국어 UI

## 주요 기능

- **탱크 3종**
  | 탱크 | 역할 | 체력 | 속도 | 포탄 피해 | 재장전 |
  |---|---|---|---|---|---|
  | VANGUARD | 균형형 | 190 | 12 | 32 | 0.65초 |
  | KESTREL | 정찰형 (빠른 이동·연사) | 130 | 17 | 23 | 0.42초 |
  | BASTION | 중장갑형 (높은 체력·피해) | 300 | 8 | 48 | 1.05초 |
- **적 전차 4종**: RAIDER(돌격), SIEGE(중장갑), MARKSMAN(장거리), DEMOLISHER(근접 범위 피해). 웨이브가 진행될수록 새 종류가 등장합니다.
- **난이도 4단계**: 쉬움 / 보통 / 어려움(각 5웨이브) / 무한 모드(웨이브마다 점점 강해짐)
- **탄종 2가지**: 철갑탄(단일 대상), 고폭탄(범위 피해, 재장전 느림)
- **경제 시스템**: 적을 격파해 크레딧을 얻고, 코어 수리와 포탑 건설·강화(좌우 2기, 최대 5레벨)에 사용
- **강화 아이템**: 수리 키트, 범위 포탄, 연사 속도, 포탄 피해 (해당 전투 동안 유지)
- **로컬 랭킹**: 무한 모드 기록을 닉네임별로 이 PC에 저장 (PlayerPrefs)
- **온라인 협동**: Photon PUN 2로 방 만들기 / 방 참가 / 빠른 참가 (최대 4명)

## 조작법

| 키 | 동작 |
|---|---|
| W A S D / 방향키 | 이동·회전 |
| 마우스 | 시점 회전 및 조준 (포탑과 포신이 조준점을 따라감) |
| 마우스 왼쪽 버튼 | 발사 |
| 마우스 휠 | 카메라 거리 조절 |
| V | 3인칭 ↔ 포수 시점 전환 |
| 1 / 2 | 철갑탄 / 고폭탄 |
| R | 코어 수리 (코어 근처에서) |
| T | 포탑 건설·강화 (코어 근처에서) |
| M | 음악 켜기/끄기 |
| Esc | 일시정지 |
| Enter | 로비에서 출격 |

## 실행 방법

1. Unity Hub 또는 Unity CLI로 **6000.5.10f1** 에디터를 설치합니다.
2. 이 저장소를 클론한 뒤 Unity에서 프로젝트 폴더를 엽니다.
   ```bash
   git clone https://github.com/taehwan12/TankAttack.git
   ```
3. `Assets/Defense/Scenes/Intro.unity`를 열고 Play를 누릅니다.

빌드 씬 순서: `Intro` → `Main` → `Victory` / `Defeat`

### 멀티플레이 설정

온라인 협동은 Photon 서버를 사용합니다. 저장소에 들어 있는 AppId 대신 직접 만든 AppId를 쓰려면
[Photon 대시보드](https://dashboard.photonengine.com)에서 PUN 앱을 만든 뒤
`Window > Photon Unity Networking > Highlight Server Settings`에서 **App Id PUN** 값을 바꾸세요.
솔로 플레이는 오프라인 모드로 동작하므로 인터넷 연결이 필요 없습니다.

## 프로젝트 구조

```
Assets/
├── Defense/              # 게임 본체 (네임스페이스 IronKeep)
│   ├── Scripts/          # 게임 로직, HUD, 네트워크, 랭킹, 한국어 번역
│   ├── Editor/           # 씬 자동 생성, 한글 폰트 생성, 자동 점검 메뉴
│   ├── Scenes/           # Intro / Main / Victory / Defeat
│   ├── Prefabs/, Materials/, Fonts/
│   └── Resources/Music/  # 로비·전투·승리·패배 음악
├── 01.Scenes/, 02.Scripts/  # 초기 탱크 튜토리얼 (빌드에는 포함되지 않음)
├── Photon/               # Photon PUN 2
└── 03.Prefabs/, 04.Images/, 05.Models/, polyperfect/, Standard Assets/  # 외부 에셋
```

에디터 상단 **Iron Keep** 메뉴에서 씬 재생성(`Build Defense Scenes`), 검증(`Validate Defense`),
Play 모드 자동 점검, 한글 폰트 생성(`Build Korean Font`)을 실행할 수 있습니다.

## 사용한 외부 에셋

외부 에셋의 저작권과 라이선스는 각 제작자에게 있습니다.

- [Photon PUN 2](https://www.photonengine.com/pun) — Exit Games
- Low Poly Ultimate Pack — polyperfect
- Unity Standard Assets, TextMesh Pro — Unity Technologies
- Noto Sans KR — Google (SIL Open Font License)
- Abrams 전차 모델, Explosion Pack 이펙트

## 만든 사람

- [taehwan12](https://github.com/taehwan12) — 학생 프로젝트
