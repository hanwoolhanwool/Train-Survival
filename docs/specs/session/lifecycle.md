# 세션 수명 주기 — 사망·부활·게임오버·상태 스냅샷

> **종류**: 아키텍처 명세 · **상태**: 구현 완료 (M6 3차)
> **최종 갱신**: 2026-08-20 · **관련 문서**: [기획서 §9.1](../../design/Train-Survival-기획서.md) ·
> [네트워크 아키텍처 §2.3](../../design/Train-Survival-네트워크-아키텍처.md) ·
> [개발 가이드 §5 M6](../../guide/Train-Survival-개발-가이드.md)

## 1. 개요·목적

런은 **단일 세션으로 완주**한다 — 게임 중간 저장이 없다(기획서 §9.1 확정). 따라서 이 도메인이
정하는 것은 "언제 런이 끝나는가"와 "끊긴 사람을 어떻게 다루는가"다.

핵심 규칙 하나가 전부를 지배한다 —

> **접속 끊김 ≠ 사망.** 끊긴 플레이어는 게임오버 판정에서 제외된다 (네트워크 §2.3).

이 규칙이 없으면 한 명이 인터넷이 끊기는 순간 남은 사람들의 런이 함께 끝난다.

## 2. 범위 (Scope)

**포함**: 전멸(wipe) 판정, 부활 대기 시간 계산, 게임오버 확정·전파, 세션 종료·Main 복귀,
재접속 복원을 위한 플레이어 상태 스냅샷.

**미포함**: 트랜스포트·로비(→ [networking/transport-and-lobby.md](../networking/transport-and-lobby.md)) ·
메타 진행 저장(→ [meta/progress-and-achievements.md](../meta/progress-and-achievements.md)) ·
체력·사망 처리 자체(→ [player/network-movement.md](../player/network-movement.md)).

## 3. 요구사항 → 설계 해석

| 요구사항 | 출처 | 설계적 해석 |
|---|---|---|
| 끊긴 플레이어는 게임오버 판정 제외 | 네트워크 §2.3 | 판정 입력이 "전체 플레이어"가 아니라 **접속 중 플레이어 목록** |
| 부활 대기 = 사망으로 친다 | M6 3차 결정 | 대기 중이어도 살아 있지 않으므로 전멸 판정에 포함 |
| 부활 대기가 Day에 비례 | 기획서 §9.1 (M2 미결) | `5 + Day × 1 s`, 상한 20 s — **이탈 사망도 같은 계산**으로 일원화 |
| 중간 저장 없음 | 기획서 §9.1 확정 | 세이브 포맷을 만들지 않는다. 복원은 **세션 내 재접속**만 |
| 재접속 시 같은 플레이어로 복귀 | 가이드 §5 M6 | 신원(Steam ID/GUID) ↔ 상태 스냅샷 매핑 |

## 4. 시스템 구조

| 구성요소 | 역할 | 계층 |
|---|---|---|
| `GameOverLogic` | 전멸 판정 (순수) | 순수 C# static |
| `GameOverMonitor` | 호스트 권위 감시 · `GameOverEvent` 발행 | `NetworkBehaviour` |
| `PlayerSessionAgent` | 플레이어별 세션 상태 | `NetworkBehaviour` |
| `PlayerSessionRegistry` | 신원 ↔ 세션 매핑 보관 | 순수 C# |
| `PlayerStateSnapshot` | 복원 단위 struct | 데이터 |
| `PlayerStateSnapshotOps` | 캡처·적용 (순수) | 순수 C# static |
| `GameOverHud` | 결과 오버레이 | `MonoBehaviour` (UI) |
| `SessionExitHud` | Esc 메뉴 · 세션 나가기 | `MonoBehaviour` (UI) |

## 5. 데이터 구조

### `GameOverLogic.PlayerLifeState`

전멸 판정의 **입력 단위**다. 판정에 필요한 최소 정보만 담아 순수 함수로 넘긴다 —
`IsWipe(IReadOnlyList<PlayerLifeState>)`.

### `PlayerStateSnapshot`

재접속 복원 단위. 인벤토리·장비·집게 등급·스탯·위치를 담는다.
`PlayerStateSnapshotOps.Capture(...)` / `.Apply(...)`가 **순수 함수**라 복원 규칙을 테스트할 수 있다.

## 6. 상세 로직·상태

### 6.1 전멸 판정

```
접속 중인 플레이어를 모아
  → 전원이 (사망 OR 부활 대기) 이면 전멸
  → 접속이 끊긴 플레이어는 목록에 넣지 않는다
```

**판정 시점**: 사망·부활·접속 변화가 있을 때. `GameOverMonitor`가 호스트에서만 돌고, 확정되면
`GameOverEvent`를 권위 이벤트로 발행한다.

