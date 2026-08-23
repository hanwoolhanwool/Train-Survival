# 제3자 저작물 표기 (Third-Party Notices)

이 문서는 **Train Survival**이 사용하는 외부 소프트웨어·에셋과 그 라이선스를 정리한다.
저장소 자체의 라이선스는 [`LICENSE`](LICENSE)(코드·문서, MIT)와
[`LICENSE-ASSETS`](LICENSE-ASSETS)(게임 에셋, All Rights Reserved)로 나뉘며,
**이 문서에 열거한 항목은 그 둘 어디에도 속하지 않고 각 원저작자의 라이선스를 따른다.**

최종 갱신: 2026-08-23 · 기준 커밋: `0d322d1`

---

## 1. 게임 엔진

| 항목 | 버전 | 라이선스 |
|---|---|---|
| Unity | 6000.5.3f1 | [Unity 소프트웨어 사용권 계약](https://unity.com/legal/terms-of-service) — 엔진 바이너리는 이 저장소에 포함되지 않는다 |
| Unity 내장 모듈 (`com.unity.modules.*`) | 1.0.0 | 위와 동일 |

---

## 2. Unity 공식 패키지

`Packages/manifest.json`의 직접 의존이다. 패키지 소스는 저장소에 포함되지 않고
Unity Package Manager가 내려받는다.

| 패키지 | 버전 | 라이선스 |
|---|---|---|
| `com.unity.render-pipelines.universal` (URP) | 17.5.0 | Unity Companion License |
| `com.unity.netcode.gameobjects` | 2.13.0 | Unity Companion License |
| `com.unity.inputsystem` | 1.19.0 | Unity Companion License |
| `com.unity.ugui` | 2.5.0 | Unity Companion License |
| `com.unity.timeline` | 1.8.12 | Unity Companion License |
| `com.unity.test-framework` | 1.7.0 | Unity Companion License |
| `com.unity.ai.navigation` | 2.0.13 | Unity Companion License |
| `com.unity.multiplayer.tools` | 2.2.9 | Unity Package Distribution License |
| `com.unity.multiplayer.playmode` | 2.0.2 | Unity Package Distribution License |
| `com.unity.multiplayer.center` | 1.0.1 | Unity Package Distribution License |
| `com.unity.visualscripting` | 1.9.11 | Unity Package Distribution License |
| `com.unity.collab-proxy` | 2.12.4 | Unity Package Distribution License |
| `com.unity.ide.rider` | 3.0.38 | MIT License |
| `com.unity.ide.visualstudio` | 2.0.26 | MIT License |

전이 의존(`com.unity.burst` · `collections` · `mathematics` · `transport` ·
`shadergraph` · `render-pipelines.core` · `nuget.newtonsoft-json` 등)도
Unity Companion License 또는 Unity Package Distribution License를 따른다.
정확한 원문은 각 패키지의 `LICENSE.md`에 있다.

- Unity Companion License — <https://unity.com/legal/licenses/unity-companion-license>
- Unity Package Distribution License — <https://unity.com/legal/licenses/unity-package-distribution-license>

### 2.1 TextMesh Pro 필수 리소스 (저장소에 포함)

`Assets/TextMesh Pro/**` 는 Unity가 배포하는 TMP Essential Resources를 프로젝트에
임포트한 결과물이다. **저장소에 커밋돼 있다.**

| 항목 | 저작권 | 라이선스 |
|---|---|---|
| TMP 셰이더·설정·리소스 | © Unity Technologies | Unity Companion License |
| `Fonts/LiberationSans.ttf` | © 2010 Google Corporation · © 2012 Red Hat, Inc. (RFN: Liberation) | SIL OFL 1.1 — 전문 동봉 (`Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`) |

---

## 3. 제3자 오픈소스 패키지

| 패키지 | 버전 | 저작권 | 라이선스 | 용도 |
|---|---|---|---|---|
| [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) | 2025.164.1 | Copyright (c) 2013-2022 Riley Labrecque | **MIT License** | Steam 로비·릴레이·업적 (`Scripts/Systems/Networking/Steam/**`) |
| [MCP for Unity](https://github.com/CoplayDev/unity-mcp) | 10.1.0 | Copyright (c) 2025 CoplayDev | **MIT License** | **에디터 전용 개발 도구** — 게임 빌드에 포함되지 않는다 |

### 3.1 Valve Steamworks SDK

Steamworks.NET은 Valve의 **Steamworks SDK**를 감싼 C# 바인딩이다. SDK 재배포 바이너리
(`steam_api64.dll` 등)는 패키지에 동봉되며, 이 저장소가 직접 커밋하지는 않는다.
해당 바이너리는 MIT가 아니라 Valve의 **Steamworks SDK Access Agreement**를 따른다.

- <https://partner.steamgames.com/documentation/sdk_access_agreement>

`steam_appid.txt`의 값 `480`은 Valve가 공개한 **Spacewar 테스트 AppID**다.
자체 AppID 발급 전까지 쓰는 개발용 값이며 출시 빌드에는 들어가지 않는다.

---

## 4. 폰트

| 파일 | 서체 | 저작권 | 라이선스 |
|---|---|---|---|
| `Assets/_Project/Art/Fonts/NotoSansKR-VF.ttf` | Noto Sans KR (Variable, v2.004) | © 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'. | **SIL OFL 1.1** — 전문 동봉 (`Assets/_Project/Art/Fonts/OFL.txt`) |
| `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` | Liberation Sans | © 2010 Google Corporation · © 2012 Red Hat, Inc. | **SIL OFL 1.1** — §2.1 참조 |

OFL 1.1은 게임에 **임베딩·재배포**를 허용한다. 다만 폰트 파일을 **단독으로 판매**할 수
없고, 재배포 시 **라이선스 사본을 동봉**해야 하며, 파생본에 **예약 서체명(RFN)** 을
쓸 수 없다. 파생 폰트 에셋(`F_NotoSansKR_SDF.asset`)의 이름이 `NotoSansKR`을 쓰지만
이는 SDF 아틀라스일 뿐 폰트 소프트웨어의 파생 배포가 아니다.

선정 경위는 [`Assets/_Project/Art/Fonts/NOTICE.md`](Assets/_Project/Art/Fonts/NOTICE.md)에 있다.

---

## 5. 3D 모델 · 텍스처 — AI 생성 파이프라인

`Assets/_Project/Art/Models/**`(FBX 39개)와 `Assets/_Project/Art/Textures/**`(PNG 102장)의
**전부 AI 생성 도구를 거쳐 만들어졌다.** 원본 메시·텍스처를 사람이 모델링한 것이 아니다.
권리 관계는 §5.4에 정리했다 — **상업 이용이 허용되는 유료 등급에서 생성했다.**

### 5.1 파이프라인

```
ChatGPT (이미지 생성)  →  Tripo AI / Meshy (image-to-3D)  →  Blender (정규화·감축·머티리얼 통합)  →  Unity 반입
```

근거 문서 — [레벨디자인 가이드 결정 ⑦](docs/design/Train-Survival-레벨디자인-가이드.md) ·
[이미지생성 브리프 §G9](docs/design/Train-Survival-이미지생성-브리프.md) ·
[M8 에셋 수용 판정](docs/plans/M8/M8-에셋-수용-판정.md).

### 5.2 대상

**모든 3D 에셋이 같은 경로를 거쳤다** — 예외는 없다.

| 분류 | 경로 | 생성 경로 |
|---|---|---|
| 캐릭터 2체 | `Models/Character_Girl.fbx` · `Character_Man.fbx` | ChatGPT 이미지 → Tripo AI (리깅 포함) |
| 무기 5종 | `Models/Weapon_*.fbx` | ChatGPT 이미지 → Tripo AI |
| 열차·궤도 12종 | `Models/Train_*.fbx` | ChatGPT 이미지 → Tripo AI |
| 건축물 6종 | `Models/Structure_*.fbx` | ChatGPT 이미지 → Tripo AI |
| 자원 노드 12종 | `Models/ResourceNodes/Res_*.fbx` | ChatGPT 이미지 → Tripo AI / Meshy |
| 환경 배치물 29종 | `Models/Environment/**` | ChatGPT 이미지 → Tripo AI / Meshy |
| BaseColor 텍스처 | `Textures/T_*.png` | 위 생성물에 딸린 텍스처 (Blender에서 재정리) |
| 캐릭터 컨셉 이미지 | `Image/Player/*.png` | ChatGPT 이미지 생성 (3D 변환 입력본) |

### 5.3 캐릭터 애니메이션 (FBX 내장)

`Character_*.fbx`에 들어 있는 클립(`idle` · `walk` · `run` · `jump` · `turn` ·
`defeat_03` 등)은 이름이 `preset:biped:*` 형식이다 — **Tripo AI 리깅 프리셋
애니메이션**이며 직접 제작한 모션이 아니다.

### 5.4 상업적 이용 권리 — 확보

세 서비스 모두 **상업 이용이 허용되는 등급**에서 생성했다.

| 서비스 | 등급 | 약관상 권리 |
|---|---|---|
| **OpenAI (ChatGPT 이미지)** | 유료 구독 | 이용약관상 출력물의 권리가 사용자에게 귀속된다. 생성 이미지는 3D 변환의 *입력*으로만 쓰였고 게임 빌드에 직접 들어가지 않는다 (`Image/Player/*.png` 2장은 저장소 참고용) |
| **Tripo AI** | **Pro** (월 $19.90 · 약 3만 원) | 요금제 표기 그대로 *"Private Models · Commercial Use"*. 무료 등급의 *"Public Models · Non-Commercial Use"* 제약을 받지 않는다 |
| **Meshy** | **Pro** (월 $20 · 약 3만 원) | 유료 등급은 *"you own all assets you create with Meshy"* — 무료 등급에 붙는 CC BY 4.0 출처 표시 의무가 적용되지 않는다 |

따라서 §5의 3D 에셋은 **상업 출시에 사용할 수 있고**, 저장소 라이선스상
[`LICENSE-ASSETS`](LICENSE-ASSETS)(All Rights Reserved)의 대상이다.
이 표기는 권리에 문제가 있어서가 아니라 **무엇을 직접 만들었는지 밝히기 위한 것**이다.

> **증빙 보관** — 출시 시점에 요금제 약관이 바뀔 수 있다. 각 서비스의 결제 내역과
> 생성 당시 약관 사본을 보관해 두는 것이 안전하다. 확인 시점: 2026-08-23.

---

## 6. 무기 파지 애니메이션 — Adobe Mixamo

> **이 저장소에 포함되지 않는다.** Mixamo 약관이 원본 애니메이션 파일의 재배포를
> 금지하므로 `Anim_Hold_*.fbx` 3개를 추적에서 제외했다(`.gitignore`).
> 받는 방법은 [`Art/Animations/NOTICE.md`](Assets/_Project/Art/Animations/NOTICE.md)에 있다.

| 파일 (미포함) | Mixamo 원본 클립 | 쓰임 |
|---|---|---|
| `Art/Animations/Anim_Hold_TorchIdle.fbx` | Standing Torch Idle 01 | 한손 조준 — 리볼버 · 집게 · 망치 |
| `Art/Animations/Anim_Hold_RifleAim.fbx` | Rifle Aiming Idle | 양손 조준 — 샷건 |
| `Art/Animations/Anim_Hold_Gunplay.fbx` | Gunplay | 미사용 보관 |

Mixamo 콘텐츠는 Adobe 계정 보유자가 **로열티 없이 상업 프로젝트에 사용**할 수 있다.
제약은 출처 표시가 아니라 **에셋 파일 자체의 재배포 금지**다 — 출처를 밝혀도 해소되지 않는다.
그래서 파일을 담지 않고 **받는 절차만** 남겼다.

- <https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html>

### 6.1 저장소에 남는 것

| 항목 | 커밋 여부 | 이유 |
|---|:---:|---|
| `Anim_Hold_*.fbx` | ✗ | Mixamo 원본 파일 — 재배포 금지 |
| `Anim_Hold_*.fbx.meta` | ✓ | 임포트 설정과 `guid`만 담긴다. **애니메이션 커브 0개**이므로 Mixamo 저작물이 아니다. FBX를 같은 이름으로 되돌리면 `AC_Player.controller` 참조가 그대로 살아난다 |
| `AC_Player.controller` | ✓ | 직접 만든 스테이트 머신 |
| `ArmsOnlyMask.mask` | ✓ | 직접 만든 아바타 마스크 |

받은 FBX 3개는 **메시가 없다**(Without Skin — Geometry · Mesh · Deformer 전부 0).
캐릭터 모델은 §5의 Tripo 생성물이고, Mixamo에서 오는 것은 애니메이션 커브뿐이다.

> **이력** — 추적 제외 이전 커밋(`de9313e`)에는 파일이 남아 있다. 현재 배포 상태에서는
> 제거됐으나 완전한 삭제가 필요하면 히스토리 재작성이 따로 필요하다.

관련 결정: [무기 파지 품질 업그레이드 계획 §7](docs/plans/features/무기-파지-품질-업그레이드-계획.md)

---

## 7. 직접 제작한 부분

아래는 외부 에셋이 아니라 이 프로젝트에서 직접 만든 것이다. 저장소 라이선스가 적용된다.

| 분류 | 규모 | 라이선스 |
|---|---|---|
| C# 게임플레이 코드 | 322 파일 · 43,913줄 (`Scripts/**`) | MIT (`LICENSE`) |
| EditMode·PlayMode 테스트 | 71 파일 · 10,915줄 (`Tests/**`) | MIT |
| 셰이더 | `Art/Shaders/**` (지역 하늘 · 양식화 물 등) | MIT |
| 설계·기획 문서 | 95개 (`docs/**`) | MIT |
| 머티리얼 | `Art/*.mat` · `Art/Materials/**` | All Rights Reserved (`LICENSE-ASSETS`) |
| 프리팹 · 씬 | `Prefabs/**` · `Scenes/**` | All Rights Reserved |
| ScriptableObject 데이터 (밸런스·레시피·지역) | `Data/**` | All Rights Reserved |
| 애니메이터 · 아바타 마스크 | `Art/Animations/AC_Player.controller` · `ArmsOnlyMask.mask` | All Rights Reserved |
| 게임 디자인 (코어 루프 · 시스템 · 세계관) | `docs/design/**` | MIT (문서) |

> 3D 모델과 텍스처는 §5대로 **생성 도구 출력물을 다듬은 것**이다. 원본 메시를
> 직접 모델링하지 않았다. Blender 정규화·감축·머티리얼 통합·리깅 검수와, Unity 반입 후의
> 프리팹 구성·배치·머티리얼 설정은 직접 작업했다.

---

## 8. 누락 신고

빠졌거나 잘못 표기된 항목이 있으면
[이슈](https://github.com/hanwoolhanwool/Train-Survival/issues)로 알려주면 정정한다.
