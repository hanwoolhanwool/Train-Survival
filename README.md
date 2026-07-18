# Train Survival

Unity 6 (6000.5.3f1) / URP 17.5 / Input System / PC(StandaloneWindows64)

## 시작하기

1. Unity Hub에서 **6000.5.3f1**로 프로젝트를 연다.
2. 클론 직후 아래를 1회 실행한다 (커밋 템플릿 · LFS · 씬/프리팹 머지 드라이버):

   ```bash
   git config commit.template .gitmessage
   git lfs install --local
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
   ```

## 문서

| 문서 | 내용 |
|------|------|
| [CLAUDE.md](CLAUDE.md) | Claude Code 작업 가이드 (코드 규칙 요약) |
| [Assets/SETUP.md](Assets/SETUP.md) | 초기 세팅 기록 (씬 흐름·테스트 실행·CI/CD·도구) |
| [docs/conventions/architecture-rules.md](docs/conventions/architecture-rules.md) | 아키텍처 규칙 (폴더 배치·어셈블리 의존성·인프라 사용) |
| [docs/conventions/solid-principles.md](docs/conventions/solid-principles.md) | SOLID 원칙 준수 규칙 |
| [docs/conventions/git-commit-convention.md](docs/conventions/git-commit-convention.md) | 커밋 컨벤션 |