### 6.2 부활 대기

```
대기 시간 = min(5 + Day × 1, 20)   [초]
```

M2에서 고정 5초로 두고 미결로 남겼던 n값을 M6 3차에서 확정했다. **이탈 사망(열차 뒤처짐)도
같은 계산을 쓴다** — 이전에는 별도 고정값이었다.

### 6.3 게임오버 → 세션 종료

```
GameOverEvent 수신
  → 전 피어가 결과 오버레이 표시 (도달 Day · 경과 시간)
  → 각 피어가 자기 로컬에 메타 진행 기록
  → 사용자 확인 → SessionExitHud.LeaveToMain 경로로 세션 종료 + Main 복귀
```

**세션은 결과를 보는 동안 유지된다** — 오버레이가 뜨자마자 연결을 끊으면 다른 사람 화면이 먼저 닫힌다.

### 6.4 재접속 복원

`PlayerSessionRegistry`가 신원(Steam ID64 또는 로컬 GUID) ↔ `PlayerStateSnapshot`을 들고 있다가,
같은 신원이 다시 붙으면 스냅샷을 적용한다.

| 복원 대상 | 규칙 |
|---|---|
| 인벤토리·장비·집게 등급·스탯 | 그대로 복원 |
| 위치 | 살아 있는 갑판/지상이면 그 자리, **이탈 칸·사망선 뒤면 스폰 폴백** |
| 부활 대기 | 잔여 시간을 **이어간다** (끊었다 붙어서 초기화하는 악용 차단) |
| 유령 연결 | 동일 신원 중복 접속 시 **기존 연결 킥** |

## 7. 인터페이스·의존성 (경계)

- `GameOverMonitor`는 플레이어 체력 구현을 직접 보지 않고 **생명 상태 목록**만 모은다.
- 메타 진행 기록은 `IMetaProgressService` 경유 — 세션이 저장 포맷을 모른다.
- UI는 `GameOverEvent` 구독만 한다.

## 8. 설계 포인트 (SOLID)

| 원칙 | 적용 |
|---|---|
| **S** | 판정 = `GameOverLogic` / 감시·발행 = `GameOverMonitor` / 복원 = `PlayerStateSnapshotOps` |
| **D** | 저장은 `IMetaProgressService` 추상 뒤 — 로컬 JSON인지 Steam인지 세션은 모른다 |

## 9. Unity 특화

- `NetworkManager` 콜백(연결·끊김)이 판정 트리거이므로, **에디터에서 Play 정지로 끊는 경우**와
  실제 네트워크 끊김이 같은 경로를 타는지 확인이 필요하다.

## 10. 테스트 케이스 (EditMode / PlayMode)

- `GameOverLogic.IsWipe` — 전원 사망 / 일부 끊김 + 나머지 사망 / 전원 끊김 / 부활 대기 포함
- `PlayerStateSnapshotOps` — 캡처·적용 왕복, 슬롯 경계
- PlayMode 11개가 세션 흐름을 덮는다

## 11. 리스크·미결정 (TBD)

| # | 항목 | 상태 |
|---|---|---|
| 1 | **승리/완주 판정 없음** | 최종장 진입·클리어 연출은 본편 설계와 함께. M6 완료 기준의 "완주"는 검증 목표이지 시스템 판정이 아니다 |
| 2 | 완료 기준 G8(외부 회선 릴레이 완주 + 재접속) 미검 | Steam 2계정·외부 회선 준비 미비 |
| 3 | 검증 잔여 — C(유령 킥) · D2 · E5 · D3-R · H8 | M6 잔여 작업 정리 참조 |
| 4 | 호스트 마이그레이션 없음 | 호스트가 나가면 세션 종료 (설계 확정) |

## 12. 확장 여지

- 승리 판정은 `GameOverMonitor`와 대칭 구조(`WinMonitor`)로 붙일 자리가 있다.
- 관전 모드 — 사망 후 대기 중 카메라를 다른 플레이어에 붙이는 축.

## 13. 파일 위치

```
Assets/_Project/Scripts/Gameplay/Session/
├─ GameOverLogic.cs             순수 — 전멸 판정
├─ GameOverMonitor.cs           NetworkBehaviour — 호스트 감시·발행
├─ PlayerSessionAgent.cs        NetworkBehaviour — 플레이어별 세션 상태
├─ PlayerSessionRegistry.cs     신원 ↔ 세션 매핑
├─ PlayerStateSnapshot.cs       복원 단위 struct
├─ PlayerStateSnapshotOps.cs    순수 — 캡처·적용
└─ SessionEvents.cs             GameOverEvent 등
```
