# 낮/밤 사이클 (호스트 권위 누적 시간 + 순수 파생)

> **종류**: 아키텍처 명세 · **상태**: 구현 완료 (M2 골격 → M4 난이도 입력 → M7 밤 홀드 → M8 2차 시각 연출)
> **최종 갱신**: 2026-08-20 · **관련 기획서**: [Train-Survival-기획서](../../design/Train-Survival-기획서.md) · [개발 가이드 §5 M2·M8](../../guide/Train-Survival-개발-가이드.md) · [M8 2차 구현 계획](../../plans/M8/M8-2차-구현-계획.md)

## 1. 개요·목적

M2 코어 루프의 시간축이다. **낮 → 밤 → Day+1**을 반복하는 타임라인을, 호스트가 소유하는
단일 누적 시간(`_totalSeconds`)에서 모든 피어가 동일하게 파생하도록 구현했다. 국면 전환·Day 증가
같은 이산 사건만 이벤트로 발행하고, 나머지 상태(현재 국면·남은 시간)는 매 프레임 순수 계산으로 유도한다.

## 2. 범위 (Scope)

**포함**: 호스트 권위 타임라인 구동(`DayCycleController`), 누적 시간 → (Day·국면·경과·남은 시간) 순수
파생(`DayTimelineMath`), 국면 전환 권위 이벤트(`DayPhaseChangedEvent`), 조회 서비스(`IDayCycleService`),
낮/밤 길이 밸런스 데이터(`DayTimelineSettings`).

**M7·M8에서 추가된 포함 범위**: 밤 종료 홀드 게이트(`INightHoldGate`·`NightHoldMath` — M7),
**낮/밤 시각 연출**(`DayCycleVisualController`·`DayVisualMath`·`DayVisualSettings`·
`RenderEnvironmentSnapshot` — M8 2차), 시간 배속 QA 키.

**미포함**: 밤 진입에 연동되는 몬스터 웨이브 트리거(→ [monsters](../monsters/wave-and-steering.md)가
`IDayCycleService`/이벤트를 소비), **안개**(`RenderSettings.fog*`는 `WeatherVisualController` 단독 소유 —
[region/weather-events](../region/weather-events.md)), Day 비례 난이도(→ [region](../region/region-timeline.md)).

## 3. 요구사항 → 설계 해석

| 요구사항 | 출처 | 설계적 해석 |
|---|---|---|
| Day 타임라인은 호스트 권위 | 네트워크 문서 §4, 가이드 M2 | 시간 누적은 `IsServer`만 수행, `NetworkVariable<float> _totalSeconds` 기본 복제로 전파 |
| 모든 피어가 같은 국면을 본다 | 코어 루프 정합성 | 국면 판단을 각 피어가 같은 `_totalSeconds`에 `DayTimelineMath.Evaluate`를 돌려 유도 — 전이 판단이 갈라지지 않음 |
| 밸런싱은 코드 수정 없이 | 가이드(밸런스 데이터 분리) | 낮/밤 길이를 `DayTimelineSettings`(ScriptableObject)로 분리, `Min(10f)` 제약 |
| UI는 상태를 소유하지 않는다 | 아키텍처 규칙 §3 | 전환은 `DayPhaseChangedEvent`로 발행, 연속값은 `IDayCycleService` 조회로만 노출(setter 없음) |

## 4. 시스템 구조

| 구성요소 | 역할 | 계층 |
|---|---|---|
| `DayCycleController` | 호스트 시간 누적 + 매 프레임 파생·이벤트 발행, `IDayCycleService` 구현 | `NetworkBehaviour` |
| `DayTimelineMath` | 누적 시간 하나에서 전체 상태를 유도하는 순수 평가 | 순수 C# static |
| `DayTimelineState` | 평가 결과 (Day 번호·국면·경과·국면 길이) | 순수 C# struct |
| `DayPhase` | Day/Night 열거 (`byte` 기반) | 순수 C# enum |
| `DayPhaseChangedEvent` | 국면 전환 권위 이벤트 | 순수 C# struct |
| `IDayCycleService` | Day·국면·남은 시간 조회 계약 | 인터페이스 |
| `DayTimelineSettings` | 낮/밤 국면 길이 밸런스 데이터 | `ScriptableObject` |

