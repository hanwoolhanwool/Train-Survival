# 연료 루프 (엔진 투입 → 충전 → 소모 → 감속)

> **종류**: 아키텍처 명세 · **상태**: 구현중
> **최종 갱신**: 2026-07-24 · **관련 기획서**: [Train-Survival-기획서 §3.4](../../design/Train-Survival-기획서.md) · [네트워크 아키텍처 §4](../../design/Train-Survival-네트워크-아키텍처.md) · [개발 가이드 §5 M2](../../Train-Survival-개발-가이드.md) · [world/scroll-and-streaming](scroll-and-streaming.md)

## 1. 개요·목적

운반 루프의 종착점이다. **채집 → 개인 인벤토리 → 엔진 투입 → 연료 충전**을 완성하고, 연료가 상시
소모되며 **고갈 시 월드 스크롤 속도가 감속**된다(= 열차가 느려짐). 연료·속도 모두 호스트 권위이며,
연료 잔량이 속도 제어면(`IWorldScrollSpeedControl`)을 통해 스크롤 속도로 이어지는 도메인 경계가 핵심이다.

## 2. 범위 (Scope)

**포함**: 공유 연료 탱크·상시 소모·감속 수렴(`FuelTank`, `IFuelService`), 연료 순수 계산(`FuelMath`),
엔진 투입구 상호작용(`EngineFuelPort`), 밸런스 데이터(`FuelSettings`), 연료·안내 이벤트(`FuelEvents`,
`TrainEvents`), 스크롤 속도 제어 계약(`IWorldScrollSpeedControl`, 수신 측 `WorldScrollController`).

**미포함**: 든 칸 차감 자체(→ [inventory](../inventory/hotbar.md)의 `ServerTryRemoveAt`), 월드 스크롤·타일
스트리밍 본체(→ [scroll-and-streaming](scroll-and-streaming.md)), 칸 증설에 따른 소모 증가 트레이드오프(M3).

## 3. 요구사항 → 설계 해석

| 요구사항 | 출처 | 설계적 해석 |
|---|---|---|
| 투입은 요청→호스트가 (차감+충전) 원자적 확정 | 네트워크 §4, 가이드 M2 | `RequestDepositServerRpc`에서 `ServerTryRemoveAt` 성공 시에만 `AddFuel` — 차감 실패면 충전 없음 |
| 연료 잔량 동기화 | 네트워크 §4 | `NetworkVariable<float> _fuel`, 서버만 쓰기 |
| E키 1회 = 1개 투입 | 기획서 §3.4 | E키/좌클릭 `wasPressedThisFrame` 1회 = RPC 1건 |
| 근처+시선일 때만 안내·투입 | 커밋 1f69dac | 범위(`_interactRadius`) ∧ 시선(`_lookDotThreshold`) 결합 |
| 든 칸만 소모 | 커밋 1f69dac | 선택 슬롯이 `Resource`일 때만, `SelectedIndex`를 서버에 전달해 그 칸 차감 |
| 연료 소모 → 감속 | 기획서 §3.4 | 연료 유무로 목표 속도를 정하고 `IWorldScrollSpeedControl`로 수렴 |

## 4. 시스템 구조

| 구성요소 | 역할 | 계층 |
|---|---|---|
| `FuelTank` | 호스트 권위 연료 소모 + 목표 스크롤 속도 수렴, `IFuelService` 구현 | `NetworkBehaviour` |
| `FuelMath` | 소모·충전·목표속도·수렴 순수 계산 | 순수 C# static |
| `EngineFuelPort` | 엔진 투입구 상호작용(근처+시선, E/좌클릭) | `NetworkBehaviour` |
| `FuelSettings` | 연료 밸런스 데이터 | `ScriptableObject` |
| `FuelEvents` / `TrainEvents` | 연료 변경 / 투입 안내 이벤트 | 순수 C# struct |
| `IFuelService` | 연료 상태 계약(`Fuel`/`Capacity`/`AddFuel`) | 인터페이스 |
| `IWorldScrollSpeedControl` | 스크롤 속도 제어면(조회와 분리) | 인터페이스 |
| `WorldScrollController` | 스크롤 속도 권위 소유자(속도 수신) | `NetworkBehaviour` |

