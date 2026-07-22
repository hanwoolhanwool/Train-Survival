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

        private float _health;
        private float _maxHealth;
        private bool _engineInRange;
        private bool _panelOpen;
        private int _dragFromIndex = -1;

        private void OnEnable()
        {
            EventBus<PlayerHealthChangedEvent>.Subscribe(OnPlayerHealthChanged);
            EventBus<EnginePromptLocalEvent>.Subscribe(OnEnginePrompt);
        }

        private void OnDisable()
        {
            EventBus<PlayerHealthChangedEvent>.Unsubscribe(OnPlayerHealthChanged);
            EventBus<EnginePromptLocalEvent>.Unsubscribe(OnEnginePrompt);
        }

        private void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _health = evt.Health;
                _maxHealth = evt.MaxHealth;
            }
        }

        private void OnEnginePrompt(EnginePromptLocalEvent evt)
        {
            _engineInRange = evt.InRange;
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

            if (_panelOpen)
            {
                DrawInventoryPanel(hotbar);
            }
        }

        private static string GetSlotLabel(HotbarSlotView slot, int stackSize)
        {
            switch (slot.ItemType)
            {
                case HotbarItemType.Harpoon:
                    return "집게";
                case HotbarItemType.Revolver:
                    return "리볼버";
                case HotbarItemType.Resource:
                    return $"자원\n{slot.Count}/{stackSize}";
                default:
                    return string.Empty;
            }
        }

        private void DrawHotbar(ILocalHotbar hotbar)
        {
            int slotCount = hotbar.SlotCount;
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

            bool holdingResource = hotbar.GetSlot(hotbar.SelectedIndex).ItemType == HotbarItemType.Resource;
            string prompt = holdingResource
                ? "E 또는 좌클릭 — 연료 투입 (자원 1개)"
                : "E — 연료 투입 (자원 1개)";
            GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.62f, 300f, 24f),
                $"<color=yellow>{prompt}</color>");
        }

        private void DrawInventoryPanel(ILocalHotbar hotbar)
        {
            var rect = new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.5f - 150f, 380f, 230f);
            GUI.Box(rect, "인벤토리 / 캐릭터 상태 [I 닫기] — 드래그로 재배치");

            int slotCount = hotbar.SlotCount;
            float totalWidth = slotCount * SlotSize + (slotCount - 1) * SlotGap;
            float slotsX = rect.x + (rect.width - totalWidth) * 0.5f;
            float slotsY = rect.y + 36f;

            Event current = Event.current;
            for (int i = 0; i < slotCount; i++)
            {
                var slotRect = new Rect(slotsX + i * (SlotSize + SlotGap), slotsY, SlotSize, SlotSize);
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

            if (current.type == EventType.MouseUp)
            {
                // 슬롯 밖에서 놓으면 드래그 취소 (드롭 없음 — 기획서 §3.4, M2).
                _dragFromIndex = -1;
            }

            if (_dragFromIndex >= 0)
            {
                var dragRect = new Rect(current.mousePosition.x - SlotSize * 0.5f,
                    current.mousePosition.y - SlotSize * 0.5f, SlotSize, SlotSize);
                GUI.Box(dragRect, GetSlotLabel(hotbar.GetSlot(_dragFromIndex), hotbar.StackSize));
            }

            GUILayout.BeginArea(new Rect(rect.x + 16f, slotsY + SlotSize + 12f, rect.width - 32f, 110f));
            GUILayout.Label("— 캐릭터 상태 —");
            GUILayout.Label(_maxHealth > 0f ? $"체력: {_health:F0} / {_maxHealth:F0}" : "체력: -");
            GUILayout.Label("체온·허기: 이후 지역 시스템(M4)에서 추가");
            GUILayout.EndArea();
        }
    }
}
