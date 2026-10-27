using System;
using System.Collections.Generic;
using Game.Gameplay.Inventory;
using Game.Gameplay.Train;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Explicit sprite references keep the complete UI set available in player builds.</summary>
    [CreateAssetMenu(menuName = "Train Survival/In-game UI Art")]
    public sealed class InGameUiArt : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Key;
            public Sprite Sprite;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        private Dictionary<string, Sprite> _lookup;
        public IReadOnlyList<Entry> Entries => _entries;

        public Sprite Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (Entry entry in _entries)
                    if (!string.IsNullOrEmpty(entry.Key)) _lookup[entry.Key] = entry.Sprite;
            }
            return _lookup.TryGetValue(key, out Sprite sprite) ? sprite : null;
        }

        public Sprite Item(HotbarSlotView slot) => slot.IsEmpty ? null : Find(ItemKey(slot));
        public Sprite Structure(StructureKind kind) => Find(StructureKey(kind));
        public static string StructureKey(StructureKind kind) => "I_Structure_" + kind;

        public static string ItemKey(HotbarSlotView slot) => slot.ItemType == HotbarItemType.Resource
            ? "I_Res_" + slot.Resource : "I_Item_" + slot.ItemType;

        // Equipment Count can contain storage identity (Bundle), so only resources show quantities.
        public static string QuantityLabel(HotbarSlotView slot) =>
            !slot.IsEmpty && slot.ItemType == HotbarItemType.Resource ? slot.Count.ToString() : string.Empty;

        private void OnEnable() => _lookup = null;
        private void OnValidate() => _lookup = null;
    }
}