```mermaid
classDiagram
    class DayCycleController {
        -NetworkVariable~float~ _totalSeconds
        -DayTimelineState _state
        -bool _hasEvaluated
        +int DayNumber
        +DayPhase Phase
        +float PhaseRemaining
        +float PhaseDuration
    }
    class DayTimelineMath {
        <<static>>
        +Evaluate(totalSeconds, dayDuration, nightDuration) DayTimelineState
    }
    class DayTimelineState {
        +int DayNumber
        +DayPhase Phase
        +float PhaseElapsed
        +float PhaseDuration
        +float PhaseRemaining
    }
    class DayPhase {
        <<enumeration>>
        Day
        Night
    }
    class IDayCycleService {
        <<interface>>
        +int DayNumber
        +DayPhase Phase
        +float PhaseRemaining
        +float PhaseDuration
    }
    DayCycleController ..|> IDayCycleService : ServiceLocator 등록
    DayCycleController --> DayTimelineMath
    DayCycleController --> DayTimelineSettings
    DayTimelineMath --> DayTimelineState
    DayTimelineState --> DayPhase
    DayCycleController ..> DayPhaseChangedEvent : EventBus 발행
```

## 5. 데이터 구조

### `DayTimelineSettings`

| 필드 | 기본값 | 의미 |
|---|---|---|
| `DayDurationSeconds` | 240 s | 낮 국면 길이 (`Min(10)`) |
| `NightDurationSeconds` | 150 s | 밤 국면 길이 (`Min(10)`) |

### `DayTimelineState` (파생 struct)

| 멤버 | 의미 |
|---|---|
| `DayNumber` | 1부터 시작하는 현재 Day 번호 |
| `Phase` | 현재 국면(Day/Night) |
| `PhaseElapsed` / `PhaseDuration` | 현재 국면 내 경과·국면 총 길이 |
| `PhaseRemaining`(파생) | `max(0, PhaseDuration - PhaseElapsed)` |

## 6. 상세 로직·상태

### 6.1 누적 시간 → 상태 파생 (`DayTimelineMath.Evaluate`)

`Evaluate(totalSeconds, dayDuration, nightDuration)` — 순수 함수, 부작용 없음.

```
cycleDuration = dayDuration + nightDuration
clamped       = max(0, totalSeconds)          // 음수 시간은 시작(0)으로 고정
cycleIndex    = (int)(clamped / cycleDuration)
timeInCycle   = clamped - cycleIndex * cycleDuration
DayNumber     = cycleIndex + 1                 // 1부터 시작

timeInCycle < dayDuration
  → Phase = Day,   Elapsed = timeInCycle,             Duration = dayDuration
  → 그 외 Night,   Elapsed = timeInCycle - dayDuration, Duration = nightDuration
```

### 6.2 구동·발행 루프 (`DayCycleController.Update`)

```mermaid
sequenceDiagram
    participant Server as 호스트
    participant Var as _totalSeconds (NetworkVariable)
    participant Peer as 모든 피어(호스트 포함)

    Server->>Var: IsServer일 때 += Time.deltaTime
    Var-->>Peer: 기본 복제로 값 전파
    loop 매 Update (모든 피어)
    Peer->>Peer: DayTimelineMath.Evaluate(_totalSeconds)
    alt 첫 평가 || DayNumber 변경 || Phase 변경
    Peer->>Peer: EventBus<DayPhaseChangedEvent>.Publish(...)
    end
    end
```

- **왜 RPC가 없나**: 상태 전이는 시간의 함수이므로, 하나의 값(`_totalSeconds`)만 복제하면 각 피어가
  같은 국면을 독립 유도할 수 있다. 전용 전환 RPC를 두지 않아 대역폭과 순서 문제를 회피한다.
