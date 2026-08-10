using Game.Core.Services;
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
        [SerializeField] private InventorySettings _settings;
        [SerializeField] private ResourceCatalog _catalog;
        [SerializeField] private EquipmentCatalog _equipmentCatalog;

        private readonly NetworkList<NetworkSlot> _slots = new NetworkList<NetworkSlot>();

        // 착용 장비 — 인덱스 = EquipSlot 값 (기획서 §6.3, M5 3차). 핫바(_slots)와 분리해
        // "첫 빈 칸" 계열 규칙이 착용 칸을 넘보지 않게 한다.
        private readonly NetworkList<NetworkSlot> _equipment = new NetworkList<NetworkSlot>();

        private const int EquipmentSlotCount = 4;

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
                ServerInitializeEquipment();
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

            HotbarSlotView[] slots = CopySlots();
            bool crafted;
            if (recipe.IsItemOutput)
            {
                // 무기 산출 (M5 2차) — 스택 없이 빈 칸 1개에 지급.
                crafted = Crafting.CraftingLogic.TryCraftItem(
                    slots, recipe.ToIngredientViews(), recipe.OutputItem);
            }
            else
            {
                int outputStack = _catalog != null ? _catalog.GetMaxStack(recipe.Output, StackSize) : StackSize;
                crafted = Crafting.CraftingLogic.TryCraft(slots, recipe.ToIngredientViews(),
                    recipe.Output, recipe.OutputCount, outputStack);
            }

            if (!crafted)
            {
                return false;
            }

            ApplySlots(slots);
            return true;
        }

        /// <summary>
        /// 재료 소모만 확정한다 (M5 5차 집게 승급) — 산출이 인벤토리 밖에 있는 레시피용.
        /// 복사본에서 차감이 성립했을 때만 반영하므로, 호출부는 승급 확정과 이 호출을 묶어
        /// "둘 다 성공할 때만 반영"을 만들 수 있다 (재료 보존 원자 규약).
        /// </summary>
        public bool ServerTryConsume(Crafting.CraftingRecipe recipe)
        {
            if (!IsServer || recipe == null)
            {
                return false;
            }

            HotbarSlotView[] slots = CopySlots();
            if (!Crafting.CraftingLogic.TryConsume(slots, recipe.ToIngredientViews()))
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

        // ── 장비 착용 (기획서 §6.3, M5 3차) — 착용도 호스트 확정 ───────────────────

        /// <summary>착용 칸 조회 — 인덱스 = <see cref="EquipSlot"/> 값.</summary>
        public HotbarSlotView GetEquipmentSlot(int partIndex)
        {
            if (partIndex < 0 || partIndex >= _equipment.Count)
            {
                return new HotbarSlotView(HotbarItemType.None, 0);
            }

            NetworkSlot slot = _equipment[partIndex];
            return new HotbarSlotView(slot.ItemType, slot.Count, slot.Resource);
        }

        /// <summary>착용 부위 합산 피해 배율 (상한 반영) — 물리 피해 경로(PlayerHealth)가 곱한다. 1 = 감소 없음.</summary>
        public float GetEquippedDamageMultiplier()
        {
            if (_equipmentCatalog == null)
            {
                return 1f;
            }

            float total = 0f;
            for (int i = 0; i < _equipment.Count; i++)
            {
                total += _equipmentCatalog.GetDamageReduction(_equipment[i].ItemType);
            }

            return EquipmentLogic.GetDamageMultiplier(total, _equipmentCatalog.DamageReductionCap);
        }

        /// <summary>착용 부위 합산 단열 계수 (클램프 반영) — 체온 경로(PlayerTemperature)가 쓴다.</summary>
        public void GetEquippedInsulation(out float cold, out float heat)
        {
            cold = 0f;
            heat = 0f;
            if (_equipmentCatalog == null)
            {
                return;
            }

            for (int i = 0; i < _equipment.Count; i++)
            {
                cold += _equipmentCatalog.GetColdInsulation(_equipment[i].ItemType);
                heat += _equipmentCatalog.GetHeatInsulation(_equipment[i].ItemType);
            }

            cold = EquipmentLogic.ClampInsulation(cold);
            heat = EquipmentLogic.ClampInsulation(heat);
        }

        /// <summary>착용 부위 합산 평상 체온 상향 (℃, 클램프 반영) — 체온 경로(PlayerTemperature)가 곡선에 얹는다.</summary>
        public float GetEquippedBodyWarmth()
        {
            if (_equipmentCatalog == null)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 0; i < _equipment.Count; i++)
            {
                total += _equipmentCatalog.GetBodyWarmthBonus(_equipment[i].ItemType);
            }

            return EquipmentLogic.ClampBodyWarmth(total);
        }

        /// <summary>인벤토리 칸의 장비 착용 요청 (I 창 드래그) — 소유자에서 호출한다. 부위는 서버가 카탈로그로 판정.</summary>
        public void RequestEquip(int slotIndex)
        {
            if (IsOwner && slotIndex >= 0 && slotIndex < _slots.Count)
            {
                RequestEquipServerRpc(slotIndex);
            }
        }

        /// <summary>착용 해제 요청 — 첫 빈 인벤토리 칸으로 되돌린다 (빈 칸 없으면 실패 — 장비 보존).</summary>
        public void RequestUnequip(int partIndex)
        {
            if (IsOwner && partIndex >= 0 && partIndex < _equipment.Count)
            {
                RequestUnequipServerRpc(partIndex);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestEquipServerRpc(int slotIndex)
        {
            if (_equipmentCatalog == null)
            {
                return;
            }

            HotbarSlotView[] slots = CopySlots();
            HotbarSlotView[] equipment = CopyEquipment();
            if (!EquipmentLogic.TryEquip(slots, equipment, slotIndex, _equipmentCatalog.TryGetSlot))
            {
                return;
            }

            ApplySlots(slots);
            ApplyEquipment(equipment);
        }

        [Rpc(SendTo.Server)]
        private void RequestUnequipServerRpc(int partIndex)
        {
            HotbarSlotView[] slots = CopySlots();
            HotbarSlotView[] equipment = CopyEquipment();
            if (!EquipmentLogic.TryUnequip(equipment, partIndex, slots))
            {
                return;
            }

            ApplySlots(slots);
            ApplyEquipment(equipment);
        }

        private void ServerInitializeEquipment()
        {
            _equipment.Clear();
            for (int i = 0; i < EquipmentSlotCount; i++)
            {
                _equipment.Add(new NetworkSlot { ItemType = HotbarItemType.None, Count = 0 });
            }
        }

        private HotbarSlotView[] CopyEquipment()
        {
            var copy = new HotbarSlotView[_equipment.Count];
            for (int i = 0; i < _equipment.Count; i++)
            {
                copy[i] = new HotbarSlotView(_equipment[i].ItemType, _equipment[i].Count, _equipment[i].Resource);
            }

            return copy;
        }

        private void ApplyEquipment(HotbarSlotView[] equipment)
        {
            for (int i = 0; i < equipment.Length && i < _equipment.Count; i++)
            {
                var next = new NetworkSlot
                {
                    ItemType = equipment[i].ItemType,
                    Count = (byte)equipment[i].Count,
                    Resource = equipment[i].Resource,
                };
                if (!_equipment[i].Equals(next))
                {
                    _equipment[i] = next;
                }
            }
        }

        /// <summary>칸의 자원 스택 전량 버리기 요청 (I 창 패널 밖 드롭, M5 3차) — 소유자에서 호출한다.</summary>
        public void RequestDrop(int slotIndex)
        {
            if (IsOwner && slotIndex >= 0 && slotIndex < _slots.Count)
            {
                RequestDropServerRpc(slotIndex);
            }
        }

        /// <summary>
        /// 버리기 확정 — 자원 칸만 유효(무기·도구는 기각 — 처분은 공유 창고 보관).
        /// 차감과 낙하 스폰을 원자적으로 확정한다: 스폰이 성립했을 때만 복사본 차감을 반영한다.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestDropServerRpc(int slotIndex)
        {
            if (_settings == null || !ServiceLocator.TryGet(out World.IResourceDropper dropper))
            {
                return;
            }

            HotbarSlotView[] slots = CopySlots();
            if (!HotbarLogic.TryClearResourceSlot(slots, slotIndex, out ResourceType type, out int count))
            {
                return;
            }

            if (!dropper.ServerSpawnDropped(type, count, transform.position))
            {
                return;
            }

            ApplySlots(slots);
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
                else if (i == 3 && _settings.InitialRevolverAmmo > 0)
                {
                    // 시작 지급 권총탄 (M5) — 시작 직후 전투가 가능해야 탄약 루프가 압박이 아니라 관리가 된다.
                    slot.ItemType = HotbarItemType.Resource;
                    slot.Resource = ResourceType.RevolverAmmo;
                    slot.Count = (byte)Mathf.Min(byte.MaxValue, _settings.InitialRevolverAmmo);
                }

                _slots.Add(slot);
            }
        }

        private bool IsBuildMaterial(ResourceType type)
        {
            return _catalog != null && _catalog.IsBuildMaterial(type);
        }

        // ── 서버 협력 API — 공유 창고(TrainStorage)가 인벤토리↔창고 이동을 원자 확정할 때 쓴다 ──

        /// <summary>서버 전용 — 슬롯 복사본. 순수 로직 판정용, 반영은 <see cref="ServerApplySlotViews"/>.</summary>
        public HotbarSlotView[] ServerCopySlotViews()
        {
            return IsServer ? CopySlots() : System.Array.Empty<HotbarSlotView>();
        }

        /// <summary>서버 전용 — 판정을 마친 복사본을 권위 상태로 되쓴다 (변경 칸만).</summary>
        public void ServerApplySlotViews(HotbarSlotView[] slots)
        {
            if (IsServer)
            {
                ApplySlots(slots);
            }
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
