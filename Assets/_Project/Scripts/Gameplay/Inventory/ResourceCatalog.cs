using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 자원 종류별 데이터 카탈로그 (기획서 §4 지역별 자원) — 표시명·스택 상한·엔진 발열량·건자재 여부.
    /// <see cref="ResourceType"/>은 네트워크 식별자만 담당하고, 밸런스·표시는 전부 이 에셋이 담당한다
    /// (수치 하드코딩 금지 — 개발 가이드 §3). 미등재 종류는 폴백 값으로 동작한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceCatalog", menuName = "Game/Resource Catalog")]
    public sealed class ResourceCatalog : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            [SerializeField] private ResourceType _type;
            [SerializeField] private string _displayName;

            [Tooltip("이 종류의 슬롯당 스택 상한. 0 이하면 InventorySettings의 기본 스택을 쓴다.")]
            [SerializeField, Min(0)] private int _maxStack;

            [Tooltip("엔진 투입 1개당 연료 충전량. 0 이하면 투입 불가 (화약 원료·탄약).")]
            [SerializeField, Min(0f)] private float _fuelValue;

            [Tooltip("건설·수리 비용(\"건자재 아무거나 N개\")으로 소모될 수 있는지.")]
            [SerializeField] private bool _isBuildMaterial;

            [Tooltip("지상 노드·아이콘에 쓰는 종류 식별 색 (전 종류가 한 프리팹을 공유하므로 색이 곧 외형 구분이다).")]
            [SerializeField] private Color _color = Color.white;

            [Tooltip("요구 집게 등급 — 이 값 이상의 집게 등급이어야 낚아챌 수 있다. 0·미등재 = 1.")]
            [FormerlySerializedAs("_grabWeight")]
            [SerializeField, Range(0, 3)] private int _requiredHarpoonTier = 1;

            public ResourceType Type => _type;

            public string DisplayName => _displayName;

            public int MaxStack => _maxStack;

            public float FuelValue => _fuelValue;

            public bool IsBuildMaterial => _isBuildMaterial;

            public Color Color => _color;

            /// <summary>요구 집게 등급 — 0 이하로 두면 1(기본)로 본다.</summary>
            public int RequiredHarpoonTier => _requiredHarpoonTier <= 0 ? 1 : _requiredHarpoonTier;
        }

        [SerializeField] private Entry[] _entries;

        private Entry[] _lookup;
        private string _buildMaterialNames;

        /// <summary>종류의 표시명. 미등재면 종류 이름 그대로.</summary>
        public string GetDisplayName(ResourceType type)
        {
            Entry entry = Find(type);
            return entry == null || string.IsNullOrEmpty(entry.DisplayName) ? type.ToString() : entry.DisplayName;
        }

        /// <summary>종류의 슬롯당 스택 상한. 미등재이거나 0 이하면 fallback.</summary>
        public int GetMaxStack(ResourceType type, int fallback)
        {
            Entry entry = Find(type);
            return entry == null || entry.MaxStack <= 0 ? fallback : entry.MaxStack;
        }

        /// <summary>종류의 엔진 발열량. 0 이하 = 투입 불가.</summary>
        public float GetFuelValue(ResourceType type)
        {
            Entry entry = Find(type);
            return entry == null ? 0f : entry.FuelValue;
        }

        /// <summary>건설 비용으로 소모될 수 있는 종류인지.</summary>
        public bool IsBuildMaterial(ResourceType type)
        {
            Entry entry = Find(type);
            return entry != null && entry.IsBuildMaterial;
        }

        /// <summary>종류 식별 색. 미등재면 fallback.</summary>
        public Color GetColor(ResourceType type, Color fallback)
        {
            Entry entry = Find(type);
            return entry == null ? fallback : entry.Color;
        }

        /// <summary>
        /// 종류의 요구 집게 등급 (M5 5차). <b>미등재는 1</b> — 기존 종류가 전부 1단계 집게로
        /// 그대로 채집되도록 하는 폴백이다 (회귀 없음).
        /// </summary>
        public int GetRequiredHarpoonTier(ResourceType type)
        {
            Entry entry = Find(type);
            return entry == null ? 1 : entry.RequiredHarpoonTier;
        }

        /// <summary>건자재로 인정되는 종류들의 표시명 나열 ("목재·돌·고철") — 비용 부족 안내용.</summary>
        public string GetBuildMaterialNames()
        {
            if (_buildMaterialNames != null)
            {
                return _buildMaterialNames;
            }

            var builder = new System.Text.StringBuilder(32);
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] == null || !_entries[i].IsBuildMaterial)
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append("·");
                    }

                    builder.Append(GetDisplayName(_entries[i].Type));
                }
            }

            _buildMaterialNames = builder.ToString();
            return _buildMaterialNames;
        }

        private Entry Find(ResourceType type)
        {
            EnsureLookup();

            int index = (int)type;
            return index < 0 || index >= _lookup.Length ? null : _lookup[index];
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
            {
                return;
            }

            int maxValue = 0;
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null && (int)_entries[i].Type > maxValue)
                    {
                        maxValue = (int)_entries[i].Type;
                    }
                }
            }

            _lookup = new Entry[maxValue + 1];
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i] != null)
                    {
                        _lookup[(int)_entries[i].Type] = _entries[i];
                    }
                }
            }
        }

        private void OnValidate()
        {
            _lookup = null;
            _buildMaterialNames = null;
        }
    }
}
