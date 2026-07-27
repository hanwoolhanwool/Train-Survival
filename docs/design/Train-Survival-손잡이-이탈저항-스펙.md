# 손잡이 & 이탈 저항 설계 (M3 확장)

> 상태: 설계 확정 · 구현 완료
> 기반: 기획서 §9(연결부 = 밤 방어전 핵심 방어 목표), 개발 가이드 §M3, 슬라이스 스펙 §2(집게), 네트워크 §4(권위 분담)
> 선행: M3 A(상태 모델)·B(파괴·연쇄 이탈) 완료분 위에 얹는다.
> 개정(2026-07-28): 손잡이 앵커를 **동적 스폰에서 씬 정적 배치로 전환** — §3·§5·§7·§11·§12 갱신.

## 1. 목적

밤 방어전에서 연결부가 뚫려 후방 칸이 이탈할 때, **플레이어가 협력해 이탈 칸을 되살릴 수 있는** 상호작용을 만든다.
이탈 칸의 손잡이를 집게로 잡아 뒤로 밀려나는 것을 저항하며, **잡은 인원 수에 따라 감속→정지→끌어당김**으로 결과가 갈린다.
이로써 인원 수가 곧 난이도이자 협력 여지가 되게 한다(1인 = 지연만, 다수 = 회수 가능 + 역할 분담).

## 2. 범위

### 이번 증분 (구현)
1. **이탈 칸 비공격 대상화** — 연결부가 먼저 끊겨 이탈한 칸·그 연결부는 몬스터 공격 대상에서 즉시 제외.
2. **손잡이 잡기 + 이탈 저항** — 이탈 중인 칸의 손잡이를 집게로 잡으면 저항력이 걸리고, 인원 비례로 이탈 이동이 감속/정지/역전(끌어당김)된다.
3. **이탈 이동의 호스트 권위 승격** — B의 CarView 로컬 연출을 **호스트 시뮬레이션 + 복제**로 올린다(칸의 생존이 게임플레이 결과이므로).

### 후속 증분 (문서에만, 확장성 확보)
- **재결합(연결부 재생성)** — 슬롯까지 끌어당긴 칸을 수리/제작으로 재연결(수리 망치 = C단계). 이번엔 슬롯에서 '붙잡힌 채 정지'까지만.
- **선제적 잡기** — 연결부가 끊기기 전에 손잡이를 미리 잡아 두는 전략(이번엔 이탈 중인 칸만 잡기 가능).
- **다중 그랩·엄호 역할** — 엄호 사격은 기존 리볼버로 이미 가능. 손잡이당 다중 그랩·추가 손잡이는 데이터로 확장.

## 3. 개념 모델

편성 배치는 칸 끝마다 `손잡이 | 연결부 | 손잡이` 구조다. 손잡이는 **각 칸의 앞·뒤 끝에 하나씩**, 항상 시각적으로 존재한다.

- **표현(항상)**: 손잡이 앵커는 씬에 정적 배치되어 게임 시작부터 상시 존재한다(런타임 생성 없음).
  칸의 스케일 왜곡을 피해 칸의 자식이 아니라 씬 루트 홀더(`Train_Handrails`)에 두고, 칸의 이탈
  오프셋을 따라 위치를 계산해 칸과 동행한다(기관차는 파괴·이탈 불가라 앵커 없음).
- **잡기 가능(이탈 중만)**: 칸이 이탈해 뒤로 밀려나는 동안에만 그 칸의 손잡이가 집게 표적이 된다. 붙어 있는 칸·소실된 칸의 손잡이는 표적 아님.

## 4. 저항 모델 (호스트 권위)

이탈한 칸 `c`의 이동을 호스트가 매 프레임 시뮬레이션한다. 상태량은 **이탈 오프셋** `offset_c`(슬롯 기준 후방 이동 거리, m).

```
pushSpeed   = scrollSpeed + ejectExtraSpeed        // 뒤로 밀려나는 기본 속도(데이터)
resistance  = grabberCount_c * pullPerGrabber       // 손잡이 잡은 인원 × 1인 견인력(데이터)
netVelocity = pushSpeed - resistance                // +면 후퇴, -면 슬롯으로 전진
offset_c    = clamp(offset_c + netVelocity * dt, 0, ∞)
```

