# 열차 아트 배치 참조 (Game_ArtTest)

QA 중 열차 아트를 교체·정렬할 때 빠르게 고치기 위한 참조표.
대상 씬: `Assets/_Project/Scenes/Game_ArtTest.unity` / 최종 확인 2026-08-23.

**현재 기관차: `Train_Locomotive_HQ_Open`** (2026-08-23 교체 — 이력은 2절, 되돌리는 명령은 5절).
**기찻길·접지 높이는 7절** — 이 씬의 열차는 궤도 위에 얹히느라 `Game.unity`보다 **y가 0.916 높다**.
**그 높이를 통째로 올리고 내리는 축(QA 3단계 토글 — F2)은 [열차·궤도 높이 규약](train-elevation.md)**이 다룬다.

---

## 1. 좌표·스케일 규약

- **`Train`의 forward = +Z.** 기관차가 편성 맨 앞(z = 13.862)이고, 뒤 칸일수록 z가 작아진다.
  → 기관차 모델의 굴뚝·카우캐처는 **+Z를 향해야** 한다.
- 칸(`Car_*`)은 **비균등 스케일 큐브**다. 그 밑에 아트를 그냥 붙이면 찌그러지므로,
  각 칸은 `*_Art` 홀더가 칸 스케일의 **역수**를 걸어 단위 공간으로 되돌린 뒤 아트를 담는다.
  - `Locomotive_Art` scale = (0.2174, 0.2941, 0.0735) = 1/(4.6, 3.4, 13.603)
  - `Car1_Art` ~ `Car4_Art` scale = (0.2174, 0.2941, 0.0667) = 1/(4.6, 3.4, 15.0)
  - **아트를 새로 붙일 때는 반드시 `*_Art` 홀더 밑에** 넣는다. 칸 직속으로 넣으면 비균등 스케일에 눌린다.
- 모든 열차 FBX는 Blender Z-up으로 반입되어 **로컬 +Z가 위**, 루트 스케일 100 (`Train_RailTrack` 제외).
  바닥이 로컬 z = 0에 맞춰져 있어 (`bounds.center.z ≈ 높이/2`) 모델을 갈아도 접지 높이는 유지된다.

### 편성 배치 (Train 직속)

| 오브젝트 | localPos | localScale | 비고 |
|---|---|---|---|
| `Car_Locomotive` | (0, 1.70, **13.862**) | (4.6, 3.4, 13.603) | 편성 선두 |
| `Car_1` | (0, 1.70, 0) | (4.6, 3.4, 15) | |
| `Car_2` | (0, 1.70, −16.5) | (4.6, 3.4, 15) | |
| `Car_3` | (0, 1.70, −33.0) | (4.6, 3.4, 15) | |
| `Car_4` | (0, 1.70, −49.5) | (4.6, 3.4, 15) | |
| `Connector_2 / _3 / _4` | (0, **1.952**, −8.25 / −24.75 / −41.25) | (1.4, 0.2, 1.6) | `Train_Coupler` FBX (8절) |
| `BoardingRamp` | (5.8212, 0.9368, −16.5), rot(0, 270, 335) | (2, 0.2, 8.3393) | **아직 Cube + `M_Default`** |

> `Train_Handrails`는 `Train` 밖의 별도 루트다 (localPos (0, 0.916, 0), `TrainElevationFollower`). 8.2절 참조.

### 칸 내부 아트 구성

- **`Car_1` ~ `Car_4`**: `*_Art` → `Flatbed_0` / `Flatbed_1` (각각 `Train_Flatbed_A` 1개) + `Axle_N` 4개
  (`Axle` 하나당 `Orient` 좌우 2개, 각 `Orient` 밑에 `Train_Wheel` 1개 → 칸당 바퀴 8개)
- **`Car_Locomotive`**: `Locomotive_Art` → `Locomotive` → 기관차 모델 **1개뿐**.
  바퀴·대차가 기관차 메시에 포함되어 있어 별도 `Axle`/`Wheel`이 없다.

---

## 2. 기관차 — 현재 상태와 교체 이력

씬 경로: `Train / Car_Locomotive / Locomotive_Art / Locomotive / <모델>`

