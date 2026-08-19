# 집게(하푼) 그랩 파이프라인

> **종류**: 아키텍처 명세 · **상태**: 구현 완료 (M1 슬라이스 → M5 등급·그랩 → 파지 전환)
> **최종 갱신**: 2026-08-20 · **관련 기획서**: [Train-Survival-기획서](../../design/Train-Survival-기획서.md) · [수직 슬라이스 스펙](../../design/Train-Survival-수직슬라이스-스펙.md)

## 1. 개요·목적

집게 발사 → 로컬 명중 판정 → 호스트 그랩 검증 → 견인 → 획득으로 이어지는 파이프라인의 실제 구현을 기록한다.
슬라이스 스펙 §2가 정의한 "로컬 선반영 + 호스트 권위" 구조를 그대로 따르되, 구현 중 두 가지가 스펙 원안 대비 확장됐다:
① 실패(빗나감·거부) 시 훅이 즉시 사라지지 않고 총구로 되돌아가는 연출, ② 발사·견인 모습을 다른 클라이언트에도 브로드캐스트.

## 2. 범위 (Scope)

**포함**: 발사 입력 게이트(`HarpoonStateMachine`), 훅 비행·충돌·되감기(`HarpoonProjectile` + `HarpoonHookMotion`), 호스트 검증 규칙(`GrabValidation`), 견인 시뮬레이션, 로프 연출(`HarpoonRopeRenderer`), 발사·미스·견인의 비소유 클라이언트 브로드캐스트, 그랩 대상 공용 계약(`IGrabbable`), 손맛 검증 계측(`HarpoonSliceMetrics` + `TowMotionAnalyzer` — 에디터·개발 빌드 전용, 릴리스에서는 `[Conditional]`로 호출 제거).

**M5 이후 추가된 포함 범위**:
- **2·3단계 집게** (M5 5차) — 승급은 제작 경로(`IHarpoonTierHolder`). 등급이 **잡을 수 있는 대상**을 정한다
- **몬스터 그랩** (M5 5·6차) — `IGrabbable`을 몬스터가 구현(`MonsterGrabTarget`). **파이프라인은 그대로 재사용**되고
  달라지는 것은 도착 후 동작뿐이다 (→ [monsters §6.6](../monsters/wave-and-steering.md))
- **보따리 일괄 회수** (M5 8차) — 3단계 집게는 **비행 중에도 낚아챈다**(등급 예외)
- **등급별 파지 손 전환** (2026-08-19) — `HarpoonSwitchRules`가 대상 등급 축과 전환 게이트를 판정
  (→ [player §6.2](../player/network-movement.md))

**미포함**:
- 그랩 대상 자체 구현 — 자원(`ResourceNode`)은 [world 도메인](../world/scroll-and-streaming.md) 소관.
- 발사음·팔 애니메이션 등 실제 리소스 — 로컬 표현 이벤트(`HarpoonFiredLocalEvent`)만 발행하고 구독 측(연출·오디오)은 이 문서 밖.
- ~~정성 블라인드 테스트~~ — **통과 (2026-07-22)**: 정량(Q1~Q5)·정성 모두 종결로 손맛 검증 전체 완료 (§11 참조).

## 3. 요구사항 → 설계 해석

