using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 통합 핫바 인벤토리 — 호스트 권위 (기획서 §3.4, 네트워크 문서 §4: 개인 소유물이라도 증감·슬롯 이동 확정은 호스트).
    /// 무기와 자원이 한 핫바 5칸에 들어가며, 시작 배치는 1번 집게 · 2번 리볼버 · 3번 수리 망치.
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
            public ResourceType Resource;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ItemType);
                serializer.SerializeValue(ref Count);
                serializer.SerializeValue(ref Resource);
            }

            public bool Equals(NetworkSlot other)
            {
                return ItemType == other.ItemType && Count == other.Count && Resource == other.Resource;
            }
        }

        [SerializeField] private InventorySettings _settings;
        [SerializeField] private ResourceCatalog _catalog;

        private readonly NetworkList<NetworkSlot> _slots = new NetworkList<NetworkSlot>();

        public int SlotCount => _settings != null ? _settings.SlotCount : 0;

        /// <summary>앞쪽 핫바 칸 수 — 숫자 키 1~5로 드는 칸 (나머지는 보관 가방).</summary>
        public int HotbarSize => _settings != null ? _settings.HotbarSize : 0;

        public int StackSize => _settings != null ? _settings.StackSize : 1;

        /// <summary>건자재 소지 총량 — 건설 비용 검증·HUD 표시용. 탄약 등 비건자재는 세지 않는다.</summary>
        public int Count
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].ItemType == HotbarItemType.Resource && IsBuildMaterial(_slots[i].Resource))
                    {
                        total += _slots[i].Count;
                    }
                }

                return total;
            }
        }

        public int Capacity => HotbarLogic.ResourceCapacity(CopySlots(), StackSize);

        public bool IsFull => Count >= Capacity;

        public int CountOf(ResourceType type)
        {
            return HotbarLogic.CountResource(CopySlots(), type);
        }

        public ResourceType GetResourceTypeAt(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count ||
                _slots[slotIndex].ItemType != HotbarItemType.Resource)
            {
                return ResourceType.None;
            }

            return _slots[slotIndex].Resource;
        }

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
            return new HotbarSlotView(slot.ItemType, slot.Count, slot.Resource);
        }

        // ── 호스트 권위: 증감·슬롯 이동 확정 ───────────────────────────────

        public bool ServerTryAdd(ResourceType type, int amount)
        {
            if (!IsServer || amount <= 0 || type == ResourceType.None || _settings == null)
            {
                return false;
            }

            int stackSize = _catalog != null ? _catalog.GetMaxStack(type, StackSize) : StackSize;
            HotbarSlotView[] slots = CopySlots();
            for (int i = 0; i < amount; i++)
            {
                if (!HotbarLogic.TryAddResource(slots, type, stackSize))
                {
                    return false;
                }
            }

            ApplySlots(slots);
            return true;
        }

        public bool ServerTryRemove(ResourceType type, int amount)
        {
            if (!IsServer || amount <= 0)
            {
                return false;
            }

            HotbarSlotView[] slots = CopySlots();
            for (int i = 0; i < amount; i++)
            {
                if (!HotbarLogic.TryRemoveResource(slots, type))
                {
                    return false;
                }
            }

            ApplySlots(slots);
            return true;
        }

        public bool ServerTrySpend(int amount, System.Func<bool> confirm)
        {
            if (!IsServer || amount < 0 || confirm == null)
            {
                return false;
            }

            // 복사본에서 건자재를 차감해 보고, 소비처(confirm)까지 성공했을 때만 반영한다 — 수동 롤백 불요.
            HotbarSlotView[] slots = CopySlots();
            for (int i = 0; i < amount; i++)
            {
                if (!HotbarLogic.TryRemoveAnyResource(slots, IsBuildMaterial))
                {
                    return false;
                }
            }

            if (!confirm())
            {
                return false;
            }

            ApplySlots(slots);
            return true;
        }

        public bool ServerTryRemoveAt(int slotIndex, int amount)
        {
            if (!IsServer || amount <= 0)
            {
                return false;
            }

            HotbarSlotView[] slots = CopySlots();
            for (int i = 0; i < amount; i++)
            {
                if (!HotbarLogic.TryRemoveResourceAt(slots, slotIndex))
                {
                    return false;
                }
            }

            ApplySlots(slots);
            return true;
        }

        /// <summary>
        /// 제작 확정 — 레시피의 재료 차감 + 산출 지급을 복사본 위에서 수행하고 성공 시에만 반영한다 (원자).
        /// 서버 전용 — 요청 검증(거리·레시피 유효성)은 호출부(CraftingStation)가 마친 상태다.
        /// </summary>
        public bool ServerTryCraft(Crafting.CraftingRecipe recipe)
        {
            if (!IsServer || recipe == null || _settings == null)
            {
                return false;
            }

            int outputStack = _catalog != null ? _catalog.GetMaxStack(recipe.Output, StackSize) : StackSize;
            HotbarSlotView[] slots = CopySlots();
            if (!Crafting.CraftingLogic.TryCraft(slots, recipe.ToIngredientViews(),
                recipe.Output, recipe.OutputCount, outputStack))
            {
                return false;
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
                else if (i == 2)
                {
                    slot.ItemType = HotbarItemType.Hammer;
                    slot.Count = 1;
                }

                _slots.Add(slot);
            }
        }

        private bool IsBuildMaterial(ResourceType type)
        {
            return _catalog != null && _catalog.IsBuildMaterial(type);
        }

        private HotbarSlotView[] CopySlots()
        {
            var copy = new HotbarSlotView[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                copy[i] = new HotbarSlotView(_slots[i].ItemType, _slots[i].Count, _slots[i].Resource);
            }

            return copy;
        }

        private void ApplySlots(HotbarSlotView[] slots)
        {
            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var next = new NetworkSlot
                {
                    ItemType = slots[i].ItemType,
                    Count = (byte)slots[i].Count,
                    Resource = slots[i].Resource,
                };
                if (!_slots[i].Equals(next))
                {
                    _slots[i] = next;
                }
            }
        }
    }
}