```mermaid
classDiagram
    class FuelTank {
        -NetworkVariable~float~ _fuel
        -float _currentSpeed
        +AddFuel(amount)
    }
    class FuelMath {
        <<static>>
        +ConsumeFuel(fuel, perSec, dt) float
        +AddFuel(fuel, amount, capacity) float
        +ComputeTargetScrollSpeed(base, fuel, depletedRatio) float
        +StepScrollSpeed(cur, target, rate, dt) float
    }
    class EngineFuelPort {
        -RequestDepositServerRpc(slotIndex)
    }
    class IWorldScrollSpeedControl {
        <<interface>>
        +SetScrollSpeed(speed)
    }
    FuelTank ..|> IFuelService
    FuelTank --> FuelMath
    FuelTank ..> IWorldScrollSpeedControl : SetScrollSpeed (ServiceLocator)
    EngineFuelPort ..> IResourceInventory : ServerTryRemoveAt
    EngineFuelPort ..> IFuelService : AddFuel
    WorldScrollController ..|> IWorldScrollSpeedControl
```

## 5. 데이터 구조

### `FuelSettings`

| 필드 | 기본값 | 의미 |
|---|---|---|
| `Capacity` | 100 | 최대 저장량 |
| `InitialFuel` | 60 | 초기 연료 |
| `ConsumptionPerSecond` | 0.8 | 초당 소모율 |
| `FuelPerResource` | 6 | 자원 1개당 충전량 |
| `DepletedSpeedRatio` | 0.3 | 고갈 시 기본 속도 대비 유지 비율 (Range 0~1) |
| `SpeedChangeRate` | 1.5 | 가감속률 (m/s²) |

### `EngineFuelPort` 판정 상수

| 필드 | 기본값 | 의미 |
|---|---|---|
| `_interactRadius` | 3 | 상호작용 반경(서버 재검증은 +1.5 = 4.5) |
| `_lookDotThreshold` | 0.8 | 시선 내적 임계(카메라 없으면 true 폴백) |

## 6. 상세 로직·상태

### 6.1 투입 (원자적 차감+충전)

```mermaid
sequenceDiagram
    participant Owner as 소유자(선택 자원 칸)
    participant Port as EngineFuelPort
    participant Server as 호스트
    participant Inv as PlayerInventory
    participant Tank as FuelTank

    Owner->>Port: E키/좌클릭 (근처 ∧ 시선 ∧ 선택=Resource)
    Port->>Server: RequestDepositServerRpc(SelectedIndex)
    Server->>Server: 거리 재검증 (>4.5 기각)
    Server->>Inv: ServerTryRemoveAt(slotIndex, 1)
    alt 차감 성공
    Server->>Tank: AddFuel(FuelPerResource)
    else 차감 실패(든 칸이 자원 아님/빈 칸)
    Note over Server: 충전 없음 (early return)
    end
```

- **왜 원자적인가**: 차감과 충전이 같은 서버 RPC 본문에서 순서대로 일어나고, 차감 실패 시 조기 반환해
  "자원은 사라졌는데 연료가 안 찬" 중간 상태가 없다.
- **든 칸만 소모**: 선택 슬롯이 `Resource`가 아니거나 I 패널이 열려 있으면 클라이언트에서 아예 요청하지
  않고, 서버도 `ServerTryRemoveAt`가 그 칸이 자원일 때만 성공한다(이중 방어).
- **근처+시선**: `inRange ∧ IsLookingAtPort`일 때만 안내·투입 — 지나가기만 해도 안내가 뜨던 문제 해소.

### 6.2 소모 → 감속 (`FuelTank.Update`, 서버 전용)

```
_fuel = FuelMath.ConsumeFuel(_fuel, ConsumptionPerSecond, dt)
target = FuelMath.ComputeTargetScrollSpeed(BaseScrollSpeed, _fuel, DepletedSpeedRatio)
next   = FuelMath.StepScrollSpeed(_currentSpeed, target, SpeedChangeRate, dt)
변화 있으면 → ServiceLocator.TryGet<IWorldScrollSpeedControl>().SetScrollSpeed(next)
```

- **감속은 이진 목표 + 수렴**: 연료가 있으면 목표=기본 속도, 고갈이면 목표=기본×`DepletedSpeedRatio`.
  현재 속도를 `MoveTowards`로 `SpeedChangeRate`만큼 목표에 수렴시켜 급변하지 않는다. (연속 배율 곡선은
  아직 없음 — 있음/고갈 이진.)
- **도메인 경계**: `FuelTank`(연료 권위)가 `WorldScrollController`(속도 권위)를 직접 참조하지 않고
  `IWorldScrollSpeedControl` 서비스로만 속도를 지시. 실제 스크롤 속도는 `_scrollSpeed` NetworkVariable로
  전 피어에 복제된다.

### 6.3 이벤트

- `FuelChangedEvent(Fuel, Capacity)` — `_fuel.OnValueChanged` 콜백에서 각 피어 발행(HUD 게이지).
- `EnginePromptLocalEvent(InRange)` — 범위 상태 변화 시 발행(HUD "E — 연료 투입" 안내).