| 요구사항 | 출처 | 설계적 해석 |
|---|---|---|
| 발사 즉시 로컬 연출 (지연 0) | 슬라이스 스펙 §2.4 | 입력 → `EventBus<HarpoonFiredLocalEvent>` 발행과 로컬 훅 스폰을 같은 프레임에서 동기 수행 |
| 명중 판정 = 쏜 클라이언트, 확정 = 호스트 | 권위 분담표 | `HarpoonProjectile`이 로컬 SphereCast로 판정 → `RequestGrabServerRpc`로 보고 → `GrabValidation`이 서버에서 확정 |
| 취소는 릴 감기 중에만, 페널티 없이 쿨다운만 | 슬라이스 스펙 §2.1 | `HarpoonStateMachine.TryCancel()`이 `Reeling` 상태만 허용, 성공 시 `Cancel()`(즉시 소멸 = "로프 절단") |
| 거부 시 "로프가 미끄러져 빠지는" 연출 | 슬라이스 스펙 §2.4 | (사용자 추가 요구로 구체화) `BeginRetract()` — 피격 정지 없이 즉시 되감기 시작 |
| **실패해도 사라지지 않고 총구로 돌아옴** | 사용자 요청 (2026-07-20) | `HarpoonHookMotion`에 `ImpactPause`/`Retracting` 단계 추가, 훅이 총구 Transform을 매 프레임 추적하며 되감김 |
| **다른 플레이어도 발사·견인을 볼 수 있어야 함** | 사용자 요청 (2026-07-20) | 기존 `SendTo.Owner` RPC마다 대응하는 `SendTo.NotOwner` RPC로 코스메틱 훅 브로드캐스트 |

## 4. 시스템 구조

| 구성요소 | 역할 | 계층 |
|---|---|---|
| `HarpoonController` | 입력 게이트 호출, RPC 송수신, 호스트 견인 시뮬레이션 | `NetworkBehaviour` (Gameplay) |
| `HarpoonStateMachine` | 조작 상태(`Ready`~`Cooldown`) 순수 전이 | 순수 C# |
| `HarpoonHookMotion` | 훅 비행 단계(`Idle`~`Retracting`) 순수 전이 | 순수 C# |
| `HarpoonProjectile` | Transform 이동·SphereCast·훅 시각 구동 (소유자 권위 겸 비소유 코스메틱 겸용) | `MonoBehaviour` + `IPoolable` |
| `GrabValidation` | 호스트 승인/거부 판정 순수 함수 | 순수 C# static |
| `HarpoonRopeRenderer` | 로프 `LineRenderer` 연출 (탄성·실패 색상) | `MonoBehaviour` |
| `HarpoonSettings` | 발사·릴·되감기 밸런스 수치 | `ScriptableObject` |
| `IGrabbable` | 그랩 대상 공용 계약 (자원/몬스터) | 인터페이스 (Gameplay) |

```mermaid
classDiagram
    class HarpoonController {
        -HarpoonStateMachine _stateMachine
        -HarpoonProjectile _activeProjectile
        -IGrabbable _serverTowTarget
        +Fire()
        +State HarpoonState
    }
    class HarpoonStateMachine {
        +HarpoonState State
        +TryFire() bool
        +TryCancel() bool
        +NotifyLocalHit()
        +NotifyMiss()
        +NotifyGrabApproved()
        +NotifyGrabRejected()
        +Tick(deltaTime)
    }
    class HarpoonProjectile {
        -HarpoonHookMotion _motion
        +Launch(...)
        +LaunchCosmetic(...)
        +AttachTo(target)
        +BeginRetract()
        +Cancel()
        +IsAlive bool
        +IsWaitingForServer bool
        +IsFailing bool
    }
    class HarpoonHookMotion {
        +HookPhase Phase
        +StartFlying()
        +NotifyGrabbableHit()
        +NotifyMiss()
        +Attach()
        +BeginRetract()
        +NotifyRetractArrived()
        +Tick(deltaTime) bool
    }
    class GrabValidation {
        <<static>>
        +Validate(...) GrabVerdict
    }
    class IGrabbable {
        <<interface>>
        +Kind GrabKind
        +GrabWeight int
        +IsAvailableForGrab bool
        +IsClaimed bool
        +TryClaimGrab(clientId) bool
        +UpdateTowPosition(pos)
        +ReleaseGrab()
        +TryCompleteGrab(completion) bool
    }
    class HarpoonRopeRenderer {
        +Show(start, end, slack, isFail)
        +Hide()
    }
    HarpoonController --> HarpoonStateMachine
    HarpoonController --> HarpoonProjectile
    HarpoonController --> HarpoonRopeRenderer
    HarpoonController --> GrabValidation
    HarpoonController ..> IGrabbable : NetworkObjectReference
    HarpoonProjectile --> HarpoonHookMotion
    IGrabbable <|.. ResourceNode : (world 도메인)
    IGrabbable <|.. HandrailAnchor : (train 도메인)
    IGrabbable <|.. MonsterGrabTarget : (monsters 도메인, M5 5차)
```

