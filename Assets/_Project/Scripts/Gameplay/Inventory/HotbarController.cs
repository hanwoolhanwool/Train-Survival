using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Harpoon;
using Game.Gameplay.Player;
using Game.Gameplay.Train;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 통합 핫바 선택·입력 게이트 (기획서 §3.4) — 숫자 키 1~5 = 슬롯 선택(든 것이 바뀜).
    /// 선택은 소유자 로컬 결정이며, 든 슬롯의 아이템 종류에 따라 무기 입력 게이트를 연다.
    /// I 창이 열려 있는 동안은 모든 무기 입력을 닫는다. 소유자 스폰 시 <see cref="ILocalHotbar"/>로 등록된다.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class HotbarController : NetworkBehaviour, ILocalHotbar
    {
        [SerializeField] private HarpoonController _harpoon;
        [SerializeField] private RepairHammerController _hammer;

        [Tooltip("이 플레이어의 총기들 (리볼버·샷건·볼트액션) — 각자 세팅의 WeaponItem으로 게이트가 열린다.")]
        [SerializeField] private GunController[] _guns;

        [SerializeField] private MeleeWeaponController _melee;

        private PlayerInventory _inventory;
        private int _selectedIndex;
        private bool _panelOpen;
        private bool _sessionMenuOpen;
        private bool _craftingOpen;
        private bool _storageOpen;
        private bool _bundleOpen;

        public int SlotCount => _inventory != null ? _inventory.SlotCount : 0;

        public int HotbarSize => _inventory != null ? _inventory.HotbarSize : 0;

        public int StackSize => _inventory != null ? _inventory.StackSize : 1;

        public int SelectedIndex => _selectedIndex;

        /// <summary>현재 든 슬롯의 아이템 종류 — 엔진 좌클릭 투입 판정 등에 쓰인다.</summary>
        public HotbarItemType SelectedItemType => _inventory != null
            ? _inventory.GetSlot(_selectedIndex).ItemType
            : HotbarItemType.None;

        /// <summary>현재 든 슬롯의 자원 종류 — 엔진 투입 가능(발열량) 판정에 쓰인다. 자원 칸이 아니면 None.</summary>
        public ResourceType SelectedResourceType => _inventory != null
            ? _inventory.GetSlot(_selectedIndex).Resource
            : ResourceType.None;

        /// <summary>UI(I 창·세션 메뉴·제작 창·창고 창·보따리 창)가 열려 있는가 — 열려 있는 동안 무기·상호작용 입력이 정지된다.</summary>
        public bool IsPanelOpen => _panelOpen || _sessionMenuOpen || _craftingOpen || _storageOpen || _bundleOpen;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            EventBus<InventoryPanelToggledLocalEvent>.Subscribe(OnPanelToggled);
            EventBus<SessionMenuToggledLocalEvent>.Subscribe(OnSessionMenuToggled);
            EventBus<Crafting.CraftingPanelToggledLocalEvent>.Subscribe(OnCraftingPanelToggled);
            EventBus<Train.StoragePanelToggledLocalEvent>.Subscribe(OnStoragePanelToggled);
            EventBus<Train.BundlePanelToggledLocalEvent>.Subscribe(OnBundlePanelToggled);

            if (!ServiceLocator.IsRegistered<ILocalHotbar>())
            {
                ServiceLocator.Register<ILocalHotbar>(this);
            }

            Select(0);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            EventBus<InventoryPanelToggledLocalEvent>.Unsubscribe(OnPanelToggled);
            EventBus<SessionMenuToggledLocalEvent>.Unsubscribe(OnSessionMenuToggled);
            EventBus<Crafting.CraftingPanelToggledLocalEvent>.Unsubscribe(OnCraftingPanelToggled);
            EventBus<Train.StoragePanelToggledLocalEvent>.Unsubscribe(OnStoragePanelToggled);
            EventBus<Train.BundlePanelToggledLocalEvent>.Unsubscribe(OnBundlePanelToggled);

            if (ServiceLocator.TryGet(out ILocalHotbar hotbar) && ReferenceEquals(hotbar, this))
            {
                ServiceLocator.Unregister<ILocalHotbar>();
            }
        }

        public HotbarSlotView GetSlot(int index)
        {
            return _inventory != null
                ? _inventory.GetSlot(index)
                : new HotbarSlotView(HotbarItemType.None, 0);
        }

        public void RequestSwap(int a, int b)
        {
            _inventory?.RequestSwap(a, b);
        }

        public void RequestDrop(int slotIndex, int amount)
        {
            _inventory?.RequestDrop(slotIndex, amount);
        }

        public HotbarSlotView GetEquipmentSlot(int partIndex)
        {
            return _inventory != null
                ? _inventory.GetEquipmentSlot(partIndex)
                : new HotbarSlotView(HotbarItemType.None, 0);
        }

        public void RequestEquip(int slotIndex)
        {
            _inventory?.RequestEquip(slotIndex);
        }

        public void RequestUnequip(int partIndex)
        {
            _inventory?.RequestUnequip(partIndex);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            if (!IsPanelOpen)
            {
                UpdateSelectionInput();
            }

            // 슬롯 내용은 드래그 재배치·자원 증감으로 수시로 바뀌므로 게이트는 매 프레임 갱신한다.
            ApplyWeaponGates();
        }

        private void UpdateSelectionInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Select(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                Select(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                Select(2);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame)
            {
                Select(3);
            }
            else if (keyboard.digit5Key.wasPressedThisFrame)
            {
                Select(4);
            }
        }

        private void Select(int index)
        {
            // 선택은 핫바 칸(1~5)에 한정한다 — 가방 칸은 I 창 드래그로만 다룬다.
            _selectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, HotbarSize - 1));
            EventBus<HotbarSelectionChangedLocalEvent>.Publish(
                new HotbarSelectionChangedLocalEvent(_selectedIndex, SelectedItemType));
        }

        private void ApplyWeaponGates()
        {
            HotbarItemType selected = IsPanelOpen ? HotbarItemType.None : SelectedItemType;

            if (_harpoon != null)
            {
                _harpoon.InputEnabled = selected == HotbarItemType.Harpoon;
            }

            if (_hammer != null)
            {
                _hammer.InputEnabled = selected == HotbarItemType.Hammer;
            }

            if (_melee != null)
            {
                _melee.InputEnabled = selected == HotbarItemType.Melee;
            }

            if (_guns != null)
            {
                for (int i = 0; i < _guns.Length; i++)
                {
                    if (_guns[i] != null)
                    {
                        _guns[i].InputEnabled = selected == _guns[i].WeaponItem;
                    }
                }
            }
        }

        private void OnPanelToggled(InventoryPanelToggledLocalEvent evt)
        {
            _panelOpen = evt.IsOpen;
        }

        private void OnSessionMenuToggled(SessionMenuToggledLocalEvent evt)
        {
            _sessionMenuOpen = evt.IsOpen;
        }

        private void OnCraftingPanelToggled(Crafting.CraftingPanelToggledLocalEvent evt)
        {
            _craftingOpen = evt.IsOpen;
        }

        private void OnStoragePanelToggled(Train.StoragePanelToggledLocalEvent evt)
        {
            _storageOpen = evt.IsOpen;
        }

        private void OnBundlePanelToggled(Train.BundlePanelToggledLocalEvent evt)
        {
            _bundleOpen = evt.IsOpen;
        }
    }
}