| 항목 | HQ (초기) | **HQ_Open (현재)** | Open (2차 교체 이력) |
|---|---|---|---|
| 모델 | `Train_Locomotive_HQ.fbx` | **`Train_Locomotive_HQ_Open.fbx`** | `Train_Locomotive_Open.fbx` |
| 머티리얼 | `M_Train_Locomotive` | **`M_Train_Locomotive_HQ_Open`** | `M_Train_Locomotive_Open` |
| 텍스처 | `T_Train_Locomotive_BaseColor` | `T_Train_Locomotive_HQ_Open_BaseColor` | `T_Train_Locomotive_Open_BaseColor` |
| 메시 이름 | `zz_src_Train_Locomotive` | **`Train_Locomotive_HQ_Open`** | `Train_Locomotive_Open` |
| 버텍스 / 트라이앵글 | 20,814 / 28,117 | **36,097 / 48,523** | 33,096 / 47,640 |
| 모델 로컬 길이축 | **X** | **Y** | **Y** (HQ_Open과 동일) |
| 씬 회전 (Euler) | (−90, 180, **0**) | (−90, 180, **90**) | (−90, 180, **90**) |
| 씬 회전 (Quaternion) | (0, 0.7071068, 0.7071068, w 0) | (0.5, 0.5, 0.5, w −0.5) | (0.5, 0.5, 0.5, w −0.5) |
| localPosition | (1.94, 0, 0) | 동일 | 동일 |
| localScale | 200 | 동일 | 동일 |
| 월드 크기 (폭·높이·길이) | 6.004 × 9.094 × 13.603 | **5.710 × 7.309 × 13.563** | 5.372 × 8.594 × 13.587 |
| 월드 중심 | (0, 4.607, 13.862) | (0, 3.714, 13.862) | (0, 4.357, 13.862) |
| 접지 y | 0.060 | 0.060 | 0.060 (전부 동일) |

**Open 계열끼리는 회전·위치·스케일이 완전히 같다.** `Train_Locomotive_Open` ↔ `Train_Locomotive_HQ_Open`은
GUID·머티리얼·`m_Name` 3가지만 바꾸면 서로 오간다 (5절 참조).
`Train_Locomotive_HQ`(길이축 X)로 되돌릴 때만 회전을 (−90, 180, 0)으로 함께 되돌린다.

### 트랜스폼 체인 (월드 z = 13.862가 나오는 경로)

```
Car_Locomotive   p(0, 1.70, 13.862)  s(4.6, 3.4, 13.603)
└ Locomotive_Art p(0, −0.50, −0.85)  s(1/4.6, 1/3.4, 1/13.603)   → 월드 y=0(지면), z=2.30
  └ Locomotive   p(0, 0.06, 13.50)   rot(0, 90, 0)  s(1,1,1)     → 월드 z=15.80, 접지 y=0.06
    └ 모델        p(1.94, 0, 0)       rot(−90,180,90) s(200)       → 부모 Y90 때문에 z −1.94 → 월드 z=13.862
```

- **접지 높이**는 `Locomotive`의 y = 0.06이 잡는다. 모델이 떠 보이면 여기를 만진다.
- **앞뒤 위치**는 모델의 `localPosition.x` = 1.94가 잡는다 (부모가 Y 90° 회전이라 월드로는 −Z 이동).
  값을 키우면 기관차가 편성 뒤쪽(−Z)으로, 줄이면 앞쪽(+Z)으로 간다.

### 알려진 특성 (버그 아님)

- Open 계열은 **운전실 뒷면이 열린 형태**다. 그 열린 면이 화물칸 쪽(−Z)을 향하는 것이 정상 배치다.
- 높이는 모델마다 다르다: HQ 9.09 → HQ_Open 7.31 → Open 8.59. 굴뚝·돔 실루엣 차이이며 접지는 셋 다 0.060으로 같다.
- 기관차 폭이 칸 큐브 폭(4.6)보다 크다 (HQ 6.00 / HQ_Open 5.71 / Open 5.37). 초기부터 그랬던 **의도된 오버행**이다.
- `Train_Locomotive_Open`은 HQ_Open보다 채도가 높은 컬러 스킴(주황 지붕·보라 보일러)이라 톤이 눈에 띄게 다르다.

---

## 3. 모델별 로컬 축 — 교체 시 회전 결정표

모든 열차 FBX는 로컬 +Z가 위. **길이축이 X냐 Y냐만 확인하면 씬 회전이 정해진다.**

| 회전이 필요한 경우 | 씬 Euler | Quaternion (x, y, z, w) |
|---|---|---|
| 길이축 = 로컬 **X** | (−90, 180, 0) | (0, 0.7071068, 0.7071068, 0) |
| 길이축 = 로컬 **Y** | (−90, 180, **90**) | (0.5, 0.5, 0.5, −0.5) |
| 길이축 = 로컬 Y, 앞뒤 반대 | (−90, 180, **−90**) | (−0.5, 0.5, 0.5, 0.5) |
| 길이축 = 로컬 X, 앞뒤 반대 | (−90, 0, 0) | (−0.7071068, 0, 0, 0.7071068) |