## 5. 데이터 구조 — `HarpoonSettings`

기획자가 코드 수정 없이 조정하는 값 전부. 에셋: `Assets/_Project/Data/HarpoonSettings.asset`.

**등급별 값** (M5 5차) — `Tier[]` 배열. 인덱스 0 = 1단계. 등급은 `HarpoonController`가
`NetworkVariable<byte>`로 복제하므로 게스트의 코스메틱 훅도 같은 궤적을 재생한다.

| 필드 | 1단계 | 2단계 | 3단계 | 의미 |
|---|---|---|---|---|
| `MaxRange` | 20 m | 26 m | 32 m | 사거리 (슬라이스 스펙 §2.2) |
| `ReelSpeed` | 8 m/s | 11 m/s | 14 m/s | 견인(릴) 속도 |
| `FireCooldown` | 0.5 s | 0.45 s | 0.4 s | 명중·취소 후 재발사 대기 |
| `MissRecoveryDuration` | 2.5 s | 1.8 s | 1.2 s | 빗나감 페널티 (상위 단계일수록 회수가 빠르다) |
| `GrabWeightLimit` | 1 | 2 | 3 | **낚아챌 수 있는 대상 무게 상한** — `GrabValidation.CanLift`의 등급 인자 |

**등급 공통 값**

| 필드 | 기본값 | 의미 |
|---|---|---|
| `ProjectileSpeed` | 40 m/s | 투사체 비행 속도 |
| `ProjectileRadius` | 0.15 m | SphereCast 반경 |
| `ArriveRadius` | 1.2 m | 이 거리 안으로 끌려오면 도착 확정 |
| `RangeTolerance` | 2 m | 호스트 거리 검증 여유분 |
| `TowInterpolationRate` | 20 | 견인 스냅샷 보간 계수 |
| `RetractSpeed` | 14 m/s | 실패 시 총구로 되돌아가는 속도 |
| `ImpactPauseDuration` | 0.12 s | 피격 직후 되감기 전 정지 시간 |
| `WaitingForServerTimeout` | 1.5 s | 호스트 응답이 끝내 오지 않을 때의 안전 폴백 |

## 6. 상세 로직·상태

### 6.1 훅 비행 단계 (`HarpoonHookMotion`)

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Flying: StartFlying()
    Flying --> WaitingForServer: NotifyGrabbableHit()
    Flying --> ImpactPause: NotifyMiss() (벽·바닥 명중 또는 사거리 소진)
    ImpactPause --> Retracting: Tick() 타이머 만료
    WaitingForServer --> Attached: Attach() (호스트 승인)
    WaitingForServer --> Retracting: BeginRetract() (호스트 거부)
    WaitingForServer --> Retracting: Tick() 안전 타임아웃
    Attached --> Retracting: BeginRetract() (강제 해제)
    Retracting --> Idle: NotifyRetractArrived()
    Attached --> Idle: Cancel() (획득 완료)
    Flying --> Idle: Cancel() (취소)
    WaitingForServer --> Idle: Cancel()