- **엣지 케이스 — 첫 평가 강제 발행**: `_hasEvaluated` 플래그로 첫 `Update`에서는 무조건
  `DayPhaseChangedEvent`를 발행해 구독자(HUD 등)가 초기 국면을 즉시 받도록 한다.
- **엣지 케이스 — 설정 누락 방어**: `_settings == null`이면 `Update`/`EvaluateAndPublish`가 조기 반환한다.
- **엣지 케이스 — 음수 시간 고정**: `Evaluate`가 `max(0, …)`로 클램프해 어떤 경우에도 Day1 낮 이전으로 가지 않는다.

### 6.4 시각 연출 (M8 2차) — 게임플레이를 한 줄도 바꾸지 않는다

**목표**: 낮 → 밤 전환이 한 프레임 도약이 아니라 **시간의 경과로 읽힌다.**

이 축의 설계 제약이 전부다 —

> `DayPhase`·`DayPhaseChangedEvent`·`IDayCycleService`를 **읽기만** 하고, 새 네트워크 상태를 만들지
> 않으며, 산출물은 `RenderSettings`와 `Light` 하나에 대한 **순수 로컬 표현**이다.
> 호스트·클라가 서로 다른 연출 모드여도 게임플레이가 갈라지지 않는다.

| 구성요소 | 역할 |
|---|---|
| `DayCycleVisualController` | 국면 진행도 → 환경광·태양·하늘 적용 (`MonoBehaviour`, 로컬) |
| `DayVisualMath` | 색·강도·각도 보간 순수 계산 (`AmbientTone.Lerp`) |
| `DayVisualSettings` | 낮/밤 ambient 3색(sky·equator·ground)·강도·태양 고도/방위 곡선 |
| `DayVisualMode` | `Off / A / B` — 런타임 토글 |
| `RenderEnvironmentSnapshot` | 원래 렌더 설정 백업·복원 |

**입력**: `t = 1 − PhaseRemaining ÷ PhaseDuration` — 인터페이스 무수정으로 진행도를 얻는다.

**A안 / B안**

| | 내용 | 그림자맵 재생성 | 오브젝트 수 민감도 |
|---|---|---|---|
| **A안** | 국면 전환 시 ambient를 수 초(`_fadeSeconds` 6 s)에 걸쳐 보간 | 없음 | 없음 |
| **B안** | 국면 진행도로 태양 각도(고도 10→80→8°)·색·강도를 상시 보간 | **매 프레임** | 있음 |

착수 준비는 A안(2차)·B안(4차)으로 나눴으나, **산출물이 컨트롤러 1개 + 모드 필드**라 계획을 하나로
통합했다. B안의 *활성화*에만 3차 렌더 실측 통과를 조건으로 건다.

> **ambient 함정 2건** (구현 중 발견): `AmbientMode.Skybox`는 **색 설정을 무시**하고,
> `Trilight`는 **강도 설정을 무시**한다. 모드에 맞는 필드만 써야 의도한 결과가 나온다.

### 6.5 시간 배속 QA 키

국면 점프(숫자패드 1·2·3)는 "전환이 일어난 상태"만 보여주고 **흐름은 보여주지 못한다.**
시각 연출을 검증하려면 시간이 흐르는 걸 빠르게 봐야 해서 배속 키를 추가했다.

## 7. 인터페이스·의존성 (경계)

- **`IDayCycleService`** — `OnNetworkSpawn`에서 미등록일 때만 `ServiceLocator.Register`, `OnNetworkDespawn`에서
  자기 자신이 등록된 인스턴스일 때만 `Unregister`. 소비자(HUD·웨이브 스포너)는 구현을 모르고 조회만 한다.
- **권위 이벤트 vs 조회**: 이산 전환은 `DayPhaseChangedEvent`(권위 이벤트), 연속값(남은 시간 카운트다운
  등)은 `IDayCycleService` 폴링 조회로 분리 — 매 프레임 이벤트를 쏘지 않는다.