> Unity Euler는 Z→X→Y 순으로 적용되므로, **Z 성분이 모델 로컬 축 기준의 회전**이 된다.
> 즉 길이축을 돌리려면 Euler의 z만 ±90 만지면 되고, 앞뒤 반전은 z를 ±180 뒤집으면 된다.

| FBX | 버텍스 / 트라이앵글 | 로컬 size (X, Y, Z) | 길이축 | 씬 사용처 |
|---|---|---|---|---|
| `Train_Locomotive` | 12,278 / 14,057 | (0.0679, 0.0300, 0.0454) | X | 미사용 (구형) |
| `Train_Locomotive_HQ` | 20,814 / 28,117 | (0.0680, 0.0300, 0.0455) | X | 미사용 (초기 기관차) |
| `Train_Locomotive_HQ_Open` | 36,097 / 48,523 | (0.0285, 0.0678, 0.0365) | **Y** | **현재 사용 중** |
| `Train_Locomotive_Open` | 33,096 / 47,640 | (0.0269, 0.0679, 0.0430) | **Y** | 미사용 (2차 교체 이력) |
| `Train_Flatbed_A` | 7,102 / 4,773 | (0.0600, 0.0328, 0.0102) | X | 칸 바닥, 칸당 2개 |
| `Train_Flatbed_B` | 7,102 / 4,773 | (0.0600, 0.0328, 0.0102) | X | 미사용 (정점·삼각형·바운즈가 A와 동일, 텍스처만 다른 변형) |
| `Train_Wheel` | 1,506 / 1,486 | (0.0200, 0.0077, 0.0200) | X | 칸당 8개 |
| `Train_CarModule` | 4,195 / 2,308 | (0.0400, 0.0292, 0.0092) | X | 미사용 |
| `Train_RailTrack` | 8,876 / 7,668 | (6.4577, 34.6727, 1.4756) | Y | **`TerrainTile_Rail` 프리팹** (7절 — 루트 스케일 1로 예외) |

씬/프리팹에 실제로 배치된 것은 `Train_Locomotive_HQ_Open`, `Train_Flatbed_A`, `Train_Wheel`,
`Train_RailTrack` 4종이다. 나머지는 반입만 되어 있는 후보 에셋이라 교체 대상으로 바로 쓸 수 있다.
`Train_RailTrack`만 씬이 아니라 **지형 타일 프리팹**에 들어 있다 (런타임 스트리밍 — 7.1).

---

## 4. 에셋 GUID

씬 YAML을 직접 고칠 때 쓰는 값.

| 모델 (FBX) | GUID |
|---|---|
| `Train_Locomotive` | `1572e0280b4d62a459487d2a03abc5de` |
| `Train_Locomotive_HQ` | `c49d0fe89871f7f45abb58130ba10f9f` |
| `Train_Locomotive_Open` | `7eba08cf73d53c6b485936e46894fbe7` |
| **`Train_Locomotive_HQ_Open`** | **`098021ce449c33715242f8eb235571d2`** |
| `Train_Flatbed_A` | `e93875a54fbd5c942aaac1aa17096146` |
| `Train_Flatbed_B` | `69666e88f2ca8a64286f85d491550334` |
| `Train_Wheel` | `f2999edef6182e9498b1aa2fafc5e357` |
| `Train_CarModule` | `c2ad7830f85db314d9c8c626dee2d22b` |
| `Train_RailTrack` | `77de94e710b060a4dbdcad660329fbf8` |
| **`Train_Coupler`** | **`3f1c8a24d9b7e5a1c40e2b6f7d95a318`** |
| **`Train_Handrail`** | **`6b2d47e0f81a3c9d5e07b4a2c86f1d53`** |

| 프리팹 | GUID | 비고 |
|---|---|---|
| `TerrainTile` (기본) | `d52759794a78b3346be10b25945dca1e` | `Game.unity`가 쓴다 |
| **`TerrainTile_Rail`** | **`8f25cf9c8ed70a04cbc5a26c983ab9ee`** | 기본 타일의 **배리언트** — `Game_ArtTest`가 쓴다 (7절) |