```

- **엣지 케이스 — 취소는 "로프 절단"**: 릴 감기(`Attached`) 중 우클릭 취소는 `BeginRetract()`가 아니라 `Cancel()`을 호출한다 — 슬라이스 스펙 §2.1이 취소를 "되감기"가 아닌 "절단"으로 명시했기 때문. 실패(빗나감·거부)만 되감기 연출을 탄다.
- **엣지 케이스 — 안전 타임아웃**: `WaitingForServer`는 정상적으로 항상 서버 응답(`Attach`/`BeginRetract`)을 받지만, 대상이 이미 소멸된 채 늦게 도착하는 등의 상황을 대비해 `WaitingForServerTimeout` 초과 시 자동으로 `Retracting`으로 폴백한다.
- **엣지 케이스 — 코스메틱 사본의 독립 판정**: 비소유 클라이언트의 훅(`LaunchCosmetic`)도 동일한 SphereCast로 `Flying → WaitingForServer/ImpactPause`를 스스로 판단하지만, 실제 결과는 뒤이어 도착하는 RPC가 항상 덮어쓴다 — 승인/거부는 `GrabApproved/RejectedNotOwnerRpc`, **빗나감은 `PlayRemoteMissRpc`**(각 클라이언트의 자원 위치가 스크롤 외삽으로 미세하게 달라 경계 사례에서 코스메틱이 "명중"으로 재현하면, 소유자 미스를 전파받지 못할 경우 `WaitingForServerTimeout` 1.5 s 동안 대상에 붙어 있는 화면 불일치가 생긴다 — 2026-07-21 플레이테스트에서 발견되어 미스 브로드캐스트 추가). 코스메틱 판정은 시각적 근사치일 뿐 권위가 아니다.

### 6.2 그랩 요청 시퀀스

```mermaid
sequenceDiagram
    participant Shooter as 소유자 클라이언트
    participant Server as 호스트
    participant Others as 다른 클라이언트

    Shooter->>Shooter: Fire() — 로컬 훅 스폰 (지연 0)
    Shooter->>Server: ReportFireServerRpc(origin, dir)
    Server->>Others: PlayRemoteFireRpc(origin, dir)
    Note over Others: 코스메틱 훅이 동일 궤적으로 비행

    alt 빗나감 (지형 명중·사거리 초과)
        Shooter->>Shooter: 미스 확정 → 되감기 연출
        Shooter->>Server: ReportMissServerRpc()
        Server->>Others: PlayRemoteMissRpc()
        Note over Others: 코스메틱 재시뮬레이션 결과와 무관하게 훅 BeginRetract() 수렴
    end

    Shooter->>Shooter: SphereCast 명중 → WaitingForServer (탄성 연출)
    Shooter->>Server: RequestGrabServerRpc(target, firePos, hitPoint)
    Server->>Server: GrabValidation.Validate(...)
    alt 승인
        Server->>Shooter: GrabApprovedOwnerRpc(target)
        Server->>Others: GrabApprovedNotOwnerRpc(target)
        Note over Shooter,Others: 훅.AttachTo(target) — 대상 위치를 계속 추적
    else 거부
        Server->>Shooter: GrabRejectedOwnerRpc(verdict)
        Server->>Others: GrabRejectedNotOwnerRpc()
        Note over Shooter,Others: 훅.BeginRetract() — 총구로 되감기
    end
