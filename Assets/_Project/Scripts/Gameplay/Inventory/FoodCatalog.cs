using System;
using UnityEngine;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 음식 정의 카탈로그 (기획서 §7.3 요리, M5 4차). 요리별 허기 회복량과 시간제 버프
    /// (재생·보온)를 데이터로 분리한다 — 한 요리가 두 버프를 함께 가질 수 있다
    /// (스튜 = 소량 재생 + 보온). 섭취 확정·버프 적용은 호스트 소관.
    /// </summary>
    [CreateAssetMenu(fileName = "FoodCatalog", menuName = "Game/Food Catalog")]
    public sealed class FoodCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private ResourceType _type;

            [Tooltip("섭취 1회의 허기 회복량.")]
            [SerializeField, Min(0f)] private float _hungerRestore = 30f;

            [Header("재생 버프 (지속 0 = 없음)")]
            [SerializeField, Min(0f)] private float _regenPerSecond;
            [SerializeField, Min(0f)] private float _regenDurationSeconds;

            [Header("보온 버프 (지속 0 = 없음)")]
            [Tooltip("추위 단열 가산 — 장비 단열 합산에 더해지고 같은 클램프[−1, 0.9]를 통과한다.")]
            [SerializeField, Range(0f, 1f)] private float _coldInsulationBonus;
            [SerializeField, Min(0f)] private float _warmthDurationSeconds;

            public ResourceType Type => _type;

            public float HungerRestore => _hungerRestore;

            public float RegenPerSecond => _regenPerSecond;

            public float RegenDurationSeconds => _regenDurationSeconds;

            public float ColdInsulationBonus => _coldInsulationBonus;

            public float WarmthDurationSeconds => _warmthDurationSeconds;
        }

        [SerializeField] private Entry[] _entries;

        /// <summary>이 자원이 먹을 수 있는 음식인지 — 음식이면 정의를 돌려준다.</summary>
        public bool TryGetEntry(ResourceType type, out Entry entry)
        {
            entry = null;
            if (_entries == null || type == ResourceType.None)
            {
                return false;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].Type == type)
                {
                    entry = _entries[i];
                    return true;
                }
            }

            return false;
        }
    }
}