| 머티리얼 | GUID | BaseMap 텍스처 |
|---|---|---|
| `M_Train_Locomotive` | `e0fdfd8fb45ae0549bb354b06d1b786d` | `T_Train_Locomotive_BaseColor` (`25c48da444a5e4f489144f0aa3b2297e`) |
| `M_Train_Locomotive_Open` | `14e4eb30480c9ab01607e4a007ab3d88` | `T_Train_Locomotive_Open_BaseColor` (`3e2adc38ab37db5e7602b07365e93206`) |
| **`M_Train_Locomotive_HQ_Open`** | **`b3c9156091aae8b4c99f9c61d87f59a2`** | `T_Train_Locomotive_HQ_Open_BaseColor` (`48202c76339f0c8303fc2579d719bae1`) |
| `M_Train_Flatbed_A` | `f46a3e9c7cca3a14f8ed542f056be44f` | `T_Train_Flatbed_A_BaseColor` (`a679134b5e9e40444944b9ce6a4d64d3`) |
| `M_Train_Flatbed_B` | `c2fc95342f0cb8f4c96ac0fe383fcdab` | `T_Train_Flatbed_B_BaseColor` (`3277dfe093b4526488df18f169a07254`) |
| `M_Train_Wheel` | `b39f4766a917a6d439f5843048bbd6c6` | `T_Train_Wheel_BaseColor` (`43f81b015fe6a8d469ad41ce521d398e`) |
| `M_Train_CarModule` | `e9e454fd66532de4f84e72e146663442` | `T_Train_CarModule_BaseColor` (`2de49817221c5474799f96887118ed07`) |
| `M_Train_RailTrack` | `b11c36800c4893044b5519a2bb909f5a` | `T_Train_RailTrack_BaseColor` (`95e4b9de74748724b8717c61dffbba89`) |
| **`M_Train_Coupler`** | **`b034a58810969d27aa094bfa3706be4c`** | `T_Train_Coupler_BaseColor` (`7526b34ad8ef1f16a3a4aa47b7dc585a`) |
| **`M_Train_Handrail`** | **`08de460118c626256c9401bac8f306b2`** | `T_Train_Handrail_BaseColor` (`867e656c53ef03afea5ad2e6dd848ab1`) |

---

## 5. 씬 YAML 편집 위치와 절차

씬 배선은 **Unity의 씬 저장을 거치지 않고 YAML을 직접 고친다.**
(`manage_scene save`는 수천 줄짜리 재정렬 diff를 만든다.)

### 기관차 블록 찾기

`Game_ArtTest.unity`의 `PrefabInstance &160204739` (2026-08-19 기준 **428~508행**).
라인은 밀릴 수 있으므로 다음으로 찾는다:

```bash
grep -n "propertyPath: m_Name" -A1 Assets/_Project/Scenes/Game_ArtTest.unity | grep Locomotive  # 현재 모델 이름
grep -n "&160204739" Assets/_Project/Scenes/Game_ArtTest.unity                                  # PrefabInstance 앵커
```

부모 앵커: `Locomotive` Transform = `&2112464942`, `Locomotive_Art` Transform = `&317765819`.

### 모델을 갈아끼울 때 바꿀 것 (6가지)

1. 블록 안 **모든** `guid:` → 새 FBX GUID (기관차 블록에는 18곳, 마지막 `m_SourcePrefab`과 아래 stripped Transform 포함)
2. `propertyPath: m_Name` 의 `value:` → 새 모델 이름
3. `m_Materials.Array.data[0]` 의 `objectReference` guid → 새 머티리얼
4. `m_LocalRotation.w/.x/.y/.z` → 3절 표의 Quaternion
5. `m_LocalEulerAnglesHint.x/.y/.z` → 3절 표의 Euler (인스펙터 표시용, 회전 자체는 4번이 결정)
6. 필요 시 `m_LocalPosition.x`(앞뒤) / 부모 `Locomotive`의 y(접지)

> **fileID는 대개 그대로 써도 된다.** 열차 FBX들은 루트가 단일 메시라 내부 fileID가 공통이다
> (Transform `-8679921383154817045`, MeshRenderer `-7511558181221131132`, GameObject `919132149155446097`,
> MeshFilter `-5754084199372789682`). 구조가 다른 FBX로 바꿀 땐 6절 스니펫으로 먼저 확인한다.

### Open 계열 상호 교체 (복붙용)

회전이 같으므로 GUID·머티리얼·이름 3줄이면 끝난다. Git Bash 기준:

```bash
S="Assets/_Project/Scenes/Game_ArtTest.unity"

# Open → HQ_Open (2026-08-23 이 방향으로 교체함 — 현재는 HQ_Open)
sed -i -e 's/7eba08cf73d53c6b485936e46894fbe7/098021ce449c33715242f8eb235571d2/g' \
       -e 's/guid: 14e4eb30480c9ab01607e4a007ab3d88, type: 2/guid: b3c9156091aae8b4c99f9c61d87f59a2, type: 2/' \
       -e 's/value: Train_Locomotive_Open$/value: Train_Locomotive_HQ_Open/' "$S"

# 현재(HQ_Open) → Open (되돌리기)
sed -i -e 's/098021ce449c33715242f8eb235571d2/7eba08cf73d53c6b485936e46894fbe7/g' \
       -e 's/guid: b3c9156091aae8b4c99f9c61d87f59a2, type: 2/guid: 14e4eb30480c9ab01607e4a007ab3d88, type: 2/' \
       -e 's/value: Train_Locomotive_HQ_Open$/value: Train_Locomotive_Open/' "$S"
```

치환 후 `grep -c <새 GUID> "$S"` 가 **18**이면 정상이다.
`Train_Locomotive_HQ`(길이축 X)로 되돌릴 때는 위 3줄에 더해 회전 5줄(3절 표)까지 함께 바꾼다.

### 편집 전 주의

- 해당 씬이 **Unity에서 열려 있으면** 외부 편집 직후 "modified externally" 모달이 떠서 MCP 전체가 막힌다.
  → 편집 전에 다른 씬(`Boot`)을 로드해 두고, 편집이 끝나면 `Game_ArtTest`를 다시 로드한다.
- 한글이 든 파일은 PowerShell `Get-Content`/`Set-Content` 왕복 금지 (모지바케). `sed`나 편집 도구를 쓴다.

---

## 6. 검증 스니펫

### 새 FBX의 내부 fileID·축 확인

```csharp
var p = "Assets/_Project/Art/Models/<이름>.fbx";
foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(p)) {
    string g; long id;
    UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out g, out id);
    UnityEngine.Debug.Log(a.GetType().Name + " | " + id);
}
var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p);
UnityEngine.Debug.Log(go.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.bounds); // 길이축 판정
```

### 배치·방향 눈으로 확인

`manage_camera` 로 4방향 오빗 캡처:

```
action=screenshot, batch=orbit, view_target="Train/Car_Locomotive",
orbit_angles=4, orbit_elevations=[20], include_image=true
```

- `back_*` 컷에서 굴뚝·카우캐처가 보이면 **앞뒤가 뒤집힌 것** → Euler z를 ±180 뒤집는다.
- `front_*` 컷에 앞머리, `right/left_*` 컷에서 화물칸이 기관차 뒤(−Z)에 붙어 보이면 정상.

### 배치 수치 확인

```csharp
var mr = UnityEngine.GameObject.Find("Train/Car_Locomotive/Locomotive_Art/Locomotive")
    .GetComponentInChildren<UnityEngine.MeshRenderer>();
UnityEngine.Debug.Log(mr.bounds.size + " / " + mr.bounds.center + " / minY=" + mr.bounds.min.y);
```

접지 y가 0.060, 중심 z가 13.862면 기존 배치와 동일하다.

---

## 7. 기찻길 — 궤도 타일과 접지 높이 (2026-08-19 반입)

열차 밑에 궤도가 깔리면서 **접지 기준선이 지면(y 0)에서 레일 상면(y 0.976)으로 올라갔다.**
`Game_ArtTest`에만 적용된 상태이며, **`Game.unity`는 아직 옛 높이다** (7.3 경고).

> 이 절은 **기준선이 어떻게 생겼는지**를 적는다. 그 기준선을 QA 중에 통째로 내려 보고
> 최종값을 굳히는 절차는 [열차·궤도 높이 규약](train-elevation.md)에 있다 (F2 = 현재/아래/더 아래).

### 7.1 궤도는 지형 타일이 만든다 — `TerrainTile_Rail`

궤도는 씬에 놓인 오브젝트가 아니라 **스트리밍되는 지형 타일의 일부**다. 열차는 원점 고정이고
월드가 흐르므로(월드 스크롤), 궤도도 지형과 함께 흘러야 이음매가 어긋나지 않는다.

기본 `TerrainTile`의 **프리팹 배리언트**이고, 바뀐 것은 두 가지뿐이다 —
임시 `TrackBed` 박스(폭 3.2 판때기)를 **비활성**으로 끄고, 그 자리에 `Train_RailTrack` 실물을 얹었다.

| 항목 | 값 |
|---|---|
| 자식 이름 | `RailTrack` |
| localPosition | (0, **−0.5**, 0) |
| localRotation | (−0.7071068, 0, 0, w 0.7071067) = Euler (**−90**, 0, 0) |
| localScale | (1, **1.1859231**, 1) |
| 머티리얼 | `M_Train_RailTrack` |