```

## 7. 인터페이스·의존성 (경계)

- **`IGrabbable`** — Harpoon → 대상 도메인 방향 경계. `ResourceNode`(world) · `HandrailAnchor`(train) · `MonsterGrabTarget`(monsters, M5 5차)이 구현하며, Harpoon은 구체 타입을 모른 채 인터페이스로만 그랩·견인·해제·도착 확정을 호출한다. **M5 5차에서 `CompleteGrab()`이 `bool TryCompleteGrab(in GrabCompletion)`으로 바뀌었다** — 도착 시 무슨 일이 일어나는지를 **대상이 결정**한다(자원 = 수납 후 소멸, 몬스터 = 소멸 없이 무력화, 앵커 = 해당 없음). false면 집게는 기존 "해제 + `ForceRelease` RPC" 경로로 되돌아간다. 덕분에 대상 종류가 늘어도 `HarpoonController`에 분기가 늘지 않는다.
- **`GrabCompletion`** (Harpoon 도메인) — 도착 시 대상에게 넘기는 그래버 정보 (clientId · 그래버 `GameObject` · 집게 등급). 대상이 필요한 것만 읽는다 — 자원은 `GameObject`에서 `IResourceInventory`를 뽑아 스스로 수납한다.
- **`IHarpoonTierHolder`** (crafting 도메인, M5 5차) — 제작대 승급이 "지금 몇 단계인가"를 묻고 등급을 확정할 때 쓰는 최소 표면. **계약을 제작 쪽에 두고 집게가 구현**해 제작이 Harpoon 구현체를 직접 참조하지 않는다(DIP).
- ~~**`ISharedResourceCounter`**~~ — **M5 5차에서 Harpoon 경계에서 제거됐다.** 획득 확정이 `ResourceNode`로 이관되면서 카운터 증가도 World 도메인 안에서 일어난다. Harpoon은 이제 World 서비스를 전혀 알지 않는다.
- **`EventBus<HarpoonFiredLocalEvent>` / `<HarpoonMissLocalEvent>`** — UI·오디오 등 로컬 표현 구독자에게 발행하는 출력 경계. 게임 상태를 바꾸지 않는 순수 알림이므로 권위 이벤트가 아니다 (아키텍처 규칙 §3).
- **네트워크 경계** — 소유자 전용 RPC(`SendTo.Owner`)와 브로드캐스트 RPC(`SendTo.NotOwner`)를 항상 짝으로 유지한다. 새 권위 확정 지점을 추가할 때는 두 RPC를 함께 추가해야 시각 동기화가 깨지지 않는다.

## 8. 설계 포인트 (SOLID)

| 원칙 | 적용 |
|---|---|
| **SRP** | `HarpoonHookMotion`(순수 단계 전이)과 `HarpoonProjectile`(Transform·Physics 구동)을 분리 — 전이 규칙을 엔진 의존 없이 단위 테스트 가능 |
| **OCP** | `IGrabbable` 확장만으로 그랩 대상 종류를 늘릴 수 있음 — **M5 5차에서 `MonsterGrabTarget` 추가로 실증됐다**. 도착 확정을 `TryCompleteGrab` 폴리모피즘으로 이관해 `HarpoonController`에서 `ResourceNode` 캐스팅이 사라졌다 |
| **DIP** | Harpoon이 World 서비스를 아예 참조하지 않는다 (M5 5차 — `ISharedResourceCounter` 조회가 `ResourceNode`로 이관). 제작 승급 경로도 제작 쪽 계약(`IHarpoonTierHolder`)을 집게가 구현하는 방향이라 역방향 의존이 없다 |
| **강조 패턴 — 상태 머신 이원화** | 조작 게이트(`HarpoonStateMachine`, 언제 입력을 받을지)와 시각 단계(`HarpoonHookMotion`, 훅이 어디 있는지)를 별개 상태 기계로 분리해, "취소=절단 vs 실패=되감기"처럼 서로 다른 규칙을 가진 두 축을 독립적으로 바꿀 수 있게 했다 |

## 9. Unity 특화

- **생명주기**: `OnNetworkSpawn`에서 `HarpoonStateMachine` 생성, `OnNetworkDespawn`에서 서버 견인 해제(`ServerReleaseTow`) + 활성 훅 정리(`DiscardActiveProjectile`). `Update` 순서: 서버 견인 시뮬레이션 → (소유자면) 상태 틱·입력 → 로프 시각화(소유자·비소유 공통).
- **풀링**: `HarpoonProjectile`은 항상 `PoolManager.Spawn`/`Despawn` 경유. `OnDespawned`에서 `_motion.Cancel()` + 콜백·타깃 참조 정리로 재사용 시 상태 오염을 막는다. 네트워크 오브젝트가 아니므로 NGO 스폰 핸들러와 무관 — 클라이언트마다 로컬로 독립 풀링된다.
- **성능 예산**: `Flying` 단계에서만 프레임당 SphereCast 1회. `Attached`/`Retracting`은 `Vector3.MoveTowards` 등 값형 연산만 사용해 GC 할당 없음. 동시 그랩 상한은 플레이어 수(최대 4)로 자연 제한된다.
- **에디터 툴 필요 여부**: 없음 — 모든 수치는 `HarpoonSettings` 에셋 인스펙터로 조정.

## 10. 테스트 케이스

| 테스트 파일 | 검증 항목 |
|---|---|
| `HarpoonStateMachineTests` (9개) | Ready→Firing 전이, 비행 중 취소 무효, 미스 페널티 동안 재발사 불가, 취소는 쿨다운만 적용, 강제 해제 처리 |
| `GrabValidationTests` (5개) | 정상 승인, 대상 소멸/점유 거부, 사거리 상한 초과 거부, 여유 구간 내 승인 |
| `HarpoonHookMotionTests` (12개) | 전체 6단계 전이 경로, ImpactPause 자동 만료, WaitingForServer 안전 타임아웃, Idle에서의 무시 동작, Cancel의 전역성 |
| `TowMotionAnalyzerTests` (10개) | Q3 견인 계측 순수 로직 — 워프·역행 검출, 되밀림 1회 집계, 시작 유예(0.2 s) 중 이상 미집계 |
| `HarpoonAimMathTests` (4개) | 조준점 수렴 발사 방향 — 원거리 평행 수렴·총구 뒤/총구 겹침 조준점 폴백 |

수동 검증: 호스트 단독 Play 스모크(벽 명중→되감기 37프레임, Attach→Retract 위치 추적)와 실제 호스트+클라이언트 2인 연결(MPPM 가상 플레이어)로 `ReportFireServerRpc`→`PlayRemoteFireRpc` 도달을 진단 로그로 확인.

## 11. 리스크·미결정 (TBD)

| 항목 | 내용 |
|---|---|
| ~~손맛 정량 실측 미완료~~ → **정량 검증 종결 (Q1~Q5)** (2026-07-21) | **프로파일 ② (RTT 60 ms ± 10, 클라이언트 측)**: **Q1** 전 발사 Δ0 프레임 통과 · **Q2** 평균 67~72 ms (57~85) ≪ 250 ms 통과 · **Q3** 전 견인 워프/역행 0 통과 · **Q4** 101발사 · 거부 0 = 불일치율 0 % (표본 100회+ 충족) 통과. **Q5**: 호스트 측 100발사 계측에서 Δ0 · 워프/역행 0 · 거부 0 — 로컬 체감과 동일 (호스트 표본은 `LocalSendRpcTarget` 경유·Q2 0 ms라 프로파일 ② 표본과 분리 집계). **프로파일 ③ 참고 계측 완료** — 역행 소수 감지는 아래 별도 행. Boot 씬 시뮬레이터 중복 부착 제거(467a08f) 후 프로파일 ② 재계측 Q2 평균 66 ms (57~76, n=23)로 동일 대역 — 기존 수치 유효성 교차 확인. **정성 블라인드 테스트도 통과 (2026-07-22)** — 호스트/클라이언트를 모르는 상태의 교차 플레이에서 지연·이질감 없음 응답 (슬라이스 스펙 §3.3 기준 충족), 손맛 검증 전체 종결 |
| ~~프로파일 ③ 견인 역행~~ → **재계측 미재현·종결** (2026-07-22) | 최초 참고 계측(2026-07-21, Q2 평균 125 ms)에서 견인 5회 중 초반 2회에 역행 총 3프레임 감지 — 단 Boot 씬 `NetworkSimulator` 중복 부착(지연 비정상 적용, 467a08f로 제거) 상태의 계측이었다. **정상 환경 + 미해결 이슈 수정(조준점 수렴·겹침 검사·예측 고정) 반영 상태의 재계측(2026-07-22)에서 프로파일 ② Q2 평균 72 ms (64~85, n=33) · 워프/역행 0, 프로파일 ③에서도 역행 미재현** — 기존 역행은 중복 시뮬레이터 계측 아티팩트로 판단하고 종결한다. 연출 처방(견인 보간 버퍼 확대) 불필요. 이로써 정량 파트(Q1~Q5, 프로파일 ②·③)는 완전 종결 |
| ~~세션 종료 시 `t.GetParent() == nullptr` 어설션~~ → **원인 확정·워크어라운드 적용** (2026-07-21) | 에디터 재현·이분법으로 원인 확정: **Multiplayer Tools 2.2.9의 `RuntimeUpdater`가 만드는 숨김 `[RuntimeUpdaterBehaviour]` 오브젝트** (HideAndDontSave + DontDestroyOnLoad로 생성 후, 플레이 종료 시 GameObject가 아닌 컴포넌트만 파괴 → 잔류 오브젝트당 1회 어설션). **`PoolManager` 반환 경로는 무관 실증** — 풀 재부모화 32건이 그대로 있어도 해당 오브젝트만 제거하면 어설션 0회. 재현 조건은 "플레이 중 세션 Shutdown 후 플레이 종료" (Shutdown 없이 종료하면 미발생). 에디터 전용 정리 스크립트 `MultiplayerToolsHiddenUpdaterCleanup`(ExitingPlayMode에서 잔류 오브젝트 파괴)로 해소, 재현 시나리오에서 0회 확인. 패키지가 수정되면 스크립트 제거 |
| ~~명중 판정 시차·관통~~ → **해소** (2026-07-21) | ① 총구 시차: **조준점 수렴 발사**로 해소 (사용자 결정) — `Fire()`가 카메라 중심 레이(사수 자신 제외)로 조준점을 구해 총구→조준점 방향으로 발사 (`HarpoonAimMath.ResolveFireDirection`, 조준점이 총구 뒤이면 카메라 전방 폴백). 판정은 기존대로 훅 경로 SphereCast — 초근접에서만 미세 각도 차 잔존. ② 관통: `UpdateFlying`이 SphereCast 전 `OverlapSphere` 겹침 검사로 프레임 사이에 겹친 콜라이더를 잡는다 (사수 루트 제외 — 총구 인접 자기 콜라이더 오탐 방지, 비볼록 MeshCollider는 AABB 근사) |
| ~~게스트 그랩 확정 순간이동~~ → **수정안 A 구현 완료 · B는 롤백 · 실플레이 재검증 통과** (2026-07-21 구현, 2026-07-22 B 롤백·재검증 종결) | 원인 3중: ① **RTT 간극(주범)** — 서버는 `TryClaimGrab` 즉시 대상을 컨베이어에서 제외·고정하지만, 게스트는 `_isTowed` 스냅샷 도착까지 계속 스크롤 유도를 적용 → 도착 순간 `scrollSpeed × (편도 지연+틱 간격)`만큼 스냅. ② **훅 중심 스냅** — `UpdateAttached`가 대상 pivot을 그대로 대입. ③ 게스트 표시 거리 외삽 오차 상시 오프셋. **구현**: **A — 클라이언트 예측 고정**: `IGrabbable.BeginPredictedTow/CancelPredictedTow` 추가, 쏜 클라이언트가 로컬 명중 시점에 대상을 예측 고정(호스트는 제외), `_isTowed` 수신 시 자동 해제 후 `_towPosition` 보간 수렴, 거부·강제 해제·타임아웃(컨트롤러 Update 안전장치) 시 컨베이어 복귀 (`ResourceNode` 측 변경은 [world 스펙 §11](../world/scroll-and-streaming.md) 참조). **B — 부착점 오프셋 유지는 구현 후 롤백** (사용자 결정, 2026-07-22) — `AttachTo`는 기존대로 대상 중심(pivot) 부착. 주범 ①은 A로 제거됐고 ②(중심 스냅)·③은 잔존 — **정성 블라인드 테스트(2026-07-22, 호스트+게스트 교차 플레이)에서 지연·이질감 없음 응답으로 잔여 체감 재검증 통과, ② 재처방 불필요로 종결** |
| 코스메틱 사본과 실제 판정의 시각적 어긋남 | 비소유 독립 SphereCast가 실제 사수의 판정과 드물게 다른 지점에서 멈출 수 있음. 미스 브로드캐스트 추가로 실패 케이스의 수렴 시간이 1.5 s → 약 1 RTT로 단축됐으나, 수렴 전 잔여 스냅은 남음 — 권위는 항상 서버가 최종 확정하므로 게임플레이 영향은 없음 |
| ~~`WaitingForServerTimeout`(1.5 s) 근거 미검증~~ → **실측 확인** (2026-07-21) | 악조건 프로파일 ③(RTT 120 ms ± 20 + 로스 2 %) 실측에서 승인 수신 지연 최대 135 ms — 1.5 s 대비 10배 이상 여유 확인 |
| 비포커스 에디터에서의 프레임 압축 | MPPM 가상 클라이언트가 비포커스 상태일 때 `Time.deltaTime`이 크게 뭉개져 중간 프레임 관찰이 어려움 — 테스트 환경 한계, 실제 빌드 클라이언트로 재확인 권장. 계측 로그(스택 트레이스 포함)도 발사·승인 순간 프레임 스파이크를 유발할 수 있음 |

## 12. 확장 여지

- `IGrabbable`을 몬스터에 구현하면 그랩·처형이 동일 파이프라인으로 재사용된다 (개발 가이드 M5 지침).
- `HookPhase`에 `Charging`(홀드 조준) 등을 추가해도 `HarpoonStateMachine`과 독립이라 `HarpoonController`의 입력 게이트 로직을 거의 건드리지 않는다.
- 무게 등급별 릴 속도(2·3단계 집게)는 `HarpoonSettings`에 등급 테이블만 추가하면 되는 구조 — 현재는 하드코딩하지 않았을 뿐 막아두지도 않았다.

## 13. 파일 위치

| 구분 | 파일 | 경로 |
|---|---|---|
| 조작 게이트 | `HarpoonState.cs`, `HarpoonStateMachine.cs` | `Assets/_Project/Scripts/Gameplay/Harpoon/` |
| 훅 비행 | `HarpoonHookMotion.cs`, `HarpoonProjectile.cs` | 〃 |
| 조준 보정 | `HarpoonAimMath.cs` | 〃 |
| 호스트 검증 | `GrabValidation.cs`, `IGrabbable.cs` | 〃 |
| 컨트롤러 | `HarpoonController.cs` | 〃 |
| 연출 | `HarpoonRopeRenderer.cs`, `HarpoonEvents.cs` | 〃 |
| 데이터 | `HarpoonSettings.cs` (+ `HarpoonSettings.asset`) | 〃 (+ `Assets/_Project/Data/`) |
| 계측 | `HarpoonSliceMetrics.cs`, `TowMotionAnalyzer.cs` | 〃 |
| 프리팹 | `HarpoonProjectile.prefab` | `Assets/_Project/Prefabs/` |
| 테스트 | `HarpoonStateMachineTests.cs`, `GrabValidationTests.cs`, `HarpoonHookMotionTests.cs`, `TowMotionAnalyzerTests.cs`, `HarpoonAimMathTests.cs` | `Assets/_Project/Tests/EditMode/` |
| 에디터 워크어라운드 | `MultiplayerToolsHiddenUpdaterCleanup.cs` (세션 종료 어설션 — §11) | `Assets/_Project/Scripts/Editor/` |
