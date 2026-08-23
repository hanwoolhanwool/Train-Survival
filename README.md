# Train Survival

[![CI](https://github.com/hanwoolhanwool/Train-Survival/actions/workflows/ci.yml/badge.svg)](https://github.com/hanwoolhanwool/Train-Survival/actions/workflows/ci.yml)

> **멈추면 죽는다.** 끊임없이 달리는 열차 위에서 자원을 낚아채고, 밤마다 몰려드는 몬스터로부터 집을 지키며, 마침내 우주로 탈출한다.

**협동 생존 크래프팅 · 1~4인 온라인 · 1인칭 · PC / Steam**

**개발 완료 목표 2027년 1월 중순, 출시 목표 2027년 2월 초.**

---

## 포트폴리오 — 설계 결정 · 문제 해결 · QA · 아트

### **[hanwoolhanwool.github.io/Train-Survival](https://hanwoolhanwool.github.io/Train-Survival/)**

구현 내용 5축, 아키텍처 결정 8건, 트러블슈팅 6건, QA 계측 지표 Q1~Q5, 아트 반입 규격을 한 페이지에 정리했다. 플레이 영상도 이 페이지에서 바로 재생된다.

---

## 대표 화면

![Train Survival 대표 화면](docs/images/hero.png)

## 게임 플레이 영상

<video src="docs/videos/gameplay.mp4" controls width="100%" poster="docs/images/hero.png"></video>

[영상 파일 직접 보기](docs/videos/gameplay.mp4)

---

## 코어 루프

| Day | Night | Day +1 | Final |
|---|---|---|---|
| **집게로 자원 채집**<br>달리는 열차에서 지상의 자원을 집게로 낚아챕니다. 제작 · 건설 · 요리 · 열차 증설의 재료가 됩니다. | **웨이브 방어**<br>몬스터가 열차에 달라붙습니다. 칸이 파괴되면 연결이 끊기고 뒤쪽 편성이 통째로 이탈합니다. | **난이도 상승 · 지역 전환**<br>웨이브 물량과 몬스터 종류가 늘고, 3~5일마다 지역이 바뀌어 날씨 압박이 달라집니다. | **우주 진입**<br>궤도 도약 기관을 완성해 대기권을 벗어나면 클리어. 중간 저장은 없고, 런은 단일 세션으로 완주합니다. |

연료는 상시 소모되는 압박 자원이다. 자동 충전이 아니라 **기관차 엔진에 직접 투입**해야 하므로
"채집 → 운반 → 투입"이라는 협동 운반 루프가 생긴다. 연료가 마르면 열차가 느려지고, 추격이 시작된다.

레퍼런스: Raft(이동 거점) · Snowpiercer(열차 세계관) · Deep Rock Galactic(역할 분담) · Don't Starve(낮/밤 루프)

## 개발 규모

1인 개발 · **2026.07.17 착수** ~ 진행 중 (37일차)

| | |
|---|---|
| 커밋 | 526 |
| 스크립트 | 322개 · 43,913줄 (`Assets/_Project/Scripts`) |
| 테스트 | EditMode 743개 (테스트 파일 71개 · 10,915줄) |
| 어셈블리 | 런타임 6개 + 테스트 2개 — 의존 방향 단방향 고정 |
| 문서 | 95개 (`docs/`) — 기획서 · 네트워크 아키텍처 · 세계관 · 비주얼/UIUX · 오디오 · 아트 예산 |
| 마일스톤 | M3 열차 파괴 → M4 지역·날씨 → M5 전투·생존 → M6 Steam 연동 → M7 엔드게임 → M8 아트 패스 |

<sub>측정 2026-08-22 · `main` = `492cb55` 기준. 개발이 진행 중이라 수치는 계속 바뀐다.
집계는 `git ls-files` 기준이며 커밋되지 않은 로컬 파일은 세지 않는다.</sub>

## 일정

출시 목표에서 역산한 남은 개발 로드맵이다. 각 월은 선행 의존성, 검증 범위, 출시 준비 흐름을 기준으로 묶었다.

| 시점 | 목표 | 주요 항목 |
|---|---|---|
| **2026.09** | **기반 안정화** — 릴리스 차단 요소 제거 | 본편 `Game.unity` 궤도 이식(레벨 0차) · **레벨 디자인 1차 완료**(저작 기반, 완료 기준 = 회귀 0) · NGO 세션 재시작 해결 · 세션 이탈 시 클라이언트 잔존 · 보스 패턴 4인 미발동 · Steam 실계정 로비·초대·릴레이 검증(M6 완료 기준) · **대형 클래스 5건 리팩터링**(9일 — [근거·순서](docs/plans/features/리팩터링-조사-보고서.md)) |
| **2026.10** | **엔드게임 완성** — 단일 런 완주 가능 | M7 최종장(궤도 도약·가속 방어전·우주) · 뉴게임+ 순환 · 터렛(전투 범위 마지막 잔여 구현) · 대기실 초대·게스트 권한 · M7 변경 요청 4건 · 밸런스 기준 확정(몬스터 상한·부활 n값·수리 비용) |
| **2026.11** | **월드·오디오 패스** — 지역별 플레이 구분 강화 | M8 3차 렌더 실측(Frame Debugger·GPU RD) → 레벨 2차 숲 파일럿 10종 → 3차 중경·원경 → 4차 나머지 3지역 30종 · 오디오 인프라와 사운드 반입 · UI 정리 |
| **2026.12** | **외부 검증·밸런싱** — 협동 플레이 기준 조정 | 플레이 검증 잔여 항목 정리 · **4인 세션 역할 분담 검증**(M5 완료 기준, 미실시) · 외부 4인 플레이테스트 · 밸런싱 반영 |
| **2027.01 초** | 출시 준비 | 릴리스 후보 빌드 · 스토어 페이지 · 트레일러 · 자체 AppID 발급 |
| **2027.01 중순** | **개발 완료 — 기능 동결** | 이후 버그 수정만. 출시까지 약 3주 완충 |
| **2027.02 초** | **출시 — 설 연휴** | 2027년 설날 2월 6일. 연휴 직전 출시 + 런칭 할인 |

협동 게임 특성상 출시 직후 함께 플레이할 수 있는 기간이 중요하다고 판단해 2027년 설 연휴 직전을 목표 창으로 잡았다.
기능 구현은 2027년 1월 중순에 동결하고, 이후 약 3주는 릴리스 후보 검증·스토어 준비·트레일러 마감에 배정했다.

2026.09는 후속 콘텐츠가 올라갈 기반을 먼저 안정화하는 구간이다. 본편 궤도와 레벨 기준을 확정하지 않으면 이후 지형·배치물의 기준 높이가 흔들리고, M7 최종장 구현 전에 `TrainState`·`RepairHammerController`의 책임 경계를 나누지 않으면 회귀 범위가 커진다. 그래서 네트워크 세션 차단 이슈, 레벨 1차, 대형 클래스 리팩터링을 같은 차수에 배치했다.

<details>
<summary>수치 재측정 명령</summary>

```bash
echo "기준 시점:  $(git log -1 --format='%h %ad' --date=short)"
echo "커밋:       $(git log --oneline | wc -l)"
echo "스크립트:   $(git ls-files 'Assets/_Project/Scripts/**/*.cs' | wc -l)파일 / $(git ls-files 'Assets/_Project/Scripts/**/*.cs' | xargs cat | wc -l)줄"
echo "테스트:     $(git ls-files 'Assets/_Project/Tests/**/*.cs' | wc -l)파일 / $(git ls-files 'Assets/_Project/Tests/**/*.cs' | xargs cat | wc -l)줄"
echo "문서:       $(git ls-files 'docs/**/*.md' | wc -l)"
# 테스트 통과 개수는 Unity Test Runner에서 확인한다
```
</details>

## 설계 원칙

- **단방향 어셈블리 의존성** — `Utilities ← Core ← Systems ← Gameplay ← UI`. 역방향 참조를 컴파일 단계에서 차단한다.
- **시스템 간 직접 참조 금지** — 통신은 `EventBus<T>`, 전역 접근은 `ServiceLocator`를 경유한다.
- **스폰/소멸 일원화** — `Instantiate`/`Destroy` 직접 호출 대신 `PoolManager.Spawn/Despawn`. 웨이브 방어에서 GC 스파이크를 없애기 위한 규약이다.
- **규칙을 문서로 고정** — SOLID 준수 규칙과 아키텍처 규칙을 문서화하고, 새 코드가 그 규칙을 지키는지 리뷰 기준으로 삼는다.
- **CI** — GitHub Actions에서 EditMode·PlayMode 테스트를 자동 실행한다.

### 기술 부채 관리 — 대형 클래스 5건

현재 대형 클래스 5건은 순수 계산 복잡도보다 MonoBehaviour 배선, UI 상태, 네트워크 권한 분기가 한 파일에 집중된 문제다.

| 파일 | 줄 수 | 집중된 책임 |
|---|---:|---|
| `Gameplay/Train/TrainState.cs` | 1,531 | 상태 보유 · 조회 · 손상 · 수리 · 건축 3종 · 재결합 · 이탈 시뮬 · 클라이언트 보간 |
| `UI/InventoryHud.cs` | 1,169 | 이벤트 구독 19개 · `OnGUI` 패널 7개 · 표시 문자열 조립 |
| `Gameplay/Train/RepairHammerController.cs` | 1,117 | 상호작용 모드 4종 · 조준 4종 · ServerRpc 7개 |
| `Gameplay/Harpoon/HarpoonController.cs` | 936 | 소유자 예측 · 서버 권위 · 원격 연출 — **실행 주체 3개** · RPC 17개 |
| `UI/Ready/ReadyScreenRoot.cs` | 866 | 세션 감시 · 화면 상태 · 내비 배선 · 토스트 · 개발 빌드 도구 |

순수 로직은 이미 `*Math`/`*Logic` 35파일로 분리했고, 테스트 선언 844개가 회귀 기준을 제공한다.
남은 작업은 기능 추가가 아니라 런타임 컴포넌트와 UI 프레젠터를 역할별로 나누는 구조 정리다.

리팩터링은 2026.09 「기반 안정화」 범위에 포함했다(9일 · 순서와 완료 기준 확정).
M7 최종장 구현 전에 `TrainState`와 `RepairHammerController`의 책임 경계를 먼저 분리해 이후 엔드게임 기능의 회귀 범위를 줄인다.
진단·분해 순서·완료 기준은
**[리팩터링 조사 보고서](docs/plans/features/리팩터링-조사-보고서.md)** 에 있다.

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

3. **무기 파지 애니메이션 3종을 Mixamo에서 받는다.** Adobe 약관상 재배포가 금지돼
   저장소에 포함하지 않았다. 절차는 [`Art/Animations/NOTICE.md`](Assets/_Project/Art/Animations/NOTICE.md)
   — 받아서 같은 이름으로 두면 Animator 참조가 그대로 살아난다.
   (없어도 프로젝트는 열린다 — 클립을 이름으로 참조하는 C# 코드가 없어서 컴파일과
   EditMode 테스트에는 영향이 없다. `AC_Player`의 파지 모션 슬롯 3곳만 비어 있게 된다.)

## 라이선스

**이중 라이선스**다 — 코드는 열려 있고, 게임 에셋은 이 프로젝트 전용이다.

| 대상 | 라이선스 |
|---|---|
| **소스 코드 · 문서** — `Scripts/**` · `Tests/**` · `Art/Shaders/**` · `docs/**` | **[MIT License](LICENSE)** — 자유롭게 읽고 가져다 쓸 수 있다 |
| **게임 에셋** — 모델 · 텍스처 · 머티리얼 · 프리팹 · 씬 · 밸런스 데이터 | **[All Rights Reserved](LICENSE-ASSETS)** — 열람·포크·로컬 빌드만 허용 |
| **제3자 저작물** | 각 원저작자의 라이선스 — **[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)** |

파일별 적용 범위는 [`LICENSE`](LICENSE)의 "적용 범위"가 정한다. 경로가 겹치면 제3자 저작물이 우선한다.

### 외부 라이브러리·에셋 출처

| 구분 | 항목 | 라이선스 |
|---|---|---|
| 엔진 | Unity 6000.5.3f1 · 공식 패키지 (URP · Netcode for GameObjects · Input System · uGUI · Timeline · Test Framework) | Unity Companion License / Unity Package Distribution License |
| 네트워크 | [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) 2025.164.1 — Steam 로비·릴레이·업적 | MIT · Valve Steamworks SDK는 [별도 약관](https://partner.steamgames.com/documentation/sdk_access_agreement) |
| 개발 도구 | [MCP for Unity](https://github.com/CoplayDev/unity-mcp) 10.1.0 — **에디터 전용, 게임 빌드 미포함** | MIT |
| 폰트 | Noto Sans KR (v2.004) · Liberation Sans (TMP 동봉) | SIL OFL 1.1 — 전문 동봉 |
| **3D 모델 · 텍스처** | ChatGPT 이미지 → **Tripo AI / Meshy** (image-to-3D) → Blender 정규화·감축 | Tripo Pro · Meshy Pro — **상업 이용 허용 등급** ([근거](THIRD-PARTY-NOTICES.md)) |
| 애니메이션 | 캐릭터 기본 동작 = Tripo 리깅 프리셋 | Tripo Pro — 상업 이용 허용 |
| 무기 파지 3종 | **Adobe Mixamo** — 약관상 재배포 금지라 **저장소에 포함하지 않는다** | 상업 이용 무료 · [반입 절차](Assets/_Project/Art/Animations/NOTICE.md) |

전체 목록과 각 항목의 근거는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)에 있다.

### 직접 만든 것 / 가져온 것

**직접 제작** — 게임플레이 코드 전부(322 파일 · 43,913줄) · 테스트(71 파일 · 10,915줄) ·
셰이더(지역 하늘 · 양식화 물) · 게임 디자인과 설계 문서 95개 · 머티리얼 · 프리팹 구성 · 씬 배치 ·
애니메이터 스테이트 머신 · 밸런스 데이터.

**가져온 것** — **3D 모델과 텍스처의 원본 메시는 AI 생성물이다.** 직접 모델링하지 않았다.
Blender에서의 정규화 · 폴리곤 감축 · 머티리얼 통합 · 리깅 검수와, Unity 반입 후의
프리팹 구성 · 콜라이더 · 소켓 배치는 직접 했다. 무기 파지 애니메이션 3종은 Mixamo 클립을
리타게팅한 것이며, **약관상 재배포가 금지돼 저장소에는 없다** — 받는 방법만
[`Art/Animations/NOTICE.md`](Assets/_Project/Art/Animations/NOTICE.md)에 적어 뒀다.
클론 후 빌드하려면 이 절차가 필요하다.

> 생성형 3D 도구 출력물의 상업 이용 권리는 구독 등급에 달려 있다. Tripo AI와 Meshy 모두
> **상업 이용이 허용되는 Pro 등급**에서 생성했다 — 근거는
> [THIRD-PARTY-NOTICES.md §5.4](THIRD-PARTY-NOTICES.md).
