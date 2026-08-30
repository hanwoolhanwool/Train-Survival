using System;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 건축물 종류별 정의 카탈로그 (M5 3차 — 건축물 종류화). 표시명·체력·비용과
    /// 체온 효과(그늘/난방)를 데이터로 분리해, 종류별 역할이 에셋 수정만으로 조정되게 한다
    /// (<see cref="Game.Gameplay.Inventory.ResourceCatalog"/>와 같은 "enum 값 식별 + 폴백" 규약).
    /// </summary>
    [CreateAssetMenu(fileName = "StructureCatalog", menuName = "Game/Structure Catalog")]
    public sealed class StructureCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private StructureKind _kind;

            [SerializeField] private string _displayName;

            [Tooltip("최대 체력 — 칸보다 낮게 잡아 건축물부터 노출되는 위험을 만든다.")]
            [SerializeField, Min(1f)] private float _maxHealth = 50f;

            [Tooltip("설치에 드는 건자재 수 — 수리 망치 우클릭으로 지불한다.")]
            [SerializeField, Min(0)] private int _buildCost = 3;

            [Tooltip("그늘 제공 — 아래에 선 플레이어의 더위를 완화한다.")]
            [SerializeField] private bool _providesShade;

            [Tooltip("난방 제공 — 아래에 선 플레이어의 추위를 완화한다.")]
            [SerializeField] private bool _providesHeat;

            [Tooltip("난방을 유지하기 위해 태우는 초당 열차 연료 (M7 3차 강화 난방로). " +
                "0 = 연료를 쓰지 않는 난방(기존 난방기). 0보다 크고 연료가 남아 있으면 지역 한파 페널티가 사라진다.")]
            [SerializeField, Min(0f)] private float _heaterFuelPerSecond;

            [Header("건축 그리드 (건축 개편 1차)")]
            [Tooltip("점유 가로 셀 수 (회전 0 기준) — 그리드 설치의 발자국.")]
            [SerializeField, Min(1)] private int _footprintWidth = 1;

            [Tooltip("점유 세로 셀 수 (회전 0 기준).")]
            [SerializeField, Min(1)] private int _footprintLength = 1;

            [Tooltip("설치 목록에 노출되는지 — 끄면 R 순환에서 빠지고 설치가 기각된다 (돔 제외용, 계획서 §1.2). " +
                "enum 값·기존 항목은 유지된다.")]
            [SerializeField] private bool _placeable = true;

            [Tooltip("공유 저장 블록을 갖는지 (건축 개편 2차 §2.8) — 켜면 설치 시 슬롯 블록이 할당되고, " +
                "철거·파괴 시 내용물이 보따리로 배출되며, 근접하면 저장 패널이 열린다. 창고 계열 종류를 " +
                "추가할 때 코드 수정 없이 이 플래그만 켜면 된다.")]
            [SerializeField] private bool _providesStorageBlock;

            [Tooltip("철거 시 반환되는 자원 종류 (건축 개편 2차 — 결정 ⑧). 초기값 전부 목재.")]
            [SerializeField] private Game.Gameplay.Inventory.ResourceType _refundResource
                = Game.Gameplay.Inventory.ResourceType.Wood;

            [Tooltip("건축물 실물 프리팹 — 루트에 StructureView + BoxCollider, NetworkObject 없음 (계획서 §2.6). " +
                "각 피어가 리스트 동기화로 PoolManager 로컬 스폰한다.")]
            [SerializeField] private GameObject _viewPrefab;

            [Tooltip("거치 무기 설정 (M7 4차) — 비어 있으면 거치 무기가 아니다. " +
                "이 참조 하나가 '무기 건축물' 판정의 데이터 축이다: 종류 이름을 코드가 알지 않으므로 " +
                "세 번째 거치 무기는 에셋 추가만으로 성립한다(OCP).")]
            [SerializeField] private MountedWeaponSettings _mountedWeapon;

            [Header("가변 크기 (천막 계획 1차)")]
            [Tooltip("설치할 때 크기를 끌어서 정하는지 (천막 계획 결정 ②) — 켜면 우클릭 2회(시작·끝)로 " +
                "발자국이 정해지고, 카탈로그 발자국은 최소값으로만 쓰인다.")]
            [SerializeField] private bool _resizable;

            [Tooltip("셀 1칸당 건축 비용 (천막 계획 결정 ⑤) — 0이면 크기와 무관하게 BuildCost 고정. " +
                "가변 크기는 넓이가 곧 재료라, 0.25 = 셀 4칸당 1개.")]
            [SerializeField, Min(0f)] private float _costPerCell;

            [Tooltip("체온 효과가 닿는 범위 (천막 계획 결정 ③) — Car = 그 칸 어디서든(기존 규약), " +
                "Footprint = 발자국 안에 있어야 한다.")]
            [SerializeField] private ShelterScope _shelterScope = ShelterScope.Car;

            [Tooltip("그리드에서 실제로 막는 셀 모양 (천막 계획 결정 ⑥) — Solid = 발자국 전체(기존 규약), " +
                "Corners = 네 모서리만(천막 기둥). Corners면 안쪽에 다른 건축물이 들어간다.")]
            [SerializeField] private StructureOccupancy _occupancy = StructureOccupancy.Solid;

            public StructureKind Kind => _kind;

            public string DisplayName => _displayName;

            public float MaxHealth => _maxHealth;

            public int BuildCost => _buildCost;

            public bool ProvidesShade => _providesShade;

            public bool ProvidesHeat => _providesHeat;

            /// <summary>난방 유지에 태우는 초당 연료 (M7 3차). 0 = 연료를 쓰지 않는다.</summary>
            public float HeaterFuelPerSecond => _heaterFuelPerSecond;

            /// <summary>점유 가로 셀 수 (회전 0 기준) — 0 이하 직렬화 잔재는 1로 보정한다.</summary>
            public int FootprintWidth => Mathf.Max(1, _footprintWidth);

            /// <summary>점유 세로 셀 수 (회전 0 기준).</summary>
            public int FootprintLength => Mathf.Max(1, _footprintLength);

            /// <summary>설치 목록 노출 여부 — false면 R 순환 제외 + 설치 기각 (돔).</summary>
            public bool Placeable => _placeable;

            /// <summary>공유 저장 블록 보유 여부 (2차 §2.8) — 창고 계열 판정의 데이터 축.</summary>
            public bool ProvidesStorageBlock => _providesStorageBlock;

            /// <summary>철거 반환 자원 종류 (2차 — 결정 ⑧).</summary>
            public Game.Gameplay.Inventory.ResourceType RefundResource => _refundResource;

            /// <summary>건축물 실물 프리팹 (계획서 §2.6) — 없으면 뷰 스폰을 건너뛴다.</summary>
            public GameObject ViewPrefab => _viewPrefab;

            /// <summary>거치 무기 설정 (M7 4차 §2.1) — null이면 이 종류는 거치 무기가 아니다.</summary>
            public MountedWeaponSettings MountedWeapon => _mountedWeapon;

            /// <summary>설치 시 크기를 끌어서 정하는지 (천막 계획 결정 ②).</summary>
            public bool Resizable => _resizable;

            /// <summary>셀 1칸당 건축 비용 (결정 ⑤) — 0이면 <see cref="BuildCost"/> 고정.</summary>
            public float CostPerCell => _costPerCell;

            /// <summary>체온 효과가 닿는 범위 (결정 ③) — 기본은 기존 규약인 칸 단위.</summary>
            public ShelterScope ShelterScope => _shelterScope;

            /// <summary>그리드에서 실제로 막는 셀 모양 (결정 ⑥) — 기본은 기존 규약인 발자국 전체.</summary>
            public StructureOccupancy Occupancy => _occupancy;
        }

        [Tooltip("종류별 정의 — Kind 값으로 식별하므로 배열 순서는 자유다(설치 UI의 순환 순서로만 쓰인다).")]
        [SerializeField] private Entry[] _entries;

        /// <summary>설치 UI가 순환할 수 있는 종류 수 — 등재된 엔트리 수.</summary>
        public int EntryCount => _entries != null ? _entries.Length : 0;

        /// <summary>
        /// 등재 순서 <paramref name="index"/>의 종류 — 카탈로그 <b>전체를 훑어야 하는</b> 소비자용
        /// 열거면 (M7 3차 연료 소모 합산). 소비자가 특정 종류를 이름으로 알지 않아도 되게 한다.
        /// </summary>
        public bool TryGetKindAt(int index, out StructureKind kind)
        {
            if (_entries == null || index < 0 || index >= _entries.Length || _entries[index] == null)
            {
                kind = default;
                return false;
            }

            kind = _entries[index].Kind;
            return true;
        }

        public string GetDisplayName(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null && !string.IsNullOrEmpty(entry.DisplayName)
                ? entry.DisplayName
                : kind.ToString();
        }

        public float GetMaxHealth(StructureKind kind, float fallback)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.MaxHealth : fallback;
        }

        public int GetBuildCost(StructureKind kind, int fallback)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.BuildCost : fallback;
        }

        public bool ProvidesShade(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null && entry.ProvidesShade;
        }

        public bool ProvidesHeat(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null && entry.ProvidesHeat;
        }

        /// <summary>
        /// 난방 유지에 태우는 초당 연료 (M7 3차). 0 = 연료를 쓰지 않는 난방 — 미등재 종류도 0이라
        /// 기존 난방기·돔이 무수정으로 통과한다 (스탬피드 확률·보스 정의와 같은 소급 규약).
        /// </summary>
        public float GetHeaterFuelPerSecond(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.HeaterFuelPerSecond : 0f;
        }

        /// <summary>점유 면적 (회전 0 기준, 셀 수) — 미등재 종류는 1×1 (설치 자체는 Placeable이 거른다).</summary>
        public void GetFootprint(StructureKind kind, out int width, out int length)
        {
            Entry entry = Find(kind);
            width = entry != null ? entry.FootprintWidth : 1;
            length = entry != null ? entry.FootprintLength : 1;
        }

        /// <summary>설치 목록 노출 여부 — 미등재 종류는 설치 불가 (조작된 종류 값 방어).</summary>
        public bool IsPlaceable(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null && entry.Placeable;
        }

        /// <summary>
        /// 설치 시 크기를 끌어서 정하는 종류인지 (천막 계획 결정 ②) — 미등재·기존 종류는 false라
        /// 고정 발자국 경로가 그대로 돈다.
        /// </summary>
        public bool IsResizable(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null && entry.Resizable;
        }

        /// <summary>셀 1칸당 건축 비용 (결정 ⑤) — 0이면 크기와 무관한 고정 비용이다.</summary>
        public float GetCostPerCell(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.CostPerCell : 0f;
        }

        /// <summary>
        /// 체온 효과가 닿는 범위 (결정 ③) — 미등재·기존 종류는 <see cref="ShelterScope.Car"/>라
        /// 난방기·돔의 칸 단위 규약이 무수정으로 유지된다.
        /// </summary>
        public ShelterScope GetShelterScope(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.ShelterScope : ShelterScope.Car;
        }

        /// <summary>
        /// 그리드에서 실제로 막는 셀 모양 (결정 ⑥) — 미등재·기존 종류는
        /// <see cref="StructureOccupancy.Solid"/>라 기존 8종의 설치 판정이 바뀌지 않는다.
        /// </summary>
        public StructureOccupancy GetOccupancy(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.Occupancy : StructureOccupancy.Solid;
        }

        /// <summary>
        /// 공유 저장 블록을 갖는 종류인지 (2차 §2.8) — 설치 시 블록 할당, 철거·파괴 시 내용물 배출,
        /// 근접 시 패널 개방이 전부 이 플래그로 갈린다. 미등재 종류는 저장 없음.
        /// </summary>
        public bool ProvidesStorageBlock(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null && entry.ProvidesStorageBlock;
        }

        /// <summary>철거 반환 자원 종류 (2차 — 결정 ⑧). 미등재면 목재.</summary>
        public Game.Gameplay.Inventory.ResourceType GetRefundResource(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.RefundResource : Game.Gameplay.Inventory.ResourceType.Wood;
        }

        /// <summary>건축물 실물 프리팹 (계획서 §2.6) — 미등재·미지정이면 null.</summary>
        public GameObject GetViewPrefab(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.ViewPrefab : null;
        }

        /// <summary>
        /// 거치 무기 설정 (M7 4차 §2.1) — 미등재·미지정이면 null. 점유·조준·사격·장전 경로가
        /// 전부 "이 값이 있는가"로 갈린다: 기존 건축물 6종은 null이라 무수정으로 통과한다
        /// (난방 연료·저장 블록과 같은 소급 규약).
        /// </summary>
        public MountedWeaponSettings GetMountedWeapon(StructureKind kind)
        {
            Entry entry = Find(kind);
            return entry != null ? entry.MountedWeapon : null;
        }

        /// <summary>거치 무기 종류인가 — 설정 유무가 곧 판정이다 (§2.1).</summary>
        public bool IsMountedWeapon(StructureKind kind)
        {
            return GetMountedWeapon(kind) != null;
        }

        /// <summary>
        /// 설치 UI의 종류 순환(R 키) — 등재 순서 기준 다음 <b>설치 가능</b> 엔트리의 종류
        /// (설치 불가 플래그가 꺼진 종류(돔)는 건너뛴다 — 계획서 §2.2). 현재 종류가 미등재면 첫 설치 가능 엔트리.
        /// 설치 가능한 종류가 하나도 없으면 현재 값을 그대로 돌려준다.
        /// </summary>
        public StructureKind NextPlaceableKind(StructureKind current)
        {
            if (_entries == null || _entries.Length == 0)
            {
                return current;
            }

            int start = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].Kind == current)
                {
                    start = i + 1;
                    break;
                }
            }

            for (int step = 0; step < _entries.Length; step++)
            {
                Entry candidate = _entries[(start + step) % _entries.Length];
                if (candidate != null && candidate.Placeable)
                {
                    return candidate.Kind;
                }
            }

            return current;
        }

        private Entry Find(StructureKind kind)
        {
            if (_entries == null)
            {
                return null;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].Kind == kind)
                {
                    return _entries[i];
                }
            }

            return null;
        }
    }
}
