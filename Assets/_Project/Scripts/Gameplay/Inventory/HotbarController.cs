using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Harpoon;
using Game.Gameplay.Player;
using Game.Gameplay.Train;
using Game.Systems.Networking;
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

        [Tooltip("무기 손 점유 판정 데이터 — 그랩 유지 중 전환 게이트가 '양손인가'를 여기서만 읽는다. " +
            "비어 있으면 전부 한손으로 본다 (게이트는 1단계 거부만 남는다).")]
        [SerializeField] private WeaponHandednessSettings _handedness;

        [Tooltip("이 플레이어의 총기들 (리볼버·샷건·볼트액션) — 각자 세팅의 WeaponItem으로 게이트가 열린다.")]
        [SerializeField] private GunController[] _guns;

        [SerializeField] private MeleeWeaponController _melee;

        [Tooltip("낚싯대 (바다 3차) — 비면 낚시가 없다.")]
        [SerializeField] private World.FishingRodController _fishingRod;

        private PlayerInventory _inventory;
        private int _selectedIndex;
        private bool _panelOpen;
        private bool _sessionMenuOpen;
        private bool _craftingOpen;
        private bool _storageOpen;
        private bool _bundleOpen;

        // 거치 무기 점유 (M7 4차 §2.3) — 붙어 있는 동안 핫바 선택·무기 입력이 전부 닫힌다.
        // 창 열림과 같은 축에 얹는 이유: 소비자가 "입력이 잠겼는가"를 한 곳에서만 읽게 된다.
        private bool _mounted;

        // 거부 문구 반복 억제 (§3.6) — 한 번의 그랩 동안 문구는 첫 회만. 그랩이 풀리면 다시 알린다.
        private bool _switchRejectAnnounced;

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
        public bool IsPanelOpen => _panelOpen || _sessionMenuOpen || _craftingOpen || _storageOpen
            || _bundleOpen || _mounted;

        /// <summary>
        /// 제작 창 열기(E)를 막아야 할 다른 UI가 열려 있는가 (M7 3차 검증 개선).
        /// <b>인벤토리 창은 제외한다</b> — 제작 창과 인벤토리는 함께 열리는 짝이라 서로를 막지 않는다.
        /// 제작 창 자신도 제외한다 (E는 토글이다).
        /// </summary>
        public bool IsCraftBlockingPanelOpen => _sessionMenuOpen || _storageOpen || _bundleOpen || _mounted;

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
            EventBus<Train.MountStateChangedLocalEvent>.Subscribe(OnMountStateChanged);

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
            EventBus<Train.MountStateChangedLocalEvent>.Unsubscribe(OnMountStateChanged);

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
            int next = Mathf.Clamp(index, 0, Mathf.Max(0, HotbarSize - 1));

            if (!TryPassGrabGate(next))
            {
                return;
            }

            _selectedIndex = next;
            EventBus<HotbarSelectionChangedLocalEvent>.Publish(
                new HotbarSelectionChangedLocalEvent(_selectedIndex, SelectedItemType));
        }

        /// <summary>
        /// 그랩 유지 중 전환 게이트 (집게 단계별 파지 계획 §3.2) — 판정은
        /// <see cref="HarpoonSwitchRules"/>가 하고, 여기서는 결과를 조작에 옮기기만 한다.
        /// 1단계는 손이 묶여 전환이 막히고, 2·3단계가 양손 무기를 고르면 먼저 놓고 넘어간다.
        /// </summary>
        private bool TryPassGrabGate(int index)
        {
            if (_harpoon == null)
            {
                return true;
            }

            HotbarItemType target = _inventory != null
                ? _inventory.GetSlot(index).ItemType
                : HotbarItemType.None;
            bool twoHanded = _handedness != null && _handedness.IsTwoHanded(target);

            switch (HarpoonSwitchRules.Evaluate(_harpoon.State, _harpoon.Tier, twoHanded))
            {
                case SwitchOutcome.Deny:
                    // 문구는 같은 그랩의 첫 회만 — 연타해도 토스트가 쌓이지 않는다 (확정 ⑥).
                    EventBus<HotbarSelectionRejectedLocalEvent>.Publish(
                        new HotbarSelectionRejectedLocalEvent(
                            index, HotbarSwitchRejectReason.HarpoonTier1HandsFull, !_switchRejectAnnounced));
                    _switchRejectAnnounced = true;
                    return false;

                case SwitchOutcome.ReleaseThenAllow:
                    // 잡았던 대상은 그 자리에 떨어진다 — 우클릭 놓기와 같은 경로다.
                    _harpoon.TryReleaseForWeaponSwitch();
                    return true;

                default:
                    return true;
            }
        }

        private void ApplyWeaponGates()
        {
            // 어느 무기를 열지는 HotbarLogic이 판정한다 — 카메라·커서가 이미 쓰는 것과 같은
            // 인게임 씬 판정을 함께 넘긴다(NetworkPlayerController).
            HotbarItemType selected = HotbarLogic.ResolveActiveWeapon(
                IsPanelOpen, GameplaySceneRoute.IsActiveSceneGameplay(), SelectedItemType);

            if (_harpoon != null)
            {
                _harpoon.InputEnabled = selected == HotbarItemType.Harpoon;

                // 그랩이 풀리면 거부 문구를 다시 띄울 수 있게 한다 — "같은 그랩 동안 첫 회만"의 경계다.
                if (!HarpoonSwitchRules.IsGrabHeld(_harpoon.State))
                {
                    _switchRejectAnnounced = false;
                }
            }

            if (_hammer != null)
            {
                _hammer.InputEnabled = selected == HotbarItemType.Hammer;
            }

            if (_melee != null)
            {
                _melee.InputEnabled = selected == HotbarItemType.Melee;
            }

            if (_fishingRod != null)
            {
                _fishingRod.InputEnabled = selected == HotbarItemType.FishingRod;
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

        private void OnMountStateChanged(Train.MountStateChangedLocalEvent evt)
        {
            _mounted = evt.IsMounted;
        }
    }
}
