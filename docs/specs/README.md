# 아키텍처 명세 허브 (`specs/`)

`docs/specs/`는 **구현된 코드와 실제로 일치하는** as-built 문서만 둔다. 구현 전 계획·범위·TBD는
[`docs/design/`](../design/)에, 게임 내용(무엇을·왜)은 [`docs/design/Train-Survival-기획서.md`](../design/Train-Survival-기획서.md)에 있다.

도메인 폴더는 코드의 `Assets/_Project/Scripts/Gameplay/<도메인>` 구조를 그대로 미러링한다.

## 도메인 인덱스

| 도메인 | 문서 | 한 줄 요약 |
|---|---|---|
| `harpoon/` | [grapple-pipeline.md](harpoon/grapple-pipeline.md) | 집게 발사→판정→견인 파이프라인, 실패 되감기 연출, 원격 시각 브로드캐스트 |
| `world/` | [scroll-and-streaming.md](world/scroll-and-streaming.md) | 열차 고정 + 월드 스크롤 좌표계, 지형 타일 스트리밍, 지상 자원 스폰 |
| `player/` | [network-movement.md](player/network-movement.md) | 소유자 권위 1인칭 이동, 호스트 개입 상태 머신 골격, 낙하·이탈·부활 |

## 대표 명세 추천 순서 (심사자용)

1. **[world/scroll-and-streaming.md](world/scroll-and-streaming.md)** — 이 프로젝트 전체의 기준 좌표계를 먼저 이해해야 나머지가 읽힌다.
2. **[harpoon/grapple-pipeline.md](harpoon/grapple-pipeline.md)** — 로컬 선반영 + 호스트 권위 분리, 순수 로직(`HarpoonHookMotion`) 분리가 가장 잘 드러나는 문서.
3. **[player/network-movement.md](player/network-movement.md)** — 위 둘을 소비하는 입장에서 서비스 경계(`ServiceLocator`)가 어떻게 쓰이는지 확인.

세 문서 모두 §4 시스템 구조의 Mermaid `classDiagram`이 도메인 간 경계(어떤 인터페이스로만 서로를 참조하는지)를
명시하므로, 어셈블리 의존성 규칙([`../conventions/architecture-rules.md`](../conventions/architecture-rules.md))이
실제로 지켜졌는지 대조하는 용도로도 쓸 수 있다.