**스케일 y의 근거** — 이 모델은 **도상(자갈밭)이 레일보다 짧다.** 도상 로컬 길이 33.729에
1.1859231을 곱하면 정확히 **타일 길이 40**이 된다. 대신 레일·침목은 로컬 34.673이라 타일보다
길어져 **이음매마다 1.12 m씩 겹친다** — 겹치는 쪽을 택한 것이다.
반대로 레일 전체를 40에 맞추면 이음매마다 **도상이 0.944 m 벌어져** 자갈밭이 끊겨 보인다.

**콜라이더는 단면을 층으로 나눈 `BoxCollider` 3장**이다 (로컬 +Z가 위이므로 `y`가 길이축).
타일은 풀에서 매 프레임 켜고 꺼지므로 8,876버트짜리 `MeshCollider`를 굽지 않는다.

| 층 | m_Size (x, y, z) | m_Center z | 로컬 상면 |
|---|---|---|---|
| 도상 | 6.458 × 33.729 × 1.05 | 0.525 | 1.05 |
| 침목 | 5.44 × 33.729 × 0.256 | 1.178 | 1.306 |
| 레일 | 4.43 × 33.729 × 0.17 | 1.391 | **1.476** |

→ 궤도 루트가 y −0.5에 있으므로 **월드 레일 상면 = −0.5 + 1.476 = 0.976**. 이 값이 7.3의 기준선이다.

### 7.2 어느 씬이 어떤 타일을 쓰나

| 씬 | `TerrainTileStreamer._tilePrefab` |
|---|---|
| `Game_ArtTest` | **`TerrainTile_Rail`** (`8f25cf9c…`) |
| `Game` (본편) | `TerrainTile` 기본 (`d5275979…`) |

- **`Region_Forest._terrainTilePrefab`을 비웠다**(`fileID: 0`). 지역 데이터가 타일을 직접 가리키면
  **씬별 `_tilePrefab` 설정을 덮어써서** `Game_ArtTest`에도 기본 타일이 깔린다.
  비워 두면 씬 fallback이 살아난다 — 비우기 전 값이 기본 타일과 같았으므로 `Game.unity`는 무변.
- 즉 **지역이 궤도 유무를 정하지 않는다.** 씬이 정한다. 지역별로 궤도 외형을 나누고 싶어지면
  그때 데이터 축을 되살리되, 씬 설정을 덮어쓰지 않는 우선순위 규칙을 먼저 정해야 한다.

### 7.3 열차 접지 — `Game_ArtTest`만 +0.916 ⚠️

바퀴 하단은 원래 y 0.06에 닿아 있었고(2절 접지 y), 레일 상면은 0.976이다.
그 차이 **0.916만큼 열차 계열 루트를 통째로 올렸다.**

| 오브젝트 | 이전 y | 현재 y |
|---|---|---|
| `Train` (편성 루트) | 0 | **0.916** |
| `Train_Handrails` | 0 | **0.916** |
| `EngineFuelPort` | 2.65 | **3.566** |
| `CraftingStation` | 2.65 | **3.566** |
| `Main Camera` | 8 | **8.916** |

- **`CarBuildGhost`는 제외한다** — 런타임이 칸 중심을 기준으로 그리므로 부모가 올라가면 따라온다.
- `BoardingRamp`는 높이만이 아니라 배치 자체가 바뀌었다 (7.5).
- 칸 안쪽 값(`Car_*`의 y 1.70, `Locomotive`의 접지 0.06 등 1·2절 표)은 **그대로다.**
  올린 것은 편성 루트뿐이라, 모델 교체 절차(5절)는 영향받지 않는다.

> **`Game.unity`에는 이 이동이 적용되지 않았다.** 다만 **2026-08-23부로 `Game_ArtTest`가
> 본 씬이 되어**([레벨 디자인 구현 계획 결정 ⑥](../../plans/features/레벨-디자인-구현-계획.md)),
> 이 표의 값이 곧 **현행 기준선**이다 — 이식은 더 이상 잔여 작업이 아니다.
>
> `Game.unity`를 다시 쓰기로 하면 그때 **궤도 타일 배선(7.2)과 열차 +0.916(7.3)을 함께**
> 옮긴다. 한쪽만 옮기면 열차가 궤도에 파묻히거나(타일만) 공중에 뜬다(높이만).

### 7.4 에디터 검수용 프리뷰 — 플레이 중에는 꺼진다

지형이 런타임 스트리밍이라 **에디터 씬 뷰에는 아무것도 보이지 않는다.** 아트 검수를 위해
정적 프리뷰를 세우되, 플레이 중에는 스트리밍 실물과 겹치므로 스스로 꺼지게 했다.

