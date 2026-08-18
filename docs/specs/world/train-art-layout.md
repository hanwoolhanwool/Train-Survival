# 열차 아트 배치 참조 (Game_ArtTest)

QA 중 열차 아트를 교체·정렬할 때 빠르게 고치기 위한 참조표.
대상 씬: `Assets/_Project/Scenes/Game_ArtTest.unity` / 최종 확인 2026-08-19.

**현재 기관차: `Train_Locomotive_Open`** (교체 이력은 2절, 되돌리는 명령은 5절).

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
| `Connector_2 / _3 / _4` | z = −8.25 / −24.75 / −41.25 | (1.4, 0.2, 1.6) | Cube + `M_Default` |
| `BoardingRamp` | (0, 0.85, −26.0), rot(335, 0, 0) | (2, 0.2, 8.747) | Cube + `M_Default` |

### 칸 내부 아트 구성

- **`Car_1` ~ `Car_4`**: `*_Art` → `Flatbed_0` / `Flatbed_1` (각각 `Train_Flatbed_A` 1개) + `Axle_N` 4개
  (`Axle` 하나당 `Orient` 좌우 2개, 각 `Orient` 밑에 `Train_Wheel` 1개 → 칸당 바퀴 8개)
- **`Car_Locomotive`**: `Locomotive_Art` → `Locomotive` → 기관차 모델 **1개뿐**.
  바퀴·대차가 기관차 메시에 포함되어 있어 별도 `Axle`/`Wheel`이 없다.

---

## 2. 기관차 — 현재 상태와 교체 이력

씬 경로: `Train / Car_Locomotive / Locomotive_Art / Locomotive / <모델>`

| 항목 | HQ (초기) | HQ_Open (1차 교체) | **Open (현재)** |
|---|---|---|---|
| 모델 | `Train_Locomotive_HQ.fbx` | `Train_Locomotive_HQ_Open.fbx` | **`Train_Locomotive_Open.fbx`** |
| 머티리얼 | `M_Train_Locomotive` | `M_Train_Locomotive_HQ_Open` | **`M_Train_Locomotive_Open`** |
| 텍스처 | `T_Train_Locomotive_BaseColor` | `T_Train_Locomotive_HQ_Open_BaseColor` | `T_Train_Locomotive_Open_BaseColor` |
| 메시 이름 | `zz_src_Train_Locomotive` | `Train_Locomotive_HQ_Open` | `Train_Locomotive_Open` |
| 버텍스 / 트라이앵글 | 20,814 / 28,117 | 36,097 / 48,523 | **33,096 / 47,640** |
| 모델 로컬 길이축 | **X** | **Y** | **Y** (HQ_Open과 동일) |
| 씬 회전 (Euler) | (−90, 180, **0**) | (−90, 180, **90**) | (−90, 180, **90**) |
| 씬 회전 (Quaternion) | (0, 0.7071068, 0.7071068, w 0) | (0.5, 0.5, 0.5, w −0.5) | (0.5, 0.5, 0.5, w −0.5) |
| localPosition | (1.94, 0, 0) | 동일 | 동일 |
| localScale | 200 | 동일 | 동일 |
| 월드 크기 (폭·높이·길이) | 6.004 × 9.094 × 13.603 | 5.710 × 7.309 × 13.563 | **5.372 × 8.594 × 13.587** |
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
| `Train_Locomotive_HQ_Open` | 36,097 / 48,523 | (0.0285, 0.0678, 0.0365) | **Y** | 미사용 (1차 교체 이력) |
| `Train_Locomotive_Open` | 33,096 / 47,640 | (0.0269, 0.0679, 0.0430) | **Y** | **현재 기관차** |
| `Train_Flatbed_A` | 7,102 / 4,773 | (0.0600, 0.0328, 0.0102) | X | 칸 바닥, 칸당 2개 |
| `Train_Flatbed_B` | 7,102 / 4,773 | (0.0600, 0.0328, 0.0102) | X | 미사용 (정점·삼각형·바운즈가 A와 동일, 텍스처만 다른 변형) |
| `Train_Wheel` | 1,506 / 1,486 | (0.0200, 0.0077, 0.0200) | X | 칸당 8개 |
| `Train_CarModule` | 4,195 / 2,308 | (0.0400, 0.0292, 0.0092) | X | 미사용 |
| `Train_RailTrack` | 8,876 / 7,668 | (6.4577, 34.6727, 1.4756) | Y | 미사용 (루트 스케일 1로 예외) |

