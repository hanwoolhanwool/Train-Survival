using System;
using UnityEngine;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 장비 정의 카탈로그 (기획서 §6.3 — 의류/방어구, M5 3차). 부위·피해 감소·체온 단열을
    /// 데이터로 분리한다. 단열 계수는 양방향이다: Cold = 추위 차단, Heat = 더위 차단,
    /// <b>음수 = 역효과</b> (보온복이 사막 낮에 더위를 키우는 트레이드오프 — §6.3 가죽 옷).
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentCatalog", menuName = "Game/Equipment Catalog")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private HotbarItemType _item;

            [SerializeField] private EquipSlot _slot;

            [Tooltip("물리 피해 감소 비율 (0~1) — 착용 부위 합산에 상한(cap)이 걸린다.")]
            [SerializeField, Range(0f, 1f)] private float _damageReduction;

            [Tooltip("추위 단열 (−1~1) — 양수면 추운 환경 온도를 쾌적 하한 쪽으로 당긴다.")]
            [SerializeField, Range(-1f, 1f)] private float _coldInsulation;

            [Tooltip("더위 단열 (−1~1) — 양수면 더운 환경 온도를 쾌적 상한 쪽으로 당긴다. 음수 = 역효과.")]
            [SerializeField, Range(-1f, 1f)] private float _heatInsulation;

            [Tooltip("착용 시 평상 체온 상향 (℃, M5 7차) — 쾌적 환경에서의 수렴 체온을 밀어 올린다. " +
                "단열이 '환경을 덜 춥게'라면 이 축은 '몸 자체가 따뜻하게'다.")]
            [SerializeField, Min(0f)] private float _bodyWarmthBonus;

            public HotbarItemType Item => _item;

            public EquipSlot Slot => _slot;

            public float DamageReduction => _damageReduction;

            public float ColdInsulation => _coldInsulation;

            public float HeatInsulation => _heatInsulation;

            public float BodyWarmthBonus => _bodyWarmthBonus;
        }

        [SerializeField] private Entry[] _entries;

        [Tooltip("착용 부위 합산 피해 감소의 상한 — 풀셋으로도 무적이 되지 않게 한다.")]
        [SerializeField, Range(0f, 1f)] private float _damageReductionCap = 0.6f;

        /// <summary>착용 부위 합산 피해 감소 상한.</summary>
        public float DamageReductionCap => _damageReductionCap;

        /// <summary>이 아이템이 장비인지 — 장비면 부위를 돌려준다.</summary>
        public bool TryGetSlot(HotbarItemType item, out EquipSlot slot)
        {
            Entry entry = Find(item);
            slot = entry != null ? entry.Slot : default;
            return entry != null;
        }

        public float GetDamageReduction(HotbarItemType item)
        {
            Entry entry = Find(item);
            return entry != null ? entry.DamageReduction : 0f;
        }

        public float GetColdInsulation(HotbarItemType item)
        {
            Entry entry = Find(item);
            return entry != null ? entry.ColdInsulation : 0f;
        }

        public float GetHeatInsulation(HotbarItemType item)
        {
            Entry entry = Find(item);
            return entry != null ? entry.HeatInsulation : 0f;
        }

        public float GetBodyWarmthBonus(HotbarItemType item)
        {
            Entry entry = Find(item);
            return entry != null ? entry.BodyWarmthBonus : 0f;
        }

        private Entry Find(HotbarItemType item)
        {
            if (_entries == null || item == HotbarItemType.None || item == HotbarItemType.Resource)
            {
                return null;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].Item == item)
                {
                    return _entries[i];
                }
            }

            return null;
        }
    }
}
