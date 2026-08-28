# Train Survival

[![CI](https://github.com/hanwoolhanwool/Train-Survival/actions/workflows/ci.yml/badge.svg)](https://github.com/hanwoolhanwool/Train-Survival/actions/workflows/ci.yml)

> **멈추면 죽는다.** 끊임없이 달리는 열차 위에서 자원을 낚아채고, 밤마다 몰려드는 몬스터로부터 집을 지키며, 마침내 우주로 탈출한다.

**협동 생존 크래프팅 · 1~4인 온라인 · 1인칭 · PC / Steam**
`Unity 6 (6000.5.3f1)` · `URP 17.5` · `Netcode for GameObjects` · `Input System`

<!-- 대표 화면 — 스크린샷 준비되면 아래 주석을 풀고 파일을 docs/images/ 에 넣는다
![Train Survival](docs/images/hero.png)
-->

> 설계 결정과 문제 해결 과정을 정리한 **[프로젝트 소개 문서](https://hanwoolhanwool.github.io/portfolio/train-survival-portfolio.html)** 가 따로 있다.

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

1인 개발 · **2026.07.17 착수** ~ 진행 중 (43일차)

| | |
|---|---|
| 커밋 | 757 |
| 스크립트 | 403개 · 57,688줄 (`Assets/_Project/Scripts`) |
| 테스트 | EditMode 1,126개 **전부 통과** (테스트 파일 100개 · 15,442줄) |
| 어셈블리 | 런타임 5개 + 에디터 1개 + 테스트 2개 — 의존 방향 단방향 고정 |
| 문서 | 114개 (`docs/`) — 기획서 · 네트워크 아키텍처 · 세계관 · 레벨 디자인 · 비주얼/UIUX · 오디오 · 아트 예산 |
| 마일스톤 | M3 열차 파괴 → M4 지역·날씨 → M5 전투·생존 → M6 Steam 연동 → M7 엔드게임 → M8 아트 패스 → **레벨 디자인·지역 확장**(진행 중) |

<sub>측정 2026-08-28 · `main` = `03f09f2` 기준. EditMode 수치는 Unity Test Runner 실행 결과(1,126/1,126 · 11.2초)다.
개발이 진행 중이라 수치는 계속 바뀐다. 집계는 `git ls-files` 기준이며 커밋되지 않은 로컬 파일은 세지 않는다.</sub>

**2026.08 후반에 선 것** — 레벨 디자인 1·2차로 배경이 프리미티브에서 실제 지형으로 바뀌었다.
선로변 세그먼트 팔레트(숲 10종) · 지역 하늘과 양식화 물 셰이더 · 지면 텍스처로 단색 초록을 지웠고,
트리라인 3겹으로 타일 바깥의 스카이박스를 물렸다. 같은 기간에 승하차 사다리 · 화구형 연료구 ·
몬스터 모델 교체 · 카테고리 로그 인프라(`GameLog`)가 들어갔고,
**첫 StandaloneWindows64 빌드를 구워 기동까지 확인했다** — 에디터 밖에서 켜지는 것을 처음 본 시점이다.

**8월 마지막 주에는 지형이 평면을 벗어났다.** 다섯 번째 지역 **바다**를 세우면서 열차가
해상 교량을 달리고, 플레이어가 **수영·잠수**로 물에 들어가며 **끌낚시**로 식량을 얻는다 —
*"지면은 y = 0 평면"* 이라는 4지역 공통 전제를 처음 깬 지역이다. 같이 들어온 것은 선로변
**기차역**(연속 추첨 · 등급별 전리품) · 열차에 설치하는 **거치 무기**(소유가 아니라 점유) ·
겹쳐 뜨던 안내를 겨눈 것 하나로 좁힌 **상호작용 중재**다.

## 일정

남은 일은 각 마일스톤의 **잔여 작업 정리**·**플레이 검증 항목**·**미결 사항 추적표**에서 추렸다.
순서는 난이도가 아니라 **무엇이 무엇을 막고 있는가**로 정했다.

| 시점 | 목표 | 주요 항목 |
|---|---|---|
| **2026.09** | **기반 정리** — 릴리스 차단 요소 0 | 레벨 1·2차 플레이 검증(저작 기반은 8월에 완료) · **바다·기차역·거치 무기 검증 부채**(8월에 코드는 섰고 검증이 밀렸다) · 로비·준비 화면 검증 부채 · NGO 세션 재시작 해결 · 세션 이탈 시 클라 잔존 · 보스 패턴 4인 미발동 · Steam 실계정 로비·초대·릴레이 검증(M6 완료 기준) · **대형 클래스 5건 리팩터링**(9일 — [근거·순서](docs/plans/features/리팩터링-조사-보고서.md)) |
| **2026.10** | **엔드 콘텐츠 완결** — 처음부터 끝까지 이어진다 | M7 최종장(궤도 도약·가속 방어전·우주) · 뉴게임+ 순환 · 터렛(전투 범위 마지막 미구현) · 대기실 초대·게스트 권한 · M7 변경 요청 4건 · 미결 확정(몬스터 상한·부활 n값·수리 비용) |
| **2026.11** | **세계를 채운다** — 지역이 색이 아니라 지형으로 읽힌다 | M8 3차 렌더 실측(Frame Debugger·GPU RD) → 숲에서 유보한 예산 판정 재개(타일당 tris·활성 9장·교체 스파이크) → 레벨 4차 나머지 3지역 30종 · 오디오 인프라와 사운드 반입 · UI 정리 |
| **2026.12** | **검증과 밸런싱** — 남의 손에서 굴러간다 | 플레이 검증 부채 청산 · **4인 세션 역할 분담 검증**(M5 완료 기준, 미실시) · 외부 4인 플레이테스트 · 밸런싱 반영 |
| **2027.01 초** | 출시 준비 | 릴리스 후보 빌드 · 스토어 페이지 · 트레일러 · 자체 AppID 발급 |
| **2027.01 중순** | **개발 완료 — 기능 동결** | 이후 버그 수정만. 출시까지 약 3주 완충 |
| **2027.02 초** | **출시 — 설 연휴** | 2027년 설날 2월 6일. 연휴 직전 출시 + 런칭 할인 |

출시일에서 거꾸로 짠 일정이다. 협동 게임은 같이 할 사람이 있을 때 팔리므로 **연휴가 이 게임에는 최적의 창**이고,
남은 5개월 중 **마지막 2개월을 개발이 아니라 다듬기와 검증에 배정**했다.

**9월이 이 순서인 이유** — 8월에 전제 하나가 없어졌다. 궤도 타일과 열차 높이를 얹은 `Game_ArtTest`를
본편으로 이식하는 대신 **그 씬을 본 씬으로 승격**시켰다. 옮기는 비용이 승격보다 비쌌고,
덕분에 8월 내내 이식을 기다리지 않고 그 위에 아트를 계속 부을 수 있었다.
남은 9월 항목은 그릇이 아니라 **혼자서는 재현되지 않는 것들**이다 — 세션 재시작 · 이탈 처리 ·
4인 보스 패턴 · Steam 실계정은 전부 두 대 이상이 붙어야 드러난다. 첫 PC 빌드를 8월에 구운 것도
이 검증을 위한 선행 작업이었다.
**대형 클래스 5건 리팩터링을 9월에 넣은 것도 같은 논리다** — 10월 최종장이 그 위에 올라타고 나면
분해 대상도 회귀 범위도 커진다. 12월은 검증, 1월 중순은 기능 동결이라 뒤에는 자리가 없다.

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

### 알려진 한계 — 대형 클래스 5건

원칙을 내세우는 이상 지키지 못한 곳도 같이 적는다. **배선이 다섯 파일에 몰려 있다.**

| 파일 | 줄수 | 한 클래스가 지는 것 |
|---|---:|---|
| `Gameplay/Train/TrainState.cs` | 1,551 | 상태 보유 · 조회 · 손상 · 수리 · 건축 3종 · 재결합 · 이탈 시뮬 · 클라 보간 |
| `Gameplay/Player/NetworkPlayerController.cs` | 1,469 | 이동·시점 · 사다리 2종 · 수영/잠수 · 좌석 · 견인 — **상태 분기가 한 파일에** |
| `UI/InventoryHud.cs` | 1,215 | 이벤트 구독 19개 · `OnGUI` 패널 7개 · 표시 문자열 조립 |
| `Gameplay/Train/RepairHammerController.cs` | 1,117 | 상호작용 모드 4종 · 조준 4종 · ServerRpc 7개 |
| `Gameplay/Harpoon/HarpoonController.cs` | 936 | 소유자 예측 · 서버 권위 · 원격 연출 — **실행 주체 3개** · RPC 17개 |

**계산이 뭉친 게 아니라 배선이 뭉쳤다.** 순수 로직은 이미 `*Math`/`*Logic` 50파일로 빠져 있고
EditMode 테스트 1,126개가 그 위에 서 있다. 그래서 "로직을 더 빼라"는 처방은 이 코드에 듣지 않는다 —
필요한 것은 **역할별 컴포넌트 분해**다.

> **8월에 목록이 한 줄 바뀌었다.** 바다 지역의 수영·잠수·사다리가 들어가면서
> `NetworkPlayerController`가 2위로 올라왔고, `ReadyScreenRoot`(863줄)가 5위 밖으로 밀렸다.
> 판정은 여전히 순수 함수(`SeaLadderMotion`·`SwimMotion`)가 갖고 있으므로 **늘어난 것은 배선**이다 —
> 이 절이 말하는 문제가 그대로 재현된 사례다.

**2026.09 「기반 정리」 차수에서 5건 전부 리팩터링한다** (9일 · 순서와 완료 기준 확정).
10월 M7 최종장이 `TrainState`·`RepairHammerController` 위에 직접 올라타므로, 그 전이
분해할 수 있는 마지막 시점이다. 진단·설계안·비용은
**[리팩터링 조사 보고서](docs/plans/features/리팩터링-조사-보고서.md)** 에 있다.

## 문서

| 문서 | 내용 |
|------|------|
| [기획서](docs/design/Train-Survival-기획서.md) | 게임 개요 · 코어 루프 · 시스템 전반 |
| [네트워크 아키텍처](docs/design/Train-Survival-네트워크-아키텍처.md) | NGO + Steam 스택, 동기화 설계 |
| [세계관 컨셉](docs/design/Train-Survival-세계관-컨셉.md) | 세계·서사·연출 원칙 |
| [레벨 디자인 가이드](docs/design/Train-Survival-레벨디자인-가이드.md) | 선로변 세그먼트 규격 · 클리어 존 · 지역 팔레트 |
| [계획 지도](docs/plans/README.md) | 마일스톤·기능별 구현 계획과 플레이 검증 기록 |
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

**직접 제작** — 게임플레이 코드 전부(403 파일 · 57,688줄) · 테스트(100 파일 · 15,442줄) ·
셰이더(지역 하늘 · 양식화 물) · 게임 디자인과 설계 문서 114개 · 머티리얼 · 프리팹 구성 · 씬 배치 ·
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