- 이 도메인은 어떤 다른 도메인도 참조하지 않는다(시간축 원천). 밤 웨이브·연출이 역으로 이 도메인을 소비한다.

## 8. 설계 포인트 (SOLID)

| 원칙 | 적용 |
|---|---|
| **SRP** | 시간 누적·네트워크(`DayCycleController`)와 상태 파생 수식(`DayTimelineMath`)을 분리 |
| **DIP** | 소비자는 `IDayCycleService`로만 시간을 읽어 컨트롤러 구현에 결합되지 않음 |
| **강조 패턴 — 순수 로직의 조기 분리** | 국면 전이 전 로직을 static 순수 함수로 빼 물리·네트워크 없이 EditMode에서 전 경계 검증 |

## 9. Unity 특화

- **생명주기**: `Game` 씬에 1개 배치 의도. `OnNetworkSpawn`에서 서비스 등록, `OnNetworkDespawn`에서 해제.
- **풀링**: 대상 없음(단일 상시 오브젝트).
- **성능 예산**: 매 프레임 순수 산술 1회 + 조건 비교. 할당 없음.
- **에디터 툴 필요 여부**: 없음. 밸런스는 `DayTimelineSettings.asset` 수정으로 조정.

## 10. 테스트 케이스

| 테스트 파일 | 검증 항목 |
|---|---|
| `DayTimelineMathTests` (6개) | ① 시작=Day1 낮 ② 낮 길이 경과 후 밤 전환 ③ 한 사이클 후 Day+1 낮 ④ 남은 시간=길이−경과 ⑤ 음수 시간 시작 고정 ⑥ 다중 사이클 후 정확한 Day 번호 |

컨트롤러/네트워크 복제는 EditMode 대상 밖 — 파생 수식(전이 경계·클램프·Day 번호)만 순수 함수로 검증한다.

## 11. 리스크·미결정 (TBD)

| 항목 | 내용 |
|---|---|
| **M8 2차 플레이 검증 대기** | 구현·EditMode(620/620)는 통과, 플레이 검증 미실시 — [M8 2차 검증 항목](../../plans/M8/M8-2차-플레이-검증-항목.md) |
| B안 활성화 조건 | 3차 렌더 실측(캐스케이드 축소·배경 LOD) 통과가 선행 |
| 낮/밤 길이 밸런스 | 현재 240/150 s는 초기값 — 코어 루프 반복 밸런싱(가이드 §2)에서 데이터로 조정 예정 |
| Day 비례 난이도 연동 → **M4에서 구현** (2026-08-01) | Day 번호를 `RegionTimelineMath`가 지역·지역 내 일차로 파생하고, `WaveMath`가 지역 배율·마지막 밤 배율과 함께 총량·간격·동시 상한·체력 배율을 산출한다. 이 도메인은 무수정 — Day 번호와 `DayPhaseChangedEvent`가 그대로 입력이 됐다 |

## 12. 확장 여지

- `DayPhase`에 값을 추가하고(예: 황혼) `DayTimelineMath`의 구간 판단만 확장하면 국면 세분화가 가능 —
  소비자는 이벤트/서비스 경계 그대로 유지.
- Day 번호가 M4 지역 전환·난이도 곡선의 입력으로 그대로 재사용된다.

## 13. 파일 위치

| 구분 | 파일 | 경로 |
|---|---|---|
| 컨트롤러 | `DayCycleController.cs` | `Assets/_Project/Scripts/Gameplay/Cycle/` |
| 순수 로직 | `DayTimelineMath.cs`, `DayPhase.cs` | 〃 |
| 이벤트 | `CycleEvents.cs` | 〃 |
| 인터페이스 | `IDayCycleService.cs` | 〃 |
| 데이터 | `DayTimelineSettings.cs` (+ `.asset`) | 〃 (+ `Assets/_Project/Data/`) |
| 테스트 | `DayTimelineMathTests.cs` | `Assets/_Project/Tests/EditMode/` |
