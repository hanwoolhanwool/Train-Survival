# CLAUDE.md

이 파일은 Claude Code가 이 저장소에서 작업할 때 참고하는 가이드다.

## 프로젝트 개요

- Unity 6 (6000.5.3f1) / URP 17.5 / Input System / PC(StandaloneWindows64)
- 프로젝트명 **Train Survival** (GitHub: hanwoolhanwool/Train-Survival) / 장르 **미정** — 어셈블리/네임스페이스는 접두어 `Game.*` 사용
- 초기 세팅 상세: `Assets/SETUP.md` (씬 흐름, 테스트 실행, CI/CD, 도구)

## 코드 작성 규칙

**코드를 작성하거나 수정할 때는 반드시 아래 컨벤션 문서를 참조한다.**

| 문서 | 내용 |
|------|------|
| [docs/conventions/solid-principles.md](docs/conventions/solid-principles.md) | SOLID 원칙 준수 규칙 (필수) |
| [docs/conventions/architecture-rules.md](docs/conventions/architecture-rules.md) | 폴더·스크립트 배치, 어셈블리 의존성, 인프라(풀링/이벤트/서비스) 사용 원칙, 테스트 배치 |
| [.editorconfig](.editorconfig) | C# 스타일 (private 필드 `_camelCase`, block-scoped namespace 등) |

## 핵심 제약 (요약)

- Unity 6은 **C# 9.0**까지만 지원 — file-scoped namespace 등 C# 10+ 문법 금지
- 스폰/소멸은 `PoolManager.Spawn/Despawn` 경유 (`Instantiate`/`Destroy` 직접 호출 지양)
- 시스템 간 통신은 `EventBus<T>`, 전역 서비스는 `ServiceLocator`
- 새 스크립트는 역할에 맞는 어셈블리 폴더(`Assets/_Project/Scripts/<계층>`)에 배치, 의존성은 단방향 (`Utilities ← Core ← Systems ← Gameplay ← UI`)


# 커밋 규약
- **Git 커밋** — [`docs/conventions/git-commit-convention.md`](docs/conventions/git-commit-convention.md): 형식·type·scope·Unity 전용 규칙.