## 7. 인터페이스·의존성 (경계)

- **`IFuelService`** — `AddFuel`은 서버 전용(클라 호출 무시). 투입구가 이 계약으로만 연료를 채운다.
- **`IWorldScrollSpeedControl`** — 조회(`IWorldScrollService`)와 **분리된** 제어면(ISP). 속도를 낮출 권한은
  호스트 로직만 갖고, 조회자는 속도를 바꿀 수 없다.
- **`IResourceInventory`** — 든 칸 차감을 [inventory](../inventory/hotbar.md)의 `ServerTryRemoveAt`로 위임.
- **권위 이벤트 vs 로컬 안내**: 연료 잔량(`FuelChangedEvent`)은 복제 값 기반 권위 표현, 투입구
  안내(`EnginePromptLocalEvent`)는 각자 로컬 표현.

## 8. 설계 포인트 (SOLID)

| 원칙 | 적용 |
|---|---|
| **SRP** | 연료 수식(`FuelMath`)·연료 권위(`FuelTank`)·상호작용(`EngineFuelPort`)을 분리 |
| **ISP** | 스크롤 속도를 조회면과 제어면으로 분리 — 감속 권한 최소화 |
| **DIP** | 연료↔속도, 연료↔인벤토리를 전부 인터페이스로 연결 — 서로의 구현을 모른다 |
| **강조 패턴 — 원자적 서버 확정** | 차감+충전을 한 RPC에서 순서 처리해 부분 적용 상태를 원천 차단 |

## 9. Unity 특화

- **생명주기**: `FuelTank`는 Game 씬에 1개(호스트 소모 루프). `EngineFuelPort`는 기관차에 배치, 소유자
  로컬 플레이어와의 거리·시선으로 판정.
- **풀링**: 대상 없음(상시 오브젝트).
- **성능 예산**: `FuelTank.Update`는 서버에서 순수 산술 3회 + 속도 변화 시에만 서비스 호출. 투입구는
  거리 제곱·내적 비교뿐.
- **에디터 툴 필요 여부**: 없음. 밸런스는 `FuelSettings.asset`.

## 10. 테스트 케이스

| 테스트 파일 | 검증 항목 |
|---|---|
| `FuelMathTests` (6개) | 소모는 시간 비례·0 하한, 충전은 용량 상한·음수 무시, 연료 유무로 목표 속도(기본/최저), 가감속률 수렴, 한 스텝 변화가 가감속률 초과 안 함 |

`FuelTank`/RPC/`EngineFuelPort` 상호작용은 EditMode 대상 밖 — 순수 `FuelMath`만 검증.

## 11. 리스크·미결정 (TBD)

| 항목 | 내용 |
|---|---|
| 이진 감속 vs 연속 곡선 | 현재 있음/고갈 이진 목표 — 연료량 비례 연속 감속이 필요하면 `ComputeTargetScrollSpeed`만 곡선화 |
| 칸 증설 소모 증가 | 연료 소모 증가 트레이드오프는 M3(열차 시스템) — 현재 소모율은 고정 |
| 밸런스 초기값 | 소모 0.8/s·충전 6/개는 초기값 — 코어 루프 반복 밸런싱에서 조정 |

## 12. 확장 여지

- `IWorldScrollSpeedControl`은 날씨(모래폭풍 감속, M4)·부스트 아이템 등 다른 속도 개입원에도 재사용 가능.
- 칸별 소모 가중치(M3)는 `ConsumptionPerSecond`를 열차 상태 모델에서 산출하도록 확장하면 얹힌다.

## 13. 파일 위치

| 구분 | 파일 | 경로 |
|---|---|---|
| 연료 권위 | `FuelTank.cs` | `Assets/_Project/Scripts/Gameplay/World/` |
| 순수 로직 | `FuelMath.cs` | 〃 |
| 계약 | `IFuelService.cs`, `IWorldScrollSpeedControl.cs` | 〃 |
| 이벤트 | `FuelEvents.cs` | 〃 |
| 데이터 | `FuelSettings.cs` (+ `.asset`) | 〃 (+ `Assets/_Project/Data/`) |
| 투입구 | `EngineFuelPort.cs`, `TrainEvents.cs` | `Assets/_Project/Scripts/Gameplay/Train/` |
| 속도 수신 | `WorldScrollController.cs` | `Assets/_Project/Scripts/Gameplay/World/` |
| 테스트 | `FuelMathTests.cs` | `Assets/_Project/Tests/EditMode/` |