- `RailTrack_Preview` (localPosition (0, **−0.5**, 0)) 에 `EditorPreviewOnly` 부착 —
  `Awake`에서 `SetActive(false)`. `Assets/_Project/Scripts/Gameplay/Debugging/EditorPreviewOnly.cs`
- 자식 레일 4장을 **도상 길이 33.729 간격**으로 이어 편성 전 구간을 덮는다:
  `RailTrack_F1` z +33.729 / `RailTrack_F0` z 0 / `RailTrack_B1` z −33.729 / `RailTrack_B2` z −67.458
- 검수용 지면 판 `Ground_Preview` (0, 0.4, −17.3) 도 같은 프리뷰 묶음 아래 둔다.

### 7.5 승차 램프 — 궤도를 관통하지 않도록 옆면으로

램프가 열차 뒤로 내려가면 궤도를 뚫는다. `Game_ArtTest`에서는 **칸 옆면(+X)으로 돌렸고**,
그러려면 배치 기준점이 갑판 뒤끝이 아니라 **칸 중심**이어야 한다.

- `BoardingRampAnchor` 열거형 신설 — `RearEdge = 0`(뒤로 내려가는 램프, **직렬화 기본값**) /
  `RearCenter = 1`(옆으로 내려가는 램프). 씬마다 배치가 달라 **컴포넌트가 아니라 에셋 값으로** 고른다.
- `BoardingRampPositioner._zOffsetFromRearEdge` → `_zOffset` 개명 (`FormerlySerializedAs`로 흡수).
- `Game_ArtTest`: anchor `RearCenter` · 위치 (5.8212423, 0.9367982, −16.5) — 하단이 지면에 닿게 길이 재조정.
- **`Game.unity`는 기본값 `RearEdge`라 계산 결과가 이전과 같다** — 이식 전까지 회귀 없음.

---

## 8. 연결부·손잡이 — 프리미티브 교체 (2026-08-23 반입)

밤 방어전이 직접 겨누는 두 부위를 큐브·구체에서 모델로 바꿨다.
생성 지시는 [이미지 생성 브리프 §L](../../design/Train-Survival-이미지생성-브리프.md).

**공통 규약** — 프리미티브의 `MeshFilter`/`MeshRenderer`만 걷어내고 **콜라이더는 그대로 둔다.**
`CouplingPart`·`HandrailAnchor` 둘 다 `GetComponentsInChildren<Renderer>`로 렌더러를 모아 토글하므로,
FBX를 자식으로 넣으면 표현 on/off가 새 모델을 자동으로 따라간다. 렌더러를 남기면 다시 켜지므로 반드시 지운다.

| 에셋 | tris | globalScale | 실치수 (X, Y, Z) | 감축 |
|---|---:|---:|---|---|
| `Train_Coupler` | 3,044 | 1.6 | 0.41 × 0.56 × **1.60**(길이) | **무감축** |
| `Train_Handrail` | 1,490 | 0.8 | **0.80**(U 폭) × 0.72 × 0.45 | 0.5 |

> **감축 한계는 자연물과 정반대다.** 0.5·0.35 비교 렌더 결과, 연결부는 **0.5에서도 볼트·너클이 뭉개졌고**
> 손잡이는 파이프 곡면이라 0.35까지 버텼다. [브리프 §K.7.1](../../design/Train-Survival-이미지생성-브리프.md)의
> "직각이 뜻을 가지는 것은 감축하지 않는다"가 그대로 재현된 사례다.

### 8.1 연결부 — `Train/Connector_2 / _3 / _4`

```
Connector_N   p(0, 1.952, z)  s(1.4, 0.2, 1.6)   ← BoxCollider · CouplingPart
└ Coupler_Art p(0,0,0)  s(0.714286, 5, 0.625) = 1/(1.4, 0.2, 1.6)
  └ Train_Coupler  p(0, −0.1, 0)  s(150)
```

- **높이 기준은 갑판이 아니라 칸의 범퍼 돌출부다.** 실측 y **2.794~2.942**(중심 2.868).
  브리프 §L.1이 적은 "갑판 y 3.47"에 맞추면 연결부가 0.6 m 떠서 범퍼와 안 맞물린다.
  Train 로컬 y = 2.868 − 0.916 = **1.952**.
- 칸 끝면은 z ∓7.503(범퍼 끝), 프레임은 ∓7.059. **칸 사이 범퍼 끝 간격 1.497 m.**
- 모델은 Blender에서 **Z축 yaw 90°를 구워** 길이축을 Unity Z로 맞췄다 — 씬 회전 오버라이드가 필요 없다.

