using System;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 건축물 종류별 정의 카탈로그 (M5 3차 — StructureState 종류화). 표시명·체력·비용과
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

            public StructureKind Kind => _kind;

            public string DisplayName => _displayName;

            public float MaxHealth => _maxHealth;

            public int BuildCost => _buildCost;

            public bool ProvidesShade => _providesShade;

            public bool ProvidesHeat => _providesHeat;
        }

        [Tooltip("종류별 정의 — Kind 값으로 식별하므로 배열 순서는 자유다(설치 UI의 순환 순서로만 쓰인다).")]
        [SerializeField] private Entry[] _entries;

        /// <summary>설치 UI가 순환할 수 있는 종류 수 — 등재된 엔트리 수.</summary>
        public int EntryCount => _entries != null ? _entries.Length : 0;

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
        /// 설치 UI의 종류 순환(R 키) — 등재 순서 기준 다음 엔트리의 종류. 현재 종류가 미등재면 첫 엔트리.
        /// </summary>
        public StructureKind NextKind(StructureKind current)
        {
            if (_entries == null || _entries.Length == 0)
            {
                return current;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].Kind == current)
                {
                    Entry next = _entries[(i + 1) % _entries.Length];
                    return next != null ? next.Kind : current;
                }
            }

            return _entries[0] != null ? _entries[0].Kind : current;
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
