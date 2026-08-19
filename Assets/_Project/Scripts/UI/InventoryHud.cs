using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using Game.Gameplay.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// 통합 핫바 HUD (기획서 §3.4) — 화면 중앙 하단 핫바 5칸(무기+자원, 선택 표시) +
    /// I키 인벤토리(드래그 재배치)/캐릭터 상태 창 + 엔진 투입 안내.
    /// UI는 상태를 소유하지 않는다: <see cref="ILocalHotbar"/> 읽기 조회와 이벤트 구독으로만 그린다.
    /// I키·드래그는 창 표시와 재배치 요청일 뿐이므로 UI 계층에서 처리한다 (확정은 호스트).
    /// </summary>
    public sealed class InventoryHud : MonoBehaviour
    {
        // 창 치수는 제작 창과 공유한다 (M7 3차 검증 W3-b — 둘이 나란히 떠야 한다).
        private const float SlotSize = HudLayout.SlotSize;
        private const float SlotGap = HudLayout.SlotGap;

        /// <summary>
        /// 캐릭터 상태 영역의 높이 — 헤더 + 상태 3줄(체력·체온·허기) + 버리기 안내 1줄이
        /// 잘리지 않을 만큼 잡는다.
        /// 상태 줄을 늘릴 때 이 값을 함께 올린다 (M5 4차 A2 — 허기 줄이 잘려 보이지 않던 회귀).
        /// </summary>
        private const float StatusAreaHeight = 130f;

        /// <summary>슬롯 전환 거부 연출의 길이 (초) — 짧게 한 번 튕기고 끝난다.</summary>
        private const float RejectShakeSeconds = 0.28f;

        // 버리기 임시 비활성 (8차 1차 검증 방침 2026-08-11 — "기능 자체를 끈다").
        // 코드 경로(수정자 키 수량·서버 원자 확정)는 유지 — 다시 켤 때 이 게이트만 연다.
        private static readonly bool DropEnabled = false;

        [SerializeField] private ResourceCatalog _catalog;
        [SerializeField] private StructureCatalog _structureCatalog;

        private float _health;
        private float _maxHealth;
        private float _temperature;
        private TemperatureStress _temperatureStress;
        private float _hunger;
        private float _maxHunger;
        private HungerStress _hungerStress;
        private bool _engineInRange;
        private CarBuildAimLocalEvent _carBuildAim;
        private CarRecoupleAimLocalEvent _carRecoupleAim;
        private HammerTargetLocalEvent _hammerTarget;
        private StructurePlaceAimLocalEvent _structurePlaceAim;
        private PlankAimLocalEvent _plankAim;
        private bool _panelOpen;
        private int _dragFromIndex = -1;

        // 슬롯 전환 거부 연출 (§3.6) — 거부된 칸이 잠깐 붉게 흔들린다.
        private int _rejectedSlotIndex = -1;
        private float _rejectedUntilTime;

        // 제작 창 (M7 3차 검증 개선) — 제작 창이 열리면 인벤토리도 함께 열려 재료를 보며 만들 수 있다.
        // 제작 창의 소유자는 CraftingStation이므로 여기서는 열림 여부만 미러링한다.
        private bool _craftingOpen;

        // 공유 창고 (M5 3차, 건축 개편 2차 — 식별 = 건축물 Id) — 드래그 출처가 창고인지, 어느 창고가 열려 있는지.
        private bool _dragFromStorage;
        private bool _storagePromptInRange;
        private int _storagePromptId = -1;
        private int _storageOpenId = -1;

        // 창고 보따리 (M5 8차) — 창고 창과 같은 규약. 열린 보따리는 NetworkObjectId로 식별한다.
        private bool _dragFromBundle;
        private bool _bundlePromptInRange;
        private bool _bundleOpen;
        private ulong _bundleOpenId;

        // 장비 착용 (M5 3차) — 드래그 출처가 착용 칸이면 그 부위 인덱스, 아니면 -1.
        private int _dragFromEquip = -1;

        // 로컬 플레이어의 집게 등급 (M5 5차 승급) — 핫바 라벨을 "집게(2단계)"로 바꾼다.
        private int _harpoonTier = 1;

        private static readonly string[] EquipSlotLabels = { "머리", "상체", "하체", "신발" };

        private void OnEnable()
        {
            EventBus<PlayerHealthChangedEvent>.Subscribe(OnPlayerHealthChanged);
            EventBus<PlayerTemperatureChangedEvent>.Subscribe(OnPlayerTemperatureChanged);
            EventBus<PlayerHungerChangedEvent>.Subscribe(OnPlayerHungerChanged);
            EventBus<EnginePromptLocalEvent>.Subscribe(OnEnginePrompt);
            EventBus<CarBuildAimLocalEvent>.Subscribe(OnCarBuildAim);
            EventBus<CarRecoupleAimLocalEvent>.Subscribe(OnCarRecoupleAim);
            EventBus<HammerTargetLocalEvent>.Subscribe(OnHammerTarget);
            EventBus<StructurePlaceAimLocalEvent>.Subscribe(OnStructurePlaceAim);
            EventBus<PlankAimLocalEvent>.Subscribe(OnPlankAim);
            EventBus<StoragePromptLocalEvent>.Subscribe(OnStoragePrompt);
            EventBus<StoragePanelToggledLocalEvent>.Subscribe(OnStoragePanelToggled);
            EventBus<BundlePromptLocalEvent>.Subscribe(OnBundlePrompt);
            EventBus<BundlePanelToggledLocalEvent>.Subscribe(OnBundlePanelToggled);
            EventBus<Game.Gameplay.Crafting.CraftingPanelToggledLocalEvent>.Subscribe(OnCraftingPanelToggled);
            EventBus<UiCloseRequestedLocalEvent>.Subscribe(OnUiCloseRequested);
            EventBus<Game.Gameplay.Harpoon.HarpoonTierChangedLocalEvent>.Subscribe(OnHarpoonTierChanged);
            EventBus<HotbarSelectionRejectedLocalEvent>.Subscribe(OnSelectionRejected);
        }

        private void OnDisable()
        {
            EventBus<PlayerHealthChangedEvent>.Unsubscribe(OnPlayerHealthChanged);
            EventBus<PlayerTemperatureChangedEvent>.Unsubscribe(OnPlayerTemperatureChanged);
            EventBus<PlayerHungerChangedEvent>.Unsubscribe(OnPlayerHungerChanged);
            EventBus<EnginePromptLocalEvent>.Unsubscribe(OnEnginePrompt);
            EventBus<CarBuildAimLocalEvent>.Unsubscribe(OnCarBuildAim);
            EventBus<CarRecoupleAimLocalEvent>.Unsubscribe(OnCarRecoupleAim);
            EventBus<HammerTargetLocalEvent>.Unsubscribe(OnHammerTarget);
            EventBus<StructurePlaceAimLocalEvent>.Unsubscribe(OnStructurePlaceAim);
            EventBus<PlankAimLocalEvent>.Unsubscribe(OnPlankAim);
            EventBus<StoragePromptLocalEvent>.Unsubscribe(OnStoragePrompt);
            EventBus<StoragePanelToggledLocalEvent>.Unsubscribe(OnStoragePanelToggled);
            EventBus<BundlePromptLocalEvent>.Unsubscribe(OnBundlePrompt);
            EventBus<BundlePanelToggledLocalEvent>.Unsubscribe(OnBundlePanelToggled);
            EventBus<Game.Gameplay.Crafting.CraftingPanelToggledLocalEvent>.Unsubscribe(OnCraftingPanelToggled);
            EventBus<UiCloseRequestedLocalEvent>.Unsubscribe(OnUiCloseRequested);
            EventBus<Game.Gameplay.Harpoon.HarpoonTierChangedLocalEvent>.Unsubscribe(OnHarpoonTierChanged);
            EventBus<HotbarSelectionRejectedLocalEvent>.Unsubscribe(OnSelectionRejected);
        }

        /// <summary>
        /// 슬롯 전환 거부 연출 (집게 단계별 파지 계획 §3.6) — 문구는 첫 회만 뜨지만
        /// <b>연출은 누를 때마다</b> 나간다. "눌렀는데 아무 반응이 없다"가 고장으로 읽히는 것을 막는 쪽이
        /// 이 연출의 목적이라, 반복 억제를 여기에 걸면 그 목적이 사라진다.
        /// </summary>
        private void OnSelectionRejected(HotbarSelectionRejectedLocalEvent evt)
        {
            _rejectedSlotIndex = evt.SlotIndex;
            _rejectedUntilTime = Time.unscaledTime + RejectShakeSeconds;
        }

        private void OnHarpoonTierChanged(Game.Gameplay.Harpoon.HarpoonTierChangedLocalEvent evt)
        {
            _harpoonTier = evt.Tier;
        }

        private void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _health = evt.Health;
                _maxHealth = evt.MaxHealth;
            }
        }

        private void OnPlayerTemperatureChanged(PlayerTemperatureChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _temperature = evt.Temperature;
                _temperatureStress = evt.Stress;
            }
        }

        private void OnPlayerHungerChanged(PlayerHungerChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _hunger = evt.Hunger;
                _maxHunger = evt.MaxHunger;
                _hungerStress = evt.Stress;
            }
        }

        /// <summary>상태 창 허기 줄에 붙일 압박 표시 — 색 태그 없이 텍스트로만 알린다.</summary>
        private string GetHungerSuffix()
        {
            switch (_hungerStress)
            {
                case HungerStress.Hungry:
                    return " (배고픔)";

                case HungerStress.Starving:
                    return " (굶주림)";

                default:
                    return string.Empty;
            }
        }

        /// <summary>상태 창 체온 줄에 붙일 압박 표시 — 색 태그 없이 텍스트로만 알린다.</summary>
        private string GetStressSuffix()
        {
            switch (_temperatureStress)
            {
                case TemperatureStress.Heat:
                    return " (더위)";

                case TemperatureStress.Cold:
                    return " (추위)";

                default:
                    return string.Empty;
            }
        }

        private void OnEnginePrompt(EnginePromptLocalEvent evt)
        {
            _engineInRange = evt.InRange;
        }

        private void OnCarBuildAim(CarBuildAimLocalEvent evt)
        {
            _carBuildAim = evt;
        }

        private void OnStructurePlaceAim(StructurePlaceAimLocalEvent evt)
        {
            _structurePlaceAim = evt;
        }

        private void OnPlankAim(PlankAimLocalEvent evt)
        {
            _plankAim = evt;
        }

        private void OnCarRecoupleAim(CarRecoupleAimLocalEvent evt)
        {
            _carRecoupleAim = evt;
        }

        private void OnHammerTarget(HammerTargetLocalEvent evt)
        {
            _hammerTarget = evt;
        }

        private void OnStoragePrompt(StoragePromptLocalEvent evt)
        {
            _storagePromptInRange = evt.IsInRange;
            _storagePromptId = evt.StorageId;
        }

        private void OnStoragePanelToggled(StoragePanelToggledLocalEvent evt)
        {
            _storageOpenId = evt.IsOpen ? evt.StorageId : -1;
            _dragFromIndex = -1;
            _dragFromStorage = false;
        }

        private void OnBundlePrompt(BundlePromptLocalEvent evt)
        {
            _bundlePromptInRange = evt.IsInRange;
        }

        private void OnBundlePanelToggled(BundlePanelToggledLocalEvent evt)
        {
            _bundleOpen = evt.IsOpen;
            _bundleOpenId = evt.IsOpen ? evt.BundleObjectId : 0UL;
            _dragFromIndex = -1;
            _dragFromBundle = false;
        }

        /// <summary>Esc의 닫기 요청 (M5 4차) — I 창만 여기서 닫는다 (창고·제작 창은 각자의 소유자가 닫는다).</summary>
        private void OnUiCloseRequested(UiCloseRequestedLocalEvent evt)
        {
            SetPanelOpen(false);
        }

        /// <summary>
        /// 제작 창 토글을 따라 인벤토리도 함께 여닫는다 (M7 3차 검증 개선) — 제작 창은 좌측 상단,
        /// 인벤토리는 화면 중앙이라 겹치지 않고, <b>재료를 보면서 제작</b>할 수 있다.
        /// 제작 창의 상태는 <see cref="Game.Gameplay.Crafting.CraftingStation"/>이 소유하므로
        /// 여기서는 미러링만 한다 (UI는 상태를 소유하지 않는다).
        /// </summary>
        private void OnCraftingPanelToggled(Game.Gameplay.Crafting.CraftingPanelToggledLocalEvent evt)
        {
            _craftingOpen = evt.IsOpen;
            SetPanelOpen(evt.IsOpen);
        }

        private void Update()
        {
            // 창고·보따리 창이 열려 있는 동안 토글 키는 무시한다 — 두 창이 이미 개인 인벤토리를 포함한다.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _storageOpenId >= 0 || _bundleOpen)
            {
                return;
            }

            // I와 Tab이 같은 토글이다 (M7 3차 검증 개선). Tab으로 제작대를 열지는 않는다 —
            // 제작 창 열기는 E(제작 지점 근접 + 시선)만의 몫이다.
            if (!keyboard.iKey.wasPressedThisFrame && !keyboard.tabKey.wasPressedThisFrame)
            {
                return;
            }

            // 제작 창과 함께 열려 있으면 <b>둘 다</b> 닫는다 — Esc와 같은 경로로 보내
            // 제작 창의 소유자(CraftingStation)가 자기 상태를 닫게 한다.
            if (_craftingOpen)
            {
                EventBus<UiCloseRequestedLocalEvent>.Publish(default);
                return;
            }

            SetPanelOpen(!_panelOpen);
        }

        /// <summary>인벤토리 창 표시 상태 — 바뀔 때만 이벤트를 발행한다(무기 게이트·Esc 우선순위 입력).</summary>
        private void SetPanelOpen(bool open)
        {
            if (_panelOpen == open)
            {
                return;
            }

            _panelOpen = open;
            _dragFromIndex = -1;
            EventBus<InventoryPanelToggledLocalEvent>.Publish(new InventoryPanelToggledLocalEvent(open));
        }

        private void OnGUI()
        {
            if (!ServiceLocator.TryGet(out ILocalHotbar hotbar))
            {
                return;
            }

            DrawHotbar(hotbar);
            DrawEnginePrompt(hotbar);
            DrawCarBuildPrompt(hotbar);
            DrawCarRecouplePrompt(hotbar);
            DrawHammerTarget(hotbar);
            DrawPlankAim(hotbar);
            DrawStoragePrompt();
            DrawBundlePrompt();

            if (_bundleOpen)
            {
                DrawBundlePanel(hotbar);
            }
            else if (_storageOpenId >= 0)
            {
                DrawStoragePanel(hotbar);
            }
            else if (_panelOpen)
            {
                DrawInventoryPanel(hotbar);
            }
        }

        /// <summary>공유 창고 접근 안내 — 창고 칸 근접 + 시선에서 표시한다 (M5 3차).</summary>
        private void DrawStoragePrompt()
        {
            if (!_storagePromptInRange || _panelOpen || _storageOpenId >= 0)
            {
                return;
            }

            // 다중 창고 (건축 개편 2차) — 조준(최근접 + 시선)한 그 창고가 열린다. Id는 내부 식별자라 노출하지 않는다.
            GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.66f, 300f, 24f),
                $"<color={UiPalette.HexFocusBrass}>E — 공유 창고</color>");
        }

        /// <summary>보따리 접근 안내 (M5 8차) — 근접 + 시선에서 표시한다 (창고와 같은 규약).</summary>
        private void DrawBundlePrompt()
        {
            if (!_bundlePromptInRange || _panelOpen || _bundleOpen || _storageOpenId >= 0)
            {
                return;
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.62f, 300f, 24f),
                $"<color={UiPalette.HexFocusBrass}>E — 보따리 (파괴된 창고의 내용물)</color>");
        }

        /// <summary>
        /// 패널 밖 드롭의 버리기 수량 — 수정자 키로 결정한다 (M5 8차 — 부분 버리기).
        /// 기본 = 전량 · Shift = 절반(최소 1) · Ctrl = 1개. 서버가 보유량으로 다시 클램프한다.
        /// </summary>
        private static int ComputeDropAmount(int count)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return count;
            }

            if (keyboard.ctrlKey.isPressed)
            {
                return 1;
            }

            if (keyboard.shiftKey.isPressed)
            {
                return Mathf.Max(1, count / 2);
            }

            return count;
        }

        private string GetSlotLabel(HotbarSlotView slot, int stackSize)
        {
            if (slot.ItemType == HotbarItemType.Resource)
            {
                string name = _catalog != null ? _catalog.GetDisplayName(slot.Resource) : "자원";
                int maxStack = _catalog != null ? _catalog.GetMaxStack(slot.Resource, stackSize) : stackSize;
                return $"{name}\n{slot.Count}/{maxStack}";
            }

            // 집게는 등급이 표시명에 들어간다 (M5 5차 승급) — 승급했는지 핫바에서 바로 보인다.
            if (slot.ItemType == HotbarItemType.Harpoon)
            {
                return HotbarItemLabels.GetHarpoonLabel(_harpoonTier);
            }

            // 무기·도구 표시명은 제작 UI와 공유한다 (M5 2차 — 무기 종류 확장).
            return HotbarItemLabels.GetLabel(slot.ItemType);
        }

        private void DrawHotbar(ILocalHotbar hotbar)
        {
            int slotCount = hotbar.HotbarSize;
            float totalWidth = slotCount * SlotSize + (slotCount - 1) * SlotGap;
            float startX = (Screen.width - totalWidth) * 0.5f;
            float y = Screen.height - SlotSize - 16f;

            // 거부 연출 (§3.6) — 거부된 칸만 좌우로 튕기고 붉게 물든다. 연출이 끝나면 원래대로 그린다.
            bool rejecting = Time.unscaledTime < _rejectedUntilTime;
            float rejectShake = 0f;
            if (rejecting)
            {
                float remaining = _rejectedUntilTime - Time.unscaledTime;
                // 남은 시간이 줄수록 진폭도 준다 — 한 번 튕기고 잦아드는 모양.
                rejectShake = Mathf.Sin(remaining * 60f) * (6f * remaining / RejectShakeSeconds);
            }

            Color baseColor = GUI.color;

            for (int i = 0; i < slotCount; i++)
            {
                bool rejected = rejecting && i == _rejectedSlotIndex;
                float offsetX = rejected ? rejectShake : 0f;
                var rect = new Rect(startX + i * (SlotSize + SlotGap) + offsetX, y, SlotSize, SlotSize);

                // 거부 슬롯은 아이콘 위에 얹는 틴트라 면색이 아니라 밝은 텍스트 변형을 쓴다.
                GUI.color = rejected ? UiPalette.CriticalText : baseColor;
                GUI.Box(rect, GetSlotLabel(hotbar.GetSlot(i), hotbar.StackSize));

                if (i == hotbar.SelectedIndex)
                {
                    // 선택 강조 — 테두리 박스를 겹쳐 그린다.
                    GUI.Box(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), GUIContent.none);
                    GUI.Label(new Rect(rect.x, rect.y - 18f, rect.width, 16f), $"[{i + 1}]");
                }
            }

            GUI.color = baseColor;
        }

        private void DrawEnginePrompt(ILocalHotbar hotbar)
        {
            if (!_engineInRange || _panelOpen)
            {
                return;
            }

            // 발열량 0 이하(화약 원료·탄약)는 투입되지 않는 자원 — 투입 안내 대신 불가 사유를 보여준다 (M5 검증 D3·D4).
            HotbarSlotView held = hotbar.GetSlot(hotbar.SelectedIndex);
            bool holdingResource = held.ItemType == HotbarItemType.Resource;
            float fuelValue = _catalog != null ? _catalog.GetFuelValue(held.Resource) : 1f;

            string prompt;
            if (holdingResource && fuelValue > 0f)
            {
                string name = _catalog != null ? _catalog.GetDisplayName(held.Resource) : "자원";
                prompt = $"<color={UiPalette.HexFocusBrass}>E 또는 좌클릭 — 연료 투입 ({name} 1개 = +{fuelValue:0.#})</color>";
            }
            else if (holdingResource)
            {
                string name = _catalog != null ? _catalog.GetDisplayName(held.Resource) : "이 자원";
                prompt = $"<color={UiPalette.HexCriticalText}>{name} — 연료로 쓸 수 없는 자원이다</color>";
            }
            else
            {
                prompt = $"<color={UiPalette.HexFocusBrass}>자원 슬롯(숫자 키 1~5)을 든 채 E — 연료 투입</color>";
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.62f, 300f, 24f), prompt);
        }

        /// <summary>칸 건설 안내 — 망치로 건설 지점(초록 테두리 프리뷰)을 겨눈 동안 표시한다.</summary>
        private void DrawCarBuildPrompt(ILocalHotbar hotbar)
        {
            if (!_carBuildAim.Aiming || _panelOpen)
            {
                return;
            }

            string prompt;
            if (!_carBuildAim.CanAfford)
            {
                prompt = $"<color={UiPalette.HexCriticalText}>칸 건설 자원 부족 ({BuildShortagePrompt(hotbar, _carBuildAim.Cost)})</color>";
            }
            else if (_carBuildAim.Occupied)
            {
                prompt = $"<color={UiPalette.HexCriticalText}>자리에 사람·몬스터가 있어 칸을 지을 수 없다</color>";
            }
            else
            {
                prompt = $"<color={UiPalette.HexFocusBrass}>우클릭 — 칸 건설 (소모: {BuildSpendPreview(hotbar, _carBuildAim.Cost)})</color>";
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.58f, 360f, 24f), prompt);
        }

        /// <summary>
        /// 재결합 안내 — 망치로 이탈 칸의 앞 연결 지점을 겨눈 동안 표시한다 (손잡이-이탈저항 스펙 §4.1).
        /// 못 붙이는 이유는 먼저 걸리는 것 하나만 보여준다(구조적 순서 → 진행 → 비용).
        /// 칸 건설 안내와는 상호 배타라 같은 자리에 그린다.
        /// </summary>
        private void DrawCarRecouplePrompt(ILocalHotbar hotbar)
        {
            if (!_carRecoupleAim.Aiming || _panelOpen)
            {
                return;
            }

            string prompt;
            switch (_carRecoupleAim.Prompt)
            {
                case RecouplePrompt.FrontCarMissing:
                    prompt = $"<color={UiPalette.HexCriticalText}>앞 칸이 비어 있어 재결합할 수 없다</color>";
                    break;
                case RecouplePrompt.NotAtSlot:
                    prompt = $"<color={UiPalette.HexCriticalText}>칸을 슬롯까지 끌어와야 한다 ({_carRecoupleAim.RemainingMeters:F1} m 남음)</color>";
                    break;
                case RecouplePrompt.InsufficientResources:
                    prompt = $"<color={UiPalette.HexCriticalText}>재결합 자원 부족 ({BuildShortagePrompt(hotbar, _carRecoupleAim.Cost)})</color>";
                    break;
                default:
                    prompt = $"<color={UiPalette.HexFocusBrass}>우클릭 — 재결합 (소모: {BuildSpendPreview(hotbar, _carRecoupleAim.Cost)})</color>";
                    break;
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.58f, 360f, 24f), prompt);
        }

        /// <summary>망치 조준 라벨 — 겨눈 부위의 체력과 가능한 조작(수리/설치)을 조준점 아래에 보여준다(수리 과정 가시화).</summary>
        private void DrawHammerTarget(ILocalHotbar hotbar)
        {
            if (!_hammerTarget.HasTarget || _panelOpen)
            {
                return;
            }

            string partName;
            switch (_hammerTarget.Kind)
            {
                case TrainPartKind.Coupling:
                    partName = $"연결부 #{_hammerTarget.Index}";
                    break;
                case TrainPartKind.Structure:
                    // 다중 설치 (건축 개편 1차) — Index는 칸이 아니라 항목 Id이므로 종류명으로 부른다.
                    partName = _structureCatalog != null
                        ? _structureCatalog.GetDisplayName(_hammerTarget.TargetStructureKind)
                        : _hammerTarget.TargetStructureKind.ToString();
                    break;
                default:
                    partName = _hammerTarget.Index == 0 ? "기관차" : $"칸 #{_hammerTarget.Index}";
                    break;
            }

            string healthText = float.IsPositiveInfinity(_hammerTarget.MaxHealth)
                ? "파괴 불가"
                : $"{_hammerTarget.Health:F0} / {_hammerTarget.MaxHealth:F0}";

            // 수리·설치 안내는 독립 조건이라 함께 보여준다 — 손상된 빈 칸에서도 부족 안내가 가려지지 않는다.
            string action = string.Empty;
            if (_hammerTarget.CanRepair)
            {
                action += " — 좌클릭 수리";
            }

            if (_hammerTarget.CanBuildStructure)
            {
                string structureName = _structureCatalog != null
                    ? _structureCatalog.GetDisplayName(_hammerTarget.SelectedStructureKind)
                    : _hammerTarget.SelectedStructureKind.ToString();
                if (!_hammerTarget.CanAffordStructure)
                {
                    action += $" — <color={UiPalette.HexCriticalText}>{structureName} 설치 자원 부족 ({BuildShortagePrompt(hotbar, _hammerTarget.StructureCost)})</color> [R] 종류 변경";
                }
                else if (_structurePlaceAim.Aiming && _structurePlaceAim.Occupied)
                {
                    // 자리 점유 안내 (건축 개편 1차 — 칸 건설과 같은 규약: 테두리 안이 비어야 지어진다).
                    action += $" — <color={UiPalette.HexCriticalText}>자리에 사람·몬스터가 있어 설치할 수 없다</color> [R] 종류 변경";
                }
                else
                {
                    action += $" — 우클릭 {structureName} 설치 (소모: {BuildSpendPreview(hotbar, _hammerTarget.StructureCost)}) [R] 종류 변경";
                }
            }

            // X 홀드 철거 안내 (건축 개편 2차 — 결정 ④·⑤): 반환량과 홀드 게이지를 함께 보여준다.
            if (_hammerTarget.CanDemolish)
            {
                string refundName = _catalog != null && _structureCatalog != null
                    ? _catalog.GetDisplayName(_structureCatalog.GetRefundResource(_hammerTarget.TargetStructureKind))
                    : "자원";
                action += _hammerTarget.DemolishProgress > 0f
                    ? $" — <color={UiPalette.HexAlertText}>철거 중… {_hammerTarget.DemolishProgress * 100f:F0}%</color>"
                    : $" — [X 홀드] 철거 (반환: {refundName} {_hammerTarget.DemolishRefund})";
            }

            string color = _hammerTarget.CanRepair && _hammerTarget.Health < _hammerTarget.MaxHealth
                ? UiPalette.HexAlertText
                : UiPalette.HexTextSteam;
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.54f, 400f, 24f),
                $"<color={color}>{partName}: {healthText}{action}</color>");
        }

        /// <summary>
        /// 판자 증축·철거 안내 (건축 개편 3차 — 계획서 §2.9). 망치로 칸 옆 판자 열을 겨눈 동안
        /// 조준점 위 줄에 무엇이 일어날지 보여준다 — 빈 자리면 우클릭 증축, 이미 깔린 판자면 X 홀드 철거.
        /// 건축물을 겨눈 X 홀드는 건축물 철거(2차)의 몫이라, 그때는 판자 철거 안내를 감춘다.
        /// </summary>
        private void DrawPlankAim(ILocalHotbar hotbar)
        {
            if (!_plankAim.Aiming || _panelOpen)
            {
                return;
            }

            string sideName = _plankAim.Side == PlankSide.Left ? "좌측" : "우측";
            string where = $"칸 #{_plankAim.CarIndex} {sideName} 판자";
            string action;

            if (_plankAim.EmptySlot)
            {
                if (_plankAim.CanBuild)
                {
                    action = $"우클릭 증축 (소모: {BuildSpendPreview(hotbar, _plankAim.Cost)})";
                }
                else
                {
                    action = _plankAim.CanAfford
                        ? $"<color={UiPalette.HexCriticalText}>여기엔 판자를 붙일 수 없다</color>"
                        : $"<color={UiPalette.HexCriticalText}>증축 자원 부족 ({BuildShortagePrompt(hotbar, _plankAim.Cost)})</color>";
                }
            }
            else if (_plankAim.RemoveProgress > 0f)
            {
                action = $"<color={UiPalette.HexAlertText}>철거 중… {_plankAim.RemoveProgress * 100f:F0}%</color>";
            }
            else if (_plankAim.CanRemove)
            {
                string refundName = _catalog != null
                    ? _catalog.GetDisplayName(_plankAim.RefundResource)
                    : "자원";
                action = $"[X 홀드] 철거 (반환: {refundName} {_plankAim.Refund})";
            }
            else
            {
                action = $"<color={UiPalette.HexCriticalText}>위에 놓인 건축물을 먼저 철거해야 뜯을 수 있다</color>";
            }

            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.50f, 400f, 24f),
                $"{where} — {action}");
        }

        /// <summary>
        /// 건설 비용으로 실제 소모될 종류·수량 미리보기 (M5 검증 E1) — 서버와 같은 규칙
        /// (뒤 칸부터 건자재 차감)을 복제 슬롯 사본 위에서 재현하므로 표시와 실소모가 항상 일치한다.
        /// </summary>
        private string BuildSpendPreview(ILocalHotbar hotbar, int cost)
        {
            if (_catalog == null || cost <= 0)
            {
                return $"건자재 {cost}개";
            }

            var slots = new HotbarSlotView[hotbar.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = hotbar.GetSlot(i);
            }

            // 소모 내역 집계 — 종류 가짓수가 적어 배열 순회로 충분하다.
            var types = new ResourceType[cost];
            var counts = new int[cost];
            int distinct = 0;
            for (int n = 0; n < cost; n++)
            {
                if (!HotbarLogic.TryRemoveAnyResource(slots, _catalog.IsBuildMaterial, out ResourceType removed))
                {
                    return $"건자재 {cost}개";
                }

                int found = -1;
                for (int i = 0; i < distinct; i++)
                {
                    if (types[i] == removed)
                    {
                        found = i;
                        break;
                    }
                }

                if (found < 0)
                {
                    types[distinct] = removed;
                    counts[distinct] = 1;
                    distinct++;
                }
                else
                {
                    counts[found]++;
                }
            }

            var builder = new System.Text.StringBuilder(32);
            for (int i = 0; i < distinct; i++)
            {
                if (i > 0)
                {
                    builder.Append("·");
                }

                builder.Append($"{_catalog.GetDisplayName(types[i])} {counts[i]}");
            }

            return builder.ToString();
        }

        /// <summary>
        /// 비용 부족 안내 — 보유/필요와 함께 무엇이 건자재인지 알려준다 (M5 검증 E1 후속:
        /// "부족할 때도 필요한 자원을 표시"). 예: "건자재 2/3 필요 — 목재·돌·고철".
        /// </summary>
        private string BuildShortagePrompt(ILocalHotbar hotbar, int cost)
        {
            if (_catalog == null)
            {
                return $"건자재 {cost}개 필요";
            }

            var slots = new HotbarSlotView[hotbar.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = hotbar.GetSlot(i);
            }

            int have = HotbarLogic.CountResource(slots, _catalog.IsBuildMaterial);
            return $"건자재 {have}/{cost} 필요 — {_catalog.GetBuildMaterialNames()}";
        }

        private void DrawInventoryPanel(ILocalHotbar hotbar)
        {
            const int columns = 5;
            int hotbarSize = hotbar.HotbarSize;
            int total = hotbar.SlotCount;
            int bagSize = Mathf.Max(0, total - hotbarSize);
            int bagRows = bagSize > 0 ? Mathf.CeilToInt(bagSize / (float)columns) : 0;

            float stride = SlotSize + SlotGap;
            float gridWidth = columns * SlotSize + (columns - 1) * SlotGap;
            float panelWidth = gridWidth + 40f;
            // 마지막 항 = 캐릭터 상태 영역(헤더 + 체력·체온·허기 3줄). 줄이 늘면 여기와
            // DrawCharacterStatus의 영역 높이를 함께 올려야 한다 — 낮으면 마지막 줄이 잘린다(M5 4차 A2).
            float panelHeight = 34f + 20f + SlotSize + 16f + 20f + bagRows * stride + 12f
                + 20f + SlotSize + 16f + StatusAreaHeight + 8f;

            var rect = new Rect(HudLayout.InventoryPanelX, HudLayout.CenteredY(panelHeight),
                panelWidth, panelHeight);
            GUI.Box(rect, "인벤토리 / 캐릭터 상태 [I·Tab 닫기] — 드래그로 재배치 · 패널 밖 드롭 = 버리기");

            float gridX = rect.x + (rect.width - gridWidth) * 0.5f;
            float cursorY = rect.y + 34f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "핫바 [숫자 키 1~5]");
            cursorY += 20f;
            float hotbarRowY = cursorY;
            cursorY += SlotSize + 16f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "가방");
            cursorY += 20f;
            float bagStartY = cursorY;
            cursorY += bagRows * stride + 12f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "착용 — 장비를 끌어다 놓기 (기획서 §6.3)");
            cursorY += 20f;
            float equipRowY = cursorY;
            cursorY += SlotSize + 16f;

            Event current = Event.current;
            for (int i = 0; i < total; i++)
            {
                Rect slotRect = GetPanelSlotRect(i, hotbarSize, columns, stride, gridX, hotbarRowY, bagStartY);
                GUI.Box(slotRect, GetSlotLabel(hotbar.GetSlot(i), hotbar.StackSize));

                if (current.type == EventType.MouseDown && slotRect.Contains(current.mousePosition) &&
                    !hotbar.GetSlot(i).IsEmpty)
                {
                    _dragFromIndex = i;
                    _dragFromEquip = -1;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && slotRect.Contains(current.mousePosition) &&
                    (_dragFromIndex >= 0 || _dragFromEquip >= 0))
                {
                    if (_dragFromEquip >= 0)
                    {
                        // 착용 해제 — 서버가 첫 빈 인벤토리 칸으로 되돌린다.
                        hotbar.RequestUnequip(_dragFromEquip);
                    }
                    else if (_dragFromIndex != i)
                    {
                        hotbar.RequestSwap(_dragFromIndex, i);
                    }

                    _dragFromIndex = -1;
                    _dragFromEquip = -1;
                    current.Use();
                }
            }

            // 착용 4칸 — 어느 칸에 놓아도 부위는 서버가 카탈로그로 판정한다.
            for (int part = 0; part < EquipSlotLabels.Length; part++)
            {
                var slotRect = new Rect(gridX + part * stride, equipRowY, SlotSize, SlotSize);
                HotbarSlotView equipped = hotbar.GetEquipmentSlot(part);
                string label = equipped.IsEmpty
                    ? $"({EquipSlotLabels[part]})"
                    : GetSlotLabel(equipped, hotbar.StackSize);
                GUI.Box(slotRect, label);

                if (current.type == EventType.MouseDown && slotRect.Contains(current.mousePosition)
                    && !equipped.IsEmpty)
                {
                    _dragFromEquip = part;
                    _dragFromIndex = -1;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && slotRect.Contains(current.mousePosition)
                    && _dragFromIndex >= 0)
                {
                    hotbar.RequestEquip(_dragFromIndex);
                    _dragFromIndex = -1;
                    _dragFromEquip = -1;
                    current.Use();
                }
            }

            if (current.type == EventType.MouseUp && (_dragFromIndex >= 0 || _dragFromEquip >= 0))
            {
                // 패널 안 공백 = 취소(실수 방지선), 패널 밖 = 버리기 (M5 3차 — hotbar 명세 §11 해소).
                // 무기·도구·착용 장비는 서버가 기각·취소한다 — 처분하려면 공유 창고에 보관한다.
                if (DropEnabled && !rect.Contains(current.mousePosition) && _dragFromIndex >= 0)
                {
                    HotbarSlotView dropSlot = hotbar.GetSlot(_dragFromIndex);
                    if (dropSlot.ItemType == HotbarItemType.Resource)
                    {
                        hotbar.RequestDrop(_dragFromIndex, ComputeDropAmount(dropSlot.Count));
                    }
                }

                _dragFromIndex = -1;
                _dragFromEquip = -1;
            }

            if (_dragFromIndex >= 0 || _dragFromEquip >= 0)
            {
                var dragRect = new Rect(current.mousePosition.x - SlotSize * 0.5f,
                    current.mousePosition.y - SlotSize * 0.5f, SlotSize, SlotSize);
                HotbarSlotView dragSlot = _dragFromEquip >= 0
                    ? hotbar.GetEquipmentSlot(_dragFromEquip)
                    : hotbar.GetSlot(_dragFromIndex);
                GUI.Box(dragRect, GetSlotLabel(dragSlot, hotbar.StackSize));
            }

            GUILayout.BeginArea(new Rect(rect.x + 16f, cursorY, rect.width - 32f, StatusAreaHeight));
            GUILayout.Label("— 캐릭터 상태 —");
            GUILayout.Label(_maxHealth > 0f ? $"체력: {_health:F0} / {_maxHealth:F0}" : "체력: -");
            GUILayout.Label(_temperature > 0f ? $"체온: {_temperature:F1}℃{GetStressSuffix()}" : "체온: -");
            GUILayout.Label(_maxHunger > 0f ? $"허기: {_hunger:F0} / {_maxHunger:F0}{GetHungerSuffix()}" : "허기: -");
            if (DropEnabled)
            {
                GUILayout.Label("버리기: 패널 밖 드롭 = 전량 · Shift 절반 · Ctrl 1개");
            }

            GUILayout.EndArea();
        }

        /// <summary>I 창에서 슬롯 i의 사각형 — 앞쪽 <paramref name="hotbarSize"/>칸은 핫바 행, 나머지는 가방 격자.</summary>
        private static Rect GetPanelSlotRect(
            int index, int hotbarSize, int columns, float stride, float gridX, float hotbarRowY, float bagStartY)
        {
            if (index < hotbarSize)
            {
                return new Rect(gridX + index * stride, hotbarRowY, SlotSize, SlotSize);
            }

            int bagIndex = index - hotbarSize;
            return new Rect(
                gridX + bagIndex % columns * stride,
                bagStartY + bagIndex / columns * stride,
                SlotSize, SlotSize);
        }

        /// <summary>
        /// 공유 창고 창 (M5 3차) — 창고 격자(위) + 개인 인벤토리(아래)를 한 패널에 그리고,
        /// 격자 간 드래그로 이동 요청을 보낸다 (확정은 호스트 — <see cref="ITrainStorage"/>).
        /// </summary>
        private void DrawStoragePanel(ILocalHotbar hotbar)
        {
            if (!ServiceLocator.TryGet(out ITrainStorage storage))
            {
                return;
            }

            const int columns = 5;
            int storageSlots = storage.SlotsPerStorage;
            int storageRows = Mathf.CeilToInt(storageSlots / (float)columns);
            int hotbarSize = hotbar.HotbarSize;
            int total = hotbar.SlotCount;
            int bagSize = Mathf.Max(0, total - hotbarSize);
            int bagRows = bagSize > 0 ? Mathf.CeilToInt(bagSize / (float)columns) : 0;

            float stride = SlotSize + SlotGap;
            float gridWidth = columns * SlotSize + (columns - 1) * SlotGap;
            float panelWidth = gridWidth + 40f;
            float panelHeight = 34f + 20f + storageRows * stride + 16f
                + 20f + SlotSize + 16f + 20f + bagRows * stride + 16f;

            var rect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f,
                panelWidth, panelHeight);
            GUI.Box(rect, "공유 창고 [E 닫기] — 드래그로 이동");

            float gridX = rect.x + (rect.width - gridWidth) * 0.5f;
            float cursorY = rect.y + 34f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "창고");
            cursorY += 20f;
            float storageStartY = cursorY;
            cursorY += storageRows * stride + 16f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "핫바 [숫자 키 1~5]");
            cursorY += 20f;
            float hotbarRowY = cursorY;
            cursorY += SlotSize + 16f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "가방");
            cursorY += 20f;
            float bagStartY = cursorY;

            Event current = Event.current;

            // 창고 격자 — MouseDown = 드래그 시작(창고 출처), MouseUp = 이 칸으로 이동.
            for (int i = 0; i < storageSlots; i++)
            {
                var slotRect = new Rect(
                    gridX + i % columns * stride, storageStartY + i / columns * stride, SlotSize, SlotSize);
                GUI.Box(slotRect, GetSlotLabel(storage.GetSlot(_storageOpenId, i), hotbar.StackSize));

                if (current.type == EventType.MouseDown && slotRect.Contains(current.mousePosition)
                    && !storage.GetSlot(_storageOpenId, i).IsEmpty)
                {
                    _dragFromIndex = i;
                    _dragFromStorage = true;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && slotRect.Contains(current.mousePosition)
                    && _dragFromIndex >= 0)
                {
                    byte from = _dragFromStorage ? ITrainStorage.ContainerStorage : ITrainStorage.ContainerInventory;
                    storage.RequestTransfer(_storageOpenId, from, _dragFromIndex, ITrainStorage.ContainerStorage, i);
                    _dragFromIndex = -1;
                    _dragFromStorage = false;
                    current.Use();
                }
            }

            // 개인 격자 — 개인끼리는 기존 스왑 경로, 창고에서 오면 이동 요청.
            for (int i = 0; i < total; i++)
            {
                Rect slotRect = GetPanelSlotRect(i, hotbarSize, columns, stride, gridX, hotbarRowY, bagStartY);
                GUI.Box(slotRect, GetSlotLabel(hotbar.GetSlot(i), hotbar.StackSize));

                if (current.type == EventType.MouseDown && slotRect.Contains(current.mousePosition)
                    && !hotbar.GetSlot(i).IsEmpty)
                {
                    _dragFromIndex = i;
                    _dragFromStorage = false;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && slotRect.Contains(current.mousePosition)
                    && _dragFromIndex >= 0)
                {
                    if (_dragFromStorage)
                    {
                        storage.RequestTransfer(_storageOpenId,
                            ITrainStorage.ContainerStorage, _dragFromIndex, ITrainStorage.ContainerInventory, i);
                    }
                    else if (_dragFromIndex != i)
                    {
                        hotbar.RequestSwap(_dragFromIndex, i);
                    }

                    _dragFromIndex = -1;
                    _dragFromStorage = false;
                    current.Use();
                }
            }

            if (current.type == EventType.MouseUp && _dragFromIndex >= 0)
            {
                // 패널 밖 = 버리기 (개인 자원 칸만 — I 창과 같은 규약). 창고 출처는 취소.
                if (DropEnabled && !rect.Contains(current.mousePosition) && !_dragFromStorage
                    && hotbar.GetSlot(_dragFromIndex).ItemType == HotbarItemType.Resource)
                {
                    hotbar.RequestDrop(_dragFromIndex, ComputeDropAmount(hotbar.GetSlot(_dragFromIndex).Count));
                }

                _dragFromIndex = -1;
                _dragFromStorage = false;
            }

            if (_dragFromIndex >= 0)
            {
                var dragRect = new Rect(current.mousePosition.x - SlotSize * 0.5f,
                    current.mousePosition.y - SlotSize * 0.5f, SlotSize, SlotSize);
                HotbarSlotView dragSlot = _dragFromStorage
                    ? storage.GetSlot(_storageOpenId, _dragFromIndex)
                    : hotbar.GetSlot(_dragFromIndex);
                GUI.Box(dragRect, GetSlotLabel(dragSlot, hotbar.StackSize));
            }
        }

        /// <summary>
        /// 보따리 창 (M5 8차) — 창고 창 재사용: 보따리 격자(위) + 개인 인벤토리(아래)를 한 패널에
        /// 그리고, 격자 간 드래그로 이동을 요청한다 (확정은 호스트 — <see cref="ITrainStorage"/> 파사드).
        /// </summary>
        private void DrawBundlePanel(ILocalHotbar hotbar)
        {
            if (!ServiceLocator.TryGet(out ITrainStorage storage))
            {
                return;
            }

            int bundleSlots = storage.GetBundleSlotCount(_bundleOpenId);
            if (bundleSlots <= 0)
            {
                // 보따리가 비워져 회수됐다 — 창 소유자(StorageBundle)의 닫힘 이벤트가 곧 오지만,
                // 같은 프레임 표시 공백을 빈 패널 대신 아무것도 그리지 않는 쪽으로 메운다.
                return;
            }

            const int columns = 5;
            int bundleRows = Mathf.CeilToInt(bundleSlots / (float)columns);
            int hotbarSize = hotbar.HotbarSize;
            int total = hotbar.SlotCount;
            int bagSize = Mathf.Max(0, total - hotbarSize);
            int bagRows = bagSize > 0 ? Mathf.CeilToInt(bagSize / (float)columns) : 0;

            float stride = SlotSize + SlotGap;
            float gridWidth = columns * SlotSize + (columns - 1) * SlotGap;
            float panelWidth = gridWidth + 40f;
            float panelHeight = 34f + 20f + bundleRows * stride + 16f
                + 20f + SlotSize + 16f + 20f + bagRows * stride + 16f;

            var rect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f,
                panelWidth, panelHeight);
            GUI.Box(rect, "보따리 [E 닫기] — 드래그로 이동");

            float gridX = rect.x + (rect.width - gridWidth) * 0.5f;
            float cursorY = rect.y + 34f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "보따리");
            cursorY += 20f;
            float bundleStartY = cursorY;
            cursorY += bundleRows * stride + 16f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "핫바 [숫자 키 1~5]");
            cursorY += 20f;
            float hotbarRowY = cursorY;
            cursorY += SlotSize + 16f;

            GUI.Label(new Rect(gridX, cursorY, gridWidth, 18f), "가방");
            cursorY += 20f;
            float bagStartY = cursorY;

            Event current = Event.current;

            // 보따리 격자 — MouseDown = 드래그 시작(보따리 출처), MouseUp = 이 칸으로 이동.
            for (int i = 0; i < bundleSlots; i++)
            {
                var slotRect = new Rect(
                    gridX + i % columns * stride, bundleStartY + i / columns * stride, SlotSize, SlotSize);
                GUI.Box(slotRect, GetSlotLabel(storage.GetBundleSlot(_bundleOpenId, i), hotbar.StackSize));

                if (current.type == EventType.MouseDown && slotRect.Contains(current.mousePosition)
                    && !storage.GetBundleSlot(_bundleOpenId, i).IsEmpty)
                {
                    _dragFromIndex = i;
                    _dragFromBundle = true;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && slotRect.Contains(current.mousePosition)
                    && _dragFromIndex >= 0)
                {
                    byte from = _dragFromBundle ? ITrainStorage.ContainerBundle : ITrainStorage.ContainerInventory;
                    storage.RequestBundleTransfer(_bundleOpenId, from, _dragFromIndex, ITrainStorage.ContainerBundle, i);
                    _dragFromIndex = -1;
                    _dragFromBundle = false;
                    current.Use();
                }
            }

            // 개인 격자 — 개인끼리는 기존 스왑 경로, 보따리에서 오면 이동 요청.
            for (int i = 0; i < total; i++)
            {
                Rect slotRect = GetPanelSlotRect(i, hotbarSize, columns, stride, gridX, hotbarRowY, bagStartY);
                GUI.Box(slotRect, GetSlotLabel(hotbar.GetSlot(i), hotbar.StackSize));

                if (current.type == EventType.MouseDown && slotRect.Contains(current.mousePosition)
                    && !hotbar.GetSlot(i).IsEmpty)
                {
                    _dragFromIndex = i;
                    _dragFromBundle = false;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && slotRect.Contains(current.mousePosition)
                    && _dragFromIndex >= 0)
                {
                    if (_dragFromBundle)
                    {
                        storage.RequestBundleTransfer(_bundleOpenId,
                            ITrainStorage.ContainerBundle, _dragFromIndex, ITrainStorage.ContainerInventory, i);
                    }
                    else if (_dragFromIndex != i)
                    {
                        hotbar.RequestSwap(_dragFromIndex, i);
                    }

                    _dragFromIndex = -1;
                    _dragFromBundle = false;
                    current.Use();
                }
            }

            if (current.type == EventType.MouseUp && _dragFromIndex >= 0)
            {
                // 패널 밖 = 버리기 (개인 자원 칸만 — I 창과 같은 규약). 보따리 출처는 취소.
                if (DropEnabled && !rect.Contains(current.mousePosition) && !_dragFromBundle
                    && hotbar.GetSlot(_dragFromIndex).ItemType == HotbarItemType.Resource)
                {
                    hotbar.RequestDrop(_dragFromIndex, ComputeDropAmount(hotbar.GetSlot(_dragFromIndex).Count));
                }

                _dragFromIndex = -1;
                _dragFromBundle = false;
            }

            if (_dragFromIndex >= 0)
            {
                var dragRect = new Rect(current.mousePosition.x - SlotSize * 0.5f,
                    current.mousePosition.y - SlotSize * 0.5f, SlotSize, SlotSize);
                HotbarSlotView dragSlot = _dragFromBundle
                    ? storage.GetBundleSlot(_bundleOpenId, _dragFromIndex)
                    : hotbar.GetSlot(_dragFromIndex);
                GUI.Box(dragRect, GetSlotLabel(dragSlot, hotbar.StackSize));
            }
        }
    }
}