⚠️ **콜라이더가 실물을 다 못 덮는다 (판정 유보 · 2026-08-23).**
모델 스케일을 150(1.5배)으로 키우고 y −0.1 내린 결과 실물이 **0.62 × 0.83 × 2.40 m**가 되어,
`BoxCollider`(월드 1.4 × 0.6 × 1.6)를 **Z로 양 끝 0.40 m씩, Y로 0.12 m씩 벗어난다.**
삐져나온 부분은 칸 몸통에 파묻히는 구간이라 연출로는 문제가 없으나,
**눈에 보이는 연결부 끝을 조준해도 피격·수리 판정에 안 걸린다.**
판정을 실물에 맞추려면 `m_Size`를 `{x: 1, y: 4.2, z: 1.5}`로 (폭은 유지 → 좁아지는 방향 없음).

### 8.2 손잡이 — `Train_Handrails/*` (6기, 8기가 아니다)

`Train_Handrails`는 `Train` 밖의 별도 루트(localPos (0, 0.916, 0) + `TrainElevationFollower`)이고,
그 밑에 `HandrailAnchor.prefab` 인스턴스 6기가 있다. **기관차 칸에는 없다** — 브리프 §L.1의 "칸당 2 = 8"은 오기.

```
HandrailAnchor  p(±1.529, 2.313, z)  rot(90, 0, 0)  s(1, 1, 0.9)   ← SphereCollider r0.5 · NetworkObject
└ Handrail_Art  p(0, −0.4497, 0)  s(1.25)                          ← 프리팹 스케일 0.8 상쇄
  └ Train_Handrail  p(0,0,0)  s(100)
```

| 앵커 | `_carIndex` | localPos |
|---|---:|---|
| `Anchor_Car2_Front_Right` / `_Left` | 2 | (±1.529, 2.313, **−9.554**) |
| `HandrailAnchor_Car3_R` / `_L` | 3 | (±1.529, 2.313, **−26.054**) |
| `HandrailAnchor_Car4_R` / `_L` | 4 | (±1.529, 2.313, **−42.554**) |

- **z는 칸 앞끝에서 0.554 m 안쪽**이 기준이다 (칸 앞끝 = 칸 중심 z + 7.5). 칸이 늘어나도 이 규칙으로 잡는다.
- **rot(90, 0, 0)으로 눕혀 갑판 아래 앞면 시(sill)에 붙인다** — U 고리가 후방을 향해 가로로 눕는다.
  이탈 칸은 후방 수십 m로 밀려나므로 그 시점에서 정면으로 보이는 방향이다(브리프 §L.3-2).
  세워 두면 옆에서 말뚝으로만 읽혀 집게 표적 어포던스가 죽는다.
- 좌측은 **x 부호만 뒤집는다.** 회전에 yaw가 없어 좌우가 그대로 대칭이다.
- 실물 `1.000 × 0.502 × 0.899` 6기 동일, **실물 중심 = 그랩 구체 중심**(거리 0.000).

⚠️ **앵커를 옮기면 `GlobalObjectIdHash`가 재발급된다.** 씬 내 배치 `NetworkObject`라
YAML을 직접 고치면 해시가 낡은 채 남아 클라 접속이 거부될 수 있다 —
**이 6기만은 5절의 YAML 직접 편집 예외로, 에디터에서 옮기고 씬을 저장한다.**
(6기 해시: 682072729 / 1790809304 / 2944382052 / 2771162288 / 1741638086 / 3782114178)

### 8.3 남은 프리미티브

| 오브젝트 | 현재 | 실치수 |
|---|---|---|
| `Train/BoardingRamp` | Cube + `M_Default` | 7.64 × 3.71 × 2.00 m — 광각 화면에서 가장 먼저 눈에 띈다 |
| `EngineFuelPort/Visual` | Cube + `EngineFurnace` | 0.8³ — 기관차 연료 투입구, 상호작용 지점 |
| `CraftingStation/Visual` | Cube + `M_Default` | 0.9 × 0.5 × 0.6 — 제작 지점, 상호작용 지점 |

---

## 관련 문서

- [열차·궤도 높이 규약](train-elevation.md) — 7절 기준선을 함께 올리고 내리는 축·QA 3단계 토글
- [아트·렌더링 예산](../../design/Train-Survival-아트-렌더링-예산.md) — 폴리곤/드로우콜 기준
- [월드 스크롤·스트리밍](scroll-and-streaming.md) — 지형 타일 풀링·재배치 규약 (7.1의 전제)
- [아키텍처 규칙](../../conventions/architecture-rules.md)
