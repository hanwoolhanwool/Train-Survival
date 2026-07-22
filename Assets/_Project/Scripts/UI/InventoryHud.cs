using Game.Core.Events;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using Game.Gameplay.Train;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// 개인 인벤토리 HUD (기획서 §3.4) — 화면 중앙 하단 핫바 5칸 + I키 인벤토리/캐릭터 상태 창 +
    /// 엔진 투입 안내. UI는 상태를 소유하지 않는다: 권위/로컬 표현 이벤트 구독으로 갱신만 한다.
    /// I키는 창 표시 토글일 뿐이므로 UI 계층에서 읽는다.
    /// </summary>
    public sealed class InventoryHud : MonoBehaviour
    {
        private const float SlotSize = 52f;
        private const float SlotGap = 6f;

        private int _count;
        private int _capacity;
        private int _slotCount = 5;
        private int _stackSize = 1;
        private float _health;
        private float _maxHealth;
        private bool _engineInRange;
        private bool _panelOpen;

        private void OnEnable()
        {
            EventBus<InventoryChangedEvent>.Subscribe(OnInventoryChanged);
            EventBus<PlayerHealthChangedEvent>.Subscribe(OnPlayerHealthChanged);
            EventBus<EnginePromptLocalEvent>.Subscribe(OnEnginePrompt);
        }

        private void OnDisable()
        {
            EventBus<InventoryChangedEvent>.Unsubscribe(OnInventoryChanged);
            EventBus<PlayerHealthChangedEvent>.Unsubscribe(OnPlayerHealthChanged);
            EventBus<EnginePromptLocalEvent>.Unsubscribe(OnEnginePrompt);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            if (evt.IsLocalPlayer)
            {
                _count = evt.Count;
                _capacity = evt.Capacity;
                _slotCount = Mathf.Max(1, evt.SlotCount);
                _stackSize = Mathf.Max(1, evt.StackSize);
            }
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
            }
        }

        private void OnGUI()
        {
            DrawHotbar();
            DrawEnginePrompt();

            if (_panelOpen)
            {
                DrawInventoryPanel();
            }
        }

        private void DrawHotbar()
        {
            float totalWidth = _slotCount * SlotSize + (_slotCount - 1) * SlotGap;
            float startX = (Screen.width - totalWidth) * 0.5f;
            float y = Screen.height - SlotSize - 16f;

            for (int i = 0; i < _slotCount; i++)
            {
                var rect = new Rect(startX + i * (SlotSize + SlotGap), y, SlotSize, SlotSize);
                int fill = InventoryMath.GetSlotFill(_count, i, _stackSize);
                GUI.Box(rect, fill > 0 ? $"자원\n{fill}/{_stackSize}" : string.Empty);
            }
        }

        private void DrawEnginePrompt()
        {
            if (_engineInRange)
            {
                string prompt = _count > 0 ? "E — 연료 투입 (자원 1개)" : "자원 없음 — 집게로 채집하라";
                GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.62f, 300f, 24f),
                    $"<color=yellow>{prompt}</color>");
            }
        }

        private void DrawInventoryPanel()
        {
            var rect = new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f - 140f, 360f, 200f);
            GUI.Box(rect, "인벤토리 / 캐릭터 상태 [I 닫기]");

            GUILayout.BeginArea(new Rect(rect.x + 16f, rect.y + 32f, rect.width - 32f, rect.height - 48f));
            GUILayout.Label($"자원: {_count} / {_capacity}  (슬롯 {_slotCount}칸 × 스택 {_stackSize})");
            GUILayout.Space(8f);
            GUILayout.Label("— 캐릭터 상태 —");
            GUILayout.Label(_maxHealth > 0f ? $"체력: {_health:F0} / {_maxHealth:F0}" : "체력: -");
            GUILayout.Label("체온·허기: 이후 지역 시스템(M4)에서 추가");
            GUILayout.EndArea();
        }
    }
}