- `netVelocity > 0`: 칸이 뒤로 멀어진다(저항이 밀림보다 약함 → 감속만).
- `netVelocity < 0`: 칸이 슬롯 쪽으로 당겨진다(다수 협력 → 끌어당김). `offset`은 0에서 멈춘다(슬롯 도달 = 붙잡힌 채 정지, 재결합은 후속).
- `offset_c > lostDistance` 이고 `grabberCount_c == 0`: 칸 **영구 소실**(회수 불가, 기획서 §9.1). 손잡이 앵커 despawn.

인원 비례 난이도는 전부 데이터에서 나온다:
| 상황(4인 예시) | grabberCount | netVelocity | 결과 |
|---|---|---|---|
| 아무도 안 잡음 | 0 | +pushSpeed | 후퇴 → 소실 |
| 1인 잡음 | 1 | pushSpeed - 1×pull | 감속(약간 벌어짐) |
| 2인 잡음 | 2 | pushSpeed - 2×pull < 0 | 끌어당김(슬롯 복귀) + 남은 2인은 엄호/재결합 |

→ `pullPerGrabber`를 `pushSpeed`의 약 0.6~0.8배로 잡으면 "1인=지연, 2인=회수"가 성립. 값은 SO로 분리해 밸런싱한다.

## 5. 손잡이 앵커 (집게 표적)

NGO 중첩 NetworkObject 제약을 피하고 기존 집게 파이프라인을 재사용하기 위해, **손잡이 앵커는 독립 NetworkObject**로 다룬다. 개정(2026-07-28): 동적 스폰(`IPoolable` + `PoolManager`)을 폐기하고 **씬 정적 배치(InScenePlaced)**로 전환 — 향후 열차 재생성/재결합 시 앵커 수명 관리가 단순해진다.

- **배치(정적)**: 씬 루트 홀더 `Train_Handrails` 아래에 파괴 가능 칸마다 앞·뒤 앵커를 저작한다
  (기관차 제외, `carIndex` 직렬화 배선). Train(NetworkObject) 하위 중첩을 의도적으로 피한 배치다.
  칸 소속은 계층이 아니라 `carIndex`로 연결되므로 기능은 동일하다.
- **위치(각 피어 로컬 계산)**: 복제된 이탈 오프셋(`ITrainState.GetEjectOffset`)을 읽어
  `기준 슬롯 위치 + back × offset`으로 매 프레임 배치 — CarView와 동일 소스라 전 피어 위치가
  일치하며, 위치 복제(NetworkVariable)가 필요 없다(`SynchronizeTransform` off).
- **잡기(IGrabbable)**: `IsAvailableForGrab` = `ITrainState.IsCarGrabbable(carIndex)`(이탈 중이고
  소실 거리 전). 집게 명중→호스트 승인→`TryClaimGrab`으로 그 칸의 `grabberCount` 증가.
- **집게 앵커 모드**: 집게는 이 표적을 **플레이어 쪽으로 릴 감지 않는다**(무거운 칸). `IGrabbable`에 `GrabKind { Reel, Anchor }`를 추가하고, `HarpoonController`의 견인 루프는 `Anchor`면 릴을 건너뛰고 로프만 유지한다. 우클릭 시 `ReleaseGrab` → `grabberCount` 감소.
- **소실 처리**: despawn하지 않는다 — 소실 칸(offset ≥ `LostDistance`, 0인)은 `IsCarGrabbable`이
  false가 되어 잡기만 차단되고, 앵커는 씬에 남아 다음 판(씬 재로드)·재결합에 대비한다.

집게의 발사·명중·승인 핸드셰이크(NetworkObjectReference + GrabValidation)는 그대로 재사용한다. 달라지는 것은 승인 이후 "릴 대신 홀드"뿐이다.

## 6. 이탈 이동의 권위 승격 (B 로컬 연출 → 호스트 상태)

B에서 CarView가 로컬로 뒤로 흘려보내던 연출을 폐기하고, `offset_c`를 **호스트가 계산 + 복제**한다.

- 저장: `TrainState`에 `NetworkList<float> _ejectOffsets`(칸 수 길이, 기본 0). 호스트만 갱신. 이동 중인 칸만 매 프레임 델타 전송(소수라 부담 작음).
- 표현: CarView는 자신의 오프셋을 읽어 `localPos = 원위치 + back * offset`으로 배치. 파괴(체력0)는 즉시 소멸(오프셋 무관).
- 속도(velocity)는 호스트 로컬 배열로만 유지(복제 불필요, 오프셋만 복제).