씬/프리팹에 실제로 배치된 것은 `Train_Locomotive_Open`, `Train_Flatbed_A`, `Train_Wheel` 3종뿐이다.
나머지는 반입만 되어 있는 후보 에셋이라 교체 대상으로 바로 쓸 수 있다.

---

## 4. 에셋 GUID

씬 YAML을 직접 고칠 때 쓰는 값.

| 모델 (FBX) | GUID |
|---|---|
| `Train_Locomotive` | `1572e0280b4d62a459487d2a03abc5de` |
| `Train_Locomotive_HQ` | `c49d0fe89871f7f45abb58130ba10f9f` |
| **`Train_Locomotive_Open`** | **`7eba08cf73d53c6b485936e46894fbe7`** |
| `Train_Locomotive_HQ_Open` | `098021ce449c33715242f8eb235571d2` |
| `Train_Flatbed_A` | `e93875a54fbd5c942aaac1aa17096146` |
| `Train_Flatbed_B` | `69666e88f2ca8a64286f85d491550334` |
| `Train_Wheel` | `f2999edef6182e9498b1aa2fafc5e357` |
| `Train_CarModule` | `c2ad7830f85db314d9c8c626dee2d22b` |
| `Train_RailTrack` | `77de94e710b060a4dbdcad660329fbf8` |

| 머티리얼 | GUID | BaseMap 텍스처 |
|---|---|---|
| `M_Train_Locomotive` | `e0fdfd8fb45ae0549bb354b06d1b786d` | `T_Train_Locomotive_BaseColor` (`25c48da444a5e4f489144f0aa3b2297e`) |
| **`M_Train_Locomotive_Open`** | **`14e4eb30480c9ab01607e4a007ab3d88`** | `T_Train_Locomotive_Open_BaseColor` (`3e2adc38ab37db5e7602b07365e93206`) |
| `M_Train_Locomotive_HQ_Open` | `b3c9156091aae8b4c99f9c61d87f59a2` | `T_Train_Locomotive_HQ_Open_BaseColor` (`48202c76339f0c8303fc2579d719bae1`) |
| `M_Train_Flatbed_A` | `f46a3e9c7cca3a14f8ed542f056be44f` | `T_Train_Flatbed_A_BaseColor` (`a679134b5e9e40444944b9ce6a4d64d3`) |
| `M_Train_Flatbed_B` | `c2fc95342f0cb8f4c96ac0fe383fcdab` | `T_Train_Flatbed_B_BaseColor` (`3277dfe093b4526488df18f169a07254`) |
| `M_Train_Wheel` | `b39f4766a917a6d439f5843048bbd6c6` | `T_Train_Wheel_BaseColor` (`43f81b015fe6a8d469ad41ce521d398e`) |
| `M_Train_CarModule` | `e9e454fd66532de4f84e72e146663442` | `T_Train_CarModule_BaseColor` (`2de49817221c5474799f96887118ed07`) |
| `M_Train_RailTrack` | `b11c36800c4893044b5519a2bb909f5a` | `T_Train_RailTrack_BaseColor` (`95e4b9de74748724b8717c61dffbba89`) |

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

# 현재(Open) → HQ_Open
sed -i -e 's/7eba08cf73d53c6b485936e46894fbe7/098021ce449c33715242f8eb235571d2/g' \
       -e 's/guid: 14e4eb30480c9ab01607e4a007ab3d88, type: 2/guid: b3c9156091aae8b4c99f9c61d87f59a2, type: 2/' \
       -e 's/value: Train_Locomotive_Open$/value: Train_Locomotive_HQ_Open/' "$S"

# HQ_Open → Open (되돌리기)
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

## 관련 문서

- [아트·렌더링 예산](../../design/Train-Survival-아트-렌더링-예산.md) — 폴리곤/드로우콜 기준
- [아키텍처 규칙](../../conventions/architecture-rules.md)
