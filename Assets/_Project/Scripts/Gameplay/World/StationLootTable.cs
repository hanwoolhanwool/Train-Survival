using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 역 소품 한 종류의 전리품 저작 데이터
    /// ([기차역 이벤트 구현 계획](docs/plans/features/기차역-이벤트-구현-계획.md) §4.3).
    ///
    /// <para><b>슬롯 단위로 담는다.</b> 상자·금고·자판기는 전부 <see cref="StorageBundle"/>로
    /// 심기므로 내용물 표현이 창고 슬롯과 같다 — 그래서 무기·장비도 그대로 들어간다.
    /// 쓰레기통(<see cref="StationPropKind.Bin"/>)만 <see cref="ResourceNode"/>로 심기는데,
    /// 그때는 <b>첫 슬롯의 자원 종류</b>만 쓴다.</para>
    ///
    /// <para><b>요구 집게 등급은 여기 없다.</b> <see cref="StationLootLogic.RequiredTierFor"/>가
    /// 종류에서 규칙으로 정한다 — 금고가 3단계인 것이 성장 축이라 저작 실수로 흔들리면 안 된다.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StationLootTable", menuName = "Game/Station Loot Table")]
    public sealed class StationLootTable : ScriptableObject
    {
        /// <summary>전리품 후보 한 줄 — 무엇이 몇 개.</summary>
        [System.Serializable]
        public sealed class Entry
        {
            [Tooltip("담길 아이템 종류. Resource면 아래 자원 종류를 함께 본다.")]
            [SerializeField] private HotbarItemType _itemType = HotbarItemType.Resource;

            [Tooltip("ItemType이 Resource일 때의 자원 종류.")]
            [SerializeField] private ResourceType _resource = ResourceType.Scrap;

            [Tooltip("수량 범위 — 자원이 아닌 것(무기·장비)은 1로 둔다.")]
            [SerializeField, Min(1)] private int _minCount = 1;

            [SerializeField, Min(1)] private int _maxCount = 1;

            [Tooltip("추첨 가중치. 0이면 뽑히지 않는다 — 작업 중인 줄을 비워 둘 수 있다.")]
            [SerializeField, Min(0f)] private float _weight = 1f;

            public HotbarItemType ItemType => _itemType;

            public ResourceType Resource => _resource;

            public int MinCount => _minCount;

            public int MaxCount => _maxCount;

            public float Weight => _weight;
        }

        [Tooltip("이 표가 담당하는 소품 종류.")]
        [SerializeField] private StationPropKind _kind = StationPropKind.Crate;

        [Tooltip("채울 슬롯 수의 최소·최대. 쓰레기통은 1로 두면 된다 (첫 슬롯만 쓴다).")]
        [SerializeField, Min(1)] private int _minSlots = 1;

        [SerializeField, Min(1)] private int _maxSlots = 3;

        [SerializeField] private Entry[] _entries;

        // 추첨마다 배열을 새로 만들지 않도록 캐시한다 (TerrainSegmentPalette와 같은 규약).
        private float[] _weights;

        public StationPropKind Kind => _kind;

        public int MinSlots => _minSlots;

        public int MaxSlots => _maxSlots;

        public int Count => _entries == null ? 0 : _entries.Length;

        /// <summary>뽑을 것이 하나라도 있는가 — 없으면 스포너가 이 종류를 조용히 건너뛴다.</summary>
        public bool HasAnyEntry
        {
            get
            {
                float[] weights = GetWeights();
                if (weights == null)
                {
                    return false;
                }

                for (int i = 0; i < weights.Length; i++)
                {
                    if (weights[i] > 0f)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public Entry GetEntry(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Length)
            {
                return null;
            }

            return _entries[index];
        }

        /// <summary>가중치 배열 (캐시). 비어 있으면 null.</summary>
        public float[] GetWeights()
        {
            int count = Count;
            if (count == 0)
            {
                return null;
            }

            if (_weights == null || _weights.Length != count)
            {
                _weights = new float[count];
                for (int i = 0; i < count; i++)
                {
                    Entry entry = _entries[i];
                    _weights[i] = entry == null ? 0f : Mathf.Max(0f, entry.Weight);
                }
            }

            return _weights;
        }

        private void OnValidate()
        {
            if (_maxSlots < _minSlots)
            {
                _maxSlots = _minSlots;
            }

            _weights = null;
        }
    }
}
