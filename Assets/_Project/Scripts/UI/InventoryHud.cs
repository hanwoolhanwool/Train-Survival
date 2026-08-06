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
        private const float SlotSize = 52f;
        private const float SlotGap = 6f;

        [SerializeField] private ResourceCatalog _catalog;
        [SerializeField] private StructureCatalog _structureCatalog;

        private float _health;
        private float _maxHealth;
        private float _temperature;
        private TemperatureStress _temperatureStress;
        private bool _engineInRange;
        private CarBuildAimLocalEvent _carBuildAim;
        private CarRecoupleAimLocalEvent _carRecoupleAim;
        private HammerTargetLocalEvent _hammerTarget;
        private bool _panelOpen;
        private int _dragFromIndex = -1;

        private void OnEnable()
        {
            EventBus<PlayerHealthChangedEvent>.Subscribe(OnPlayerHealthChanged);
            EventBus<PlayerTemperatureChangedEvent>.Subscribe(OnPlayerTemperatureChanged);
            EventBus<EnginePromptLocalEvent>.Subscribe(OnEnginePrompt);
            EventBus<CarBuildAimLocalEvent>.Subscribe(OnCarBuildAim);
            EventBus<CarRecoupleAimLocalEvent>.Subscribe(OnCarRecoupleAim);
            EventBus<HammerTargetLocalEvent>.Subscribe(OnHammerTarget);
        }

        private void OnDisable()
        {
            EventBus<PlayerHealthChangedEvent>.Unsubscribe(OnPlayerHealthChanged);
            EventBus<PlayerTemperatureChangedEvent>.Unsubscribe(OnPlayerTemperatureChanged);
            EventBus<EnginePromptLocalEvent>.Unsubscribe(OnEnginePrompt);
            EventBus<CarBuildAimLocalEvent>.Unsubscribe(OnCarBuildAim);
            EventBus<CarRecoupleAimLocalEvent>.Unsubscribe(OnCarRecoupleAim);
            EventBus<HammerTargetLocalEvent>.Unsubscribe(OnHammerTarget);
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

        private void OnCarRecoupleAim(CarRecoupleAimLocalEvent evt)
        {
            _carRecoupleAim = evt;
        }

        private void OnHammerTarget(HammerTargetLocalEvent evt)
        {
            _hammerTarget = evt;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
            {
                _panelOpen = !_panelOpen;
                _dragFromIndex = -1;
                EventBus<InventoryPanelToggledLocalEvent>.Publish(new InventoryPanelToggledLocalEvent(_panelOpen));
            }
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

            if (_panelOpen)
            {
                DrawInventoryPanel(hotbar);
            }
        }

        private string GetSlotLabel(HotbarSlotView slot, int stackSize)
        {
            if (slot.ItemType == HotbarItemType.Resource)
            {
                string name = _catalog != null ? _catalog.GetDisplayName(slot.Resource) : "자원";
                int maxStack = _catalog != null ? _catalog.GetMaxStack(slot.Resource, stackSize) : stackSize;
                return $"{name}\n{slot.Count}/{maxStack}";
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

            for (int i = 0; i < slotCount; i++)
            {
                var rect = new Rect(startX + i * (SlotSize + SlotGap), y, SlotSize, SlotSize);
                GUI.Box(rect, GetSlotLabel(hotbar.GetSlot(i), hotbar.StackSize));

                if (i == hotbar.SelectedIndex)
                {
                    // 선택 강조 — 테두리 박스를 겹쳐 그린다.
                    GUI.Box(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), GUIContent.none);
                    GUI.Label(new Rect(rect.x, rect.y - 18f, rect.width, 16f), $"[{i + 1}]");
                }
            }
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
                prompt = $"<color=yellow>E 또는 좌클릭 — 연료 투입 ({name} 1개 = +{fuelValue:0.#})</color>";
            }
            else if (holdingResource)
            {
                string name = _catalog != null ? _catalog.GetDisplayName(held.Resource) : "이 자원";
                prompt = $"<color=red>{name} — 연료로 쓸 수 없는 자원이다</color>";
            }
            else
            {
                prompt = "<color=yellow>자원 슬롯(숫자 키 1~5)을 든 채 E — 연료 투입</color>";
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
                prompt = $"<color=red>칸 건설 자원 부족 ({BuildShortagePrompt(hotbar, _carBuildAim.Cost)})</color>";
            }
            else if (_carBuildAim.Occupied)
            {
                prompt = "<color=red>자리에 사람·몬스터가 있어 칸을 지을 수 없다</color>";
            }
            else
            {
                prompt = $"<color=yellow>우클릭 — 칸 건설 (소모: {BuildSpendPreview(hotbar, _carBuildAim.Cost)})</color>";
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
                    prompt = "<color=red>앞 칸이 비어 있어 재결합할 수 없다</color>";
                    break;
                case RecouplePrompt.NotAtSlot:
                    prompt = $"<color=red>칸을 슬롯까지 끌어와야 한다 ({_carRecoupleAim.RemainingMeters:F1} m 남음)</color>";
                    break;
                case RecouplePrompt.InsufficientResources:
                    prompt = $"<color=red>재결합 자원 부족 ({BuildShortagePrompt(hotbar, _carRecoupleAim.Cost)})</color>";
                    break;
                default:
                    prompt = $"<color=yellow>우클릭 — 재결합 (소모: {BuildSpendPreview(hotbar, _carRecoupleAim.Cost)})</color>";
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
                    partName = $"건축물 (#{_hammerTarget.Index}번 칸)";
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
                action += _hammerTarget.CanAffordStructure
                    ? $" — 우클릭 {structureName} 설치 (소모: {BuildSpendPreview(hotbar, _hammerTarget.StructureCost)}) [R] 종류 변경"
                    : $" — <color=red>{structureName} 설치 자원 부족 ({BuildShortagePrompt(hotbar, _hammerTarget.StructureCost)})</color> [R] 종류 변경";
            }

            string color = _hammerTarget.CanRepair && _hammerTarget.Health < _hammerTarget.MaxHealth
                ? "orange"
                : "white";
            GUI.Label(new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.54f, 400f, 24f),
                $"<color={color}>{partName}: {healthText}{action}</color>");
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
            float panelHeight = 34f + 20f + SlotSize + 16f + 20f + bagRows * stride + 12f + 90f;

            var rect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f,
                panelWidth, panelHeight);
            GUI.Box(rect, "인벤토리 / 캐릭터 상태 [I 닫기] — 드래그로 재배치 · 패널 밖 드롭 = 버리기");

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

            Event current = Event.current;
            for (int i = 0; i < total; i++)
            {
                Rect slotRect = GetPanelSlotRect(i, hotbarSize, columns, stride, gridX, hotbarRowY, bagStartY);
                GUI.Box(slotRect, GetSlotLabel(hotbar.GetSlot(i), hotbar.StackSize));

                if (current.type == EventType.MouseDown && slotRect.Contains(current.mousePosition) &&
                    !hotbar.GetSlot(i).IsEmpty)
                {
                    _dragFromIndex = i;
                    current.Use();
                }
                else if (current.type == EventType.MouseUp && slotRect.Contains(current.mousePosition) &&
                    _dragFromIndex >= 0)
                {
                    if (_dragFromIndex != i)
                    {
                        hotbar.RequestSwap(_dragFromIndex, i);
                    }

                    _dragFromIndex = -1;
                    current.Use();
                }
            }

            if (current.type == EventType.MouseUp && _dragFromIndex >= 0)
            {
                // 패널 안 공백 = 취소(실수 방지선), 패널 밖 = 버리기 (M5 3차 — hotbar 명세 §11 해소).
                // 무기·도구는 서버가 기각한다 — 처분하려면 공유 창고에 보관한다.
                if (!rect.Contains(current.mousePosition))
                {
                    HotbarSlotView dropSlot = hotbar.GetSlot(_dragFromIndex);
                    if (dropSlot.ItemType == HotbarItemType.Resource)
                    {
                        hotbar.RequestDrop(_dragFromIndex);
                    }
                }

                _dragFromIndex = -1;
            }

            if (_dragFromIndex >= 0)
            {
                var dragRect = new Rect(current.mousePosition.x - SlotSize * 0.5f,
                    current.mousePosition.y - SlotSize * 0.5f, SlotSize, SlotSize);
                GUI.Box(dragRect, GetSlotLabel(hotbar.GetSlot(_dragFromIndex), hotbar.StackSize));
            }

            GUILayout.BeginArea(new Rect(rect.x + 16f, cursorY, rect.width - 32f, 84f));
            GUILayout.Label("— 캐릭터 상태 —");
            GUILayout.Label(_maxHealth > 0f ? $"체력: {_health:F0} / {_maxHealth:F0}" : "체력: -");
            GUILayout.Label(_temperature > 0f ? $"체온: {_temperature:F1}℃{GetStressSuffix()}" : "체온: -");
            GUILayout.Label("허기: 이후 요리 시스템(M5)에서 추가");
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
    }
}
