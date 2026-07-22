using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Harpoon;
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
        [SerializeField] private RevolverController _revolver;

        private PlayerInventory _inventory;
        private int _selectedIndex;
        private bool _panelOpen;

        public int SlotCount => _inventory != null ? _inventory.SlotCount : 0;

        public int StackSize => _inventory != null ? _inventory.StackSize : 1;

        public int SelectedIndex => _selectedIndex;

        /// <summary>현재 든 슬롯의 아이템 종류 — 엔진 좌클릭 투입 판정 등에 쓰인다.</summary>
        public HotbarItemType SelectedItemType => _inventory != null
            ? _inventory.GetSlot(_selectedIndex).ItemType
            : HotbarItemType.None;

        /// <summary>I 창이 열려 있는가 — 열려 있는 동안 무기·상호작용 입력이 정지된다.</summary>
        public bool IsPanelOpen => _panelOpen;

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

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            if (!_panelOpen)
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
            _selectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, SlotCount - 1));
            EventBus<HotbarSelectionChangedLocalEvent>.Publish(
                new HotbarSelectionChangedLocalEvent(_selectedIndex, SelectedItemType));
        }

        private void ApplyWeaponGates()
        {
            HotbarItemType selected = _panelOpen ? HotbarItemType.None : SelectedItemType;

            if (_harpoon != null)
            {
                _harpoon.InputEnabled = selected == HotbarItemType.Harpoon;
            }

            if (_revolver != null)
            {
                _revolver.InputEnabled = selected == HotbarItemType.Revolver;
            }
        }

        private void OnPanelToggled(InventoryPanelToggledLocalEvent evt)
        {
            _panelOpen = evt.IsOpen;
        }
    }
}
