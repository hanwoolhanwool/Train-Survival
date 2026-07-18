# CI 파이프라인 설정 가이드

`ci.yml`은 [GameCI](https://game.ci/) 기반이며, `main`/`develop` 브랜치의 push·PR에서
**테스트(EditMode + PlayMode) → Windows 빌드 → 아티팩트 업로드**를 수행한다.

## 동작 조건 — Unity 라이선스 Secrets 등록 (필수)

저장소 **Settings → Secrets and variables → Actions**에 아래 세 개를 등록해야 한다.

| Secret | 값 |
|--------|-----|
| `UNITY_LICENSE` | Unity 개인 라이선스 `.ulf` 파일의 전체 내용 |
| `UNITY_EMAIL` | Unity 계정 이메일 |
| `UNITY_PASSWORD` | Unity 계정 비밀번호 |

### `.ulf` 라이선스 파일 얻는 법

1. https://game.ci/docs/github/activation 의 안내를 따른다.
2. 요약: `game-ci/unity-request-activation-file` 액션으로 `.alf` 생성 →
   https://license.unity3d.com/manual 에서 수동 활성화 → 발급된 `.ulf` 내용을 `UNITY_LICENSE`에 붙여넣기.

## 로컬 CLI 빌드

```
Unity.exe -batchmode -quit -projectPath . -executeMethod Game.Editor.BuildScript.PerformWindowsBuild
```

빌드 결과는 `Builds/StandaloneWindows64/`에 생성된다.