## 7. 권위 분담 (네트워크 §4)

| 요소 | 권위 | 비고 |
|---|---|---|
| 이탈 칸 이동(offset) | 호스트 | `NetworkList<float>` 복제, 클라 표현만 |
| 손잡이 앵커 존재 | 씬 정적 배치 | InScenePlaced NetworkObject — 스폰/despawn 없음 |
| 손잡이 앵커 위치 | 각 피어 로컬 계산 | 복제 오프셋 기반(CarView와 동일 소스), 위치 복제 없음 |
| 그랩 점유/해제 → grabberCount | 호스트 | 집게 승인 파이프라인 재사용 |
| 저항·소실·잡기 가능 판정 | 호스트 | 순수 로직으로 분리해 EditMode 검증, `IsCarGrabbable`은 복제 데이터 기반 |

## 8. 데이터 스키마 (SO)

`TrainDurabilitySettings`(또는 신설 `HandrailSettings`)에 추가:
- `EjectExtraSpeed`(m/s) — 스크롤 위에 더해지는 기본 후퇴 속도.
- `PullPerGrabber`(m/s) — 손잡이 1인당 상쇄 속도.
- `LostDistance`(m) — 이 거리 넘고 아무도 안 잡으면 영구 소실.

## 9. 순수 로직 (EditMode 대상)

`TrainStateLogic`(또는 신설 `EjectMotionMath`):
- `ComputeNetEjectVelocity(scrollSpeed, extra, grabberCount, pullPerGrabber)` → float.
- `StepEjectOffset(offset, netVelocity, dt)` → clamp≥0.
- `IsCarLost(offset, lostDistance, grabberCount)` → bool.
경계: 0인=후퇴, 임계 인원에서 부호 전환, 슬롯(0) 고정, 소실 조건.

## 10. 구현 단계

1. **Feature 1** — 이탈 칸/연결부를 타겟 레지스트리에서 즉시 제외(이탈 시 unregister) + IsAlive 방어 + 테스트.
2. **이동 권위 승격** — `_ejectOffsets` 도입, 호스트 시뮬레이션, CarView가 오프셋으로 배치(B 로컬 연출 제거).
3. **손잡이 앵커** — `HandrailAnchor`(IGrabbable Anchor) 스폰/이동/소멸 + grabberCount, 집게 Anchor 모드.
4. **저항 결합** — grabberCount를 이동 시뮬레이션에 연결, 데이터 밸런싱값.
5. **손잡이 표현** — 칸 앞뒤 끝 손잡이 메시(씬).
6. 검증 — EditMode(순수 로직) + 호스트1·클라N 런타임 정성 확인.

## 11. 미결 / 후속

- 재결합(연결부 재생성) 수단·비용 — C단계 수리 시스템에서 확정.
- 손잡이당 다중 그랩 허용 여부(현재 앞·뒤 2개 = 칸당 2인). 4인+ 스케일 시 재검토.
- 선제적 잡기(붙어 있는 칸) 도입 시 네트워크 종속 처리.
- **장식용 손잡이 바 메시** — 정적 배치 전환(2026-07-28)으로 그랩 앵커(구체)는 이제 붙어 있는 칸에도
  상시 존재·표시된다. 다만 시각은 여전히 표적용 구체이므로, 실제 손잡이 바 형태의 메시 교체는
  시각 폴리시로 후속.

## 12. 구현 현황 (이번 증분)

- 완료: Feature 1(이탈 칸·연결부 공격 제외), 이탈 이동 호스트 권위 승격(`_ejectOffsets`),
  순수 로직(`EjectMotionMath` + EditMode), 손잡이 앵커(`HandrailAnchor`, IGrabbable `Anchor`),
  집게 앵커 모드(릴 스킵·홀드), 저항 결합(`ITrainGrabResistance`).
- 완료(2026-07-28 개정): 앵커 **씬 정적 배치 전환** — 동적 스폰/풀링/위치 복제 제거,
  `Train_Handrails` 홀더에 4개(Car_1·Car_2 앞뒤) 저작, `ITrainState.IsCarGrabbable` 게이트.
  호스트 세션에서 스폰→이탈 시 잡기 가능→오프셋 동행→소실 시 잡기 차단까지 런타임 확인.
- 검증 남음: 클라N 참여 상태의 정성 확인 — 손잡이 그랩→인원별 감속/정지/끌어당김(집게 실조작 포함).
