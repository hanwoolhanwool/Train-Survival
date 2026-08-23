# 무기 파지 애니메이션 — 반입 절차

`Anim_Hold_*.fbx` **3개는 이 저장소에 포함되지 않는다.** Adobe Mixamo 약관이
원본 애니메이션 파일의 재배포를 금지하기 때문이다 —
크레딧 표기로 해소되는 조건이 아니라 **배포 자체에 대한 제약**이다.

`.gitignore`로 제외돼 있고, **`.meta`만 커밋**돼 있다. `.meta`에는 임포트 설정과 `guid`만
들어 있고 애니메이션 커브는 없다(검증: 커브 항목 0개). 덕분에 FBX를 같은 이름으로
같은 위치에 두면 **`AC_Player.controller`의 참조가 그대로 살아난다.**

> 저장소 전체의 제3자 표기는 [`THIRD-PARTY-NOTICES.md`](../../../../THIRD-PARTY-NOTICES.md) §6.

---

## 1. 받아야 할 클립

[Mixamo](https://www.mixamo.com/)에 Adobe 계정으로 로그인해 아래 3개를 받는다.

| 저장할 파일명 | Mixamo 원본 클립 | Unity 클립명 | 프레임 | 쓰임 |
|---|---|---|---|---|
| `Anim_Hold_TorchIdle.fbx` | **Standing Torch Idle 01** | `Hold_TorchIdle` | 0–190 | `OneHandAim` — 리볼버 · 집게 / `OneHandLow` — 망치 · 근접 |
| `Anim_Hold_RifleAim.fbx` | **Rifle Aiming Idle** | `Hold_RifleAim` | 0–93 | `TwoHandAim` — 샷건 |
| `Anim_Hold_Gunplay.fbx` | **Gunplay** | `Hold_Gunplay` | 0–77 | 미사용 보관 (발사 반동 후보) — 없어도 동작한다 |

**Pistol Idle을 쓰지 않은 이유** — Mixamo에는 양손형밖에 없어서 한손 조준을
*Standing Torch Idle*(한손에 물체를 든 정지 자세)로 대체했다. 조준 각도는 IK가 맡는다.

## 2. 다운로드 설정

| 항목 | 값 |
|---|---|
| Format | `FBX Binary(.fbx)` |
| Skin | **Without Skin** — 메시 없이 애니메이션만 |
| FPS | `30` |
| Keyframe Reduction | `none` |
| Mirror | **해제** (오른손잡이 유지) |

*In Place*는 제자리 애니메이션에 뜨지 않는 옵션이다. 없어도 정상이다(걷기·달리기에만 나타난다).

## 3. 배치와 임포트

1. 받은 파일을 위 표의 **파일명 그대로** `Assets/_Project/Art/Animations/`에 둔다.
2. **Unity를 열기 전에** 두는 것이 좋다. `.meta`만 있고 FBX가 없는 상태로 에디터를 열면
   유니티가 고아 `.meta`를 지운다. 이미 지워졌다면 `git checkout -- Assets/_Project/Art/Animations/`
   로 되돌린 뒤 다시 연다.
3. `.meta`가 함께 있으면 **Rig(Humanoid) · 클립 분할 · Trim 구간 · loopTime이 자동 적용**된다.
   임포트 설정을 따로 만질 필요가 없다.

## 4. `.meta` 가 사라졌을 때 — 수기 복구

`AC_Player.controller`는 아래 `guid`로 클립을 참조한다. 새로 임포트해 `guid`가 바뀌면
Animator의 모션 슬롯이 `Missing`이 된다.

| 파일 | guid | 컨트롤러 참조 |
|---|---|---|
| `Anim_Hold_TorchIdle.fbx` | `d1b19e416ad7c5d4989065b0f96357d8` | 2곳 |
| `Anim_Hold_RifleAim.fbx` | `07616cac18fd43c4791b5f7629002639` | 1곳 |
| `Anim_Hold_Gunplay.fbx` | `6b4c585685daafc40b9a2f29dc7944e0` | 없음 |

복구는 둘 중 하나다.

- **A. `.meta`의 `guid`를 위 값으로 되돌린다** — 참조가 그대로 살아난다. 권장.
- **B. Animator 창에서 모션 슬롯을 새 클립으로 다시 지정한다** — Rig를 **Humanoid**로,
  Avatar는 캐릭터 아바타를 소스로 설정해야 Girl · Man 양쪽에 리타게팅된다.

## 5. 반입 후 확인

- **치비 리타게팅 편향** — 캐릭터가 치비 비율이라 리타게팅 시 고개가 상시 ~22° 숙는
  전례가 있다. T포즈 대조를 반드시 거친다.
- 동작 클립을 조준 대기 자세로 쓸 때는 **Trim 슬라이더로 구간을 남긴다** (위 표의 프레임 범위).

자세한 결정 경위는 [무기 파지 품질 업그레이드 계획 §7](../../../../docs/plans/features/무기-파지-품질-업그레이드-계획.md).

## 6. 라이선스

Mixamo 콘텐츠는 Adobe 계정 보유자가 **로열티 없이 상업 프로젝트에 사용**할 수 있다.
금지되는 것은 **에셋 파일 자체의 재배포**다 — 그래서 이 저장소는 파일을 담지 않고
받는 방법만 적어 둔다.

- <https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html>
