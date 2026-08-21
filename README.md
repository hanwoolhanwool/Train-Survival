# Train Survival

> **멈추면 죽는다.** 끊임없이 달리는 열차 위에서 자원을 낚아채고, 밤마다 몰려드는 몬스터로부터 집을 지키며, 마침내 우주로 탈출한다.

**협동 생존 크래프팅 · 1~4인 온라인 · 1인칭 · PC / Steam**
`Unity 6 (6000.5.3f1)` · `URP 17.5` · `Netcode for GameObjects` · `Input System`

---

## 코어 루프

```
[낮] 집게로 자원 채집 → 제작 · 건설 · 요리 · 열차 증설
       ↓
[밤] 몬스터 웨이브 방어 (전투 자원 소모)
       ↓
[Day +1] 난이도 상승 → 3~5일마다 지역(계절) 전환
       ↓
[최종] 우주 진입 건축물 완성 → 클리어
```

연료는 상시 소모되는 압박 자원이다. 자동 충전이 아니라 **기관차 엔진에 직접 투입**해야 하므로
"채집 → 운반 → 투입"이라는 협동 운반 루프가 생긴다. 연료가 마르면 열차가 느려지고, 추격이 시작된다.

레퍼런스: Raft(이동 거점) · Snowpiercer(열차 세계관) · Deep Rock Galactic(역할 분담) · Don't Starve(낮/밤 루프)

## 개발 규모

1인 개발 · 2026.07 ~ 진행 중

| | |
|---|---|
| 커밋 | 486 |
| 스크립트 | 308개 (`Assets/_Project/Scripts`) |
| 테스트 | EditMode 707개 통과 (테스트 파일 68개) |
| 마일스톤 | M3 열차 파괴 → M4 지역·날씨 → M5 전투·생존 → M6 Steam 연동 → M7 엔드게임 → M8 아트 패스 |
| 설계 문서 | 기획서 · 네트워크 아키텍처 · 세계관 · 비주얼/UIUX · 오디오 · 아트 예산 |

## 설계 원칙

- **단방향 어셈블리 의존성** — `Utilities ← Core ← Systems ← Gameplay ← UI`. 역방향 참조를 컴파일 단계에서 차단한다.
- **시스템 간 직접 참조 금지** — 통신은 `EventBus<T>`, 전역 접근은 `ServiceLocator`를 경유한다.
- **스폰/소멸 일원화** — `Instantiate`/`Destroy` 직접 호출 대신 `PoolManager.Spawn/Despawn`. 웨이브 방어에서 GC 스파이크를 없애기 위한 규약이다.
- **규칙을 문서로 고정** — SOLID 준수 규칙과 아키텍처 규칙을 문서화하고, 새 코드가 그 규칙을 지키는지 리뷰 기준으로 삼는다.
- **CI** — GitHub Actions에서 EditMode·PlayMode 테스트를 자동 실행한다.

## 문서

| 문서 | 내용 |
|------|------|
| [기획서](docs/design/Train-Survival-기획서.md) | 게임 개요 · 코어 루프 · 시스템 전반 |
| [네트워크 아키텍처](docs/design/Train-Survival-네트워크-아키텍처.md) | NGO + Steam 스택, 동기화 설계 |
| [세계관 컨셉](docs/design/Train-Survival-세계관-컨셉.md) | 세계·서사·연출 원칙 |
| [아키텍처 규칙](docs/conventions/architecture-rules.md) | 폴더 배치 · 어셈블리 의존성 · 인프라 사용 |
| [SOLID 원칙](docs/conventions/solid-principles.md) | 코드 리뷰 기준 |
| [커밋 컨벤션](docs/conventions/git-commit-convention.md) | Unity 전용 규칙 포함 |
| [초기 세팅](Assets/SETUP.md) | 씬 흐름 · 테스트 실행 · CI/CD |

## 시작하기

1. Unity Hub에서 **6000.5.3f1**로 프로젝트를 연다.
2. 클론 직후 아래를 1회 실행한다 (커밋 템플릿 · LFS · 씬/프리팹 머지 드라이버):

   ```bash
   git config commit.template .gitmessage
   git lfs install --local
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
   ```
