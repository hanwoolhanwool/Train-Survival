using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 통합 핫바 인벤토리 — 호스트 권위 (기획서 §3.4, 네트워크 문서 §4: 개인 소유물이라도 증감·슬롯 이동 확정은 호스트).
    /// 무기와 자원이 한 핫바 5칸에 들어가며, 시작 배치는 1번 집게 · 2번 리볼버.
    /// 슬롯 목록은 NetworkList로 동기화하고, 규칙 판정은 순수 <see cref="HotbarLogic"/>이 담당한다.
    /// Player 프리팹에 부착한다.
    /// </summary>
    public sealed class PlayerInventory : NetworkBehaviour, IResourceInventory
    {
        /// <summary>핫바 슬롯의 네트워크 직렬화 표현 — 외부에는 <see cref="HotbarSlotView"/>로만 노출한다.</summary>
        public struct NetworkSlot : INetworkSerializable, System.IEquatable<NetworkSlot>
        {
            public HotbarItemType ItemType;
            public byte Count;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ItemType);
                serializer.SerializeValue(ref Count);
            }

            public bool Equals(NetworkSlot other)
            {
                return ItemType == other.ItemType && Count == other.Count;
            }
        }

        [SerializeField] private InventorySettings _settings;

        private readonly NetworkList<NetworkSlot> _slots = new NetworkList<NetworkSlot>();

        public int SlotCount => _settings != null ? _settings.SlotCount : 0;

        public int StackSize => _settings != null ? _settings.StackSize : 1;

        public int Count
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].ItemType == HotbarItemType.Resource)
                    {
                        total += _slots[i].Count;
                    }
                }

                return total;
            }
        }

        public int Capacity => HotbarLogic.ResourceCapacity(CopySlots(), StackSize);

        public bool IsFull => Count >= Capacity;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerInitializeSlots();
            }
        }

        public HotbarSlotView GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Count)
            {
                return new HotbarSlotView(HotbarItemType.None, 0);
            }

            NetworkSlot slot = _slots[index];
            return new HotbarSlotView(slot.ItemType, slot.Count);
        }

        // ── 호스트 권위: 증감·슬롯 이동 확정 ───────────────────────────────

        public bool ServerTryAdd(int amount)
        {
            if (!IsServer || amount <= 0 || _settings == null)
            {
                return false;
            }

            HotbarSlotView[] slots = CopySlots();
            for (int i = 0; i < amount; i++)
            {
                if (!HotbarLogic.TryAddResource(slots, StackSize))
                {
                    return false;
                }
            }

            ApplySlots(slots);
            return true;
        }

        public bool ServerTryRemove(int amount)
        {
            if (!IsServer || amount <= 0)
            {
                return false;
            }

            HotbarSlotView[] slots = CopySlots();
            for (int i = 0; i < amount; i++)
            {
                if (!HotbarLogic.TryRemoveResource(slots))
                {
                    return false;
                }
            }

            ApplySlots(slots);
            return true;
        }

        /// <summary>슬롯 교환 요청 (자유 배치, I 창 드래그) — 소유자에서 호출한다.</summary>
        public void RequestSwap(int a, int b)
        {
            if (IsOwner && HotbarLogic.IsValidSwap(a, b, _slots.Count))
            {
                RequestSwapServerRpc(a, b);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestSwapServerRpc(int a, int b)
        {
            if (!HotbarLogic.IsValidSwap(a, b, _slots.Count))
            {
                return;
            }

            NetworkSlot temp = _slots[a];
            _slots[a] = _slots[b];
            _slots[b] = temp;
        }

        private void ServerInitializeSlots()
        {
            _slots.Clear();
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = new NetworkSlot { ItemType = HotbarItemType.None, Count = 0 };
                if (i == 0)
                {
                    slot.ItemType = HotbarItemType.Harpoon;
                    slot.Count = 1;
                }
                else if (i == 1)
                {
                    slot.ItemType = HotbarItemType.Revolver;
                    slot.Count = 1;
                }

                _slots.Add(slot);
            }
        }

        private HotbarSlotView[] CopySlots()
        {
            var copy = new HotbarSlotView[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                copy[i] = new HotbarSlotView(_slots[i].ItemType, _slots[i].Count);
            }

            return copy;
        }

        private void ApplySlots(HotbarSlotView[] slots)
        {
            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var next = new NetworkSlot { ItemType = slots[i].ItemType, Count = (byte)slots[i].Count };
                if (!_slots[i].Equals(next))
                {
                    _slots[i] = next;
                }
            }
        }
    }
}
