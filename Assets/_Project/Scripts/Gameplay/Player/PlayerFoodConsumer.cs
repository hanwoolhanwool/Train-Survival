using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 음식 섭취 (M5 4차 — 요리·허기): 요리 슬롯을 든 상태의 좌클릭 = 먹기.
    /// 요리는 비연료라 엔진 투입 경로가 거부하므로 좌클릭이 겹치지 않는다 (계획 §4).
    /// 확정은 호스트: (든 칸 1개 차감 + 허기 회복 + 버프 부여)를 원자적으로 확정한다.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerHunger))]
    [RequireComponent(typeof(PlayerBuffs))]
    public sealed class PlayerFoodConsumer : NetworkBehaviour
    {
        [SerializeField] private FoodCatalog _foodCatalog;

        private PlayerHealth _health;
        private PlayerHunger _hunger;
        private PlayerBuffs _buffs;
        private HotbarController _hotbar;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _hunger = GetComponent<PlayerHunger>();
            _buffs = GetComponent<PlayerBuffs>();
            _hotbar = GetComponent<HotbarController>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _foodCatalog == null || _hotbar == null)
            {
                return;
            }

            // 사망 중·창 열림 중에는 먹지 않는다 — 입력 규약 (I 창 = 조작 정지).
            // null 관용을 두지 않는다 — 참조가 비면 게이트가 통째로 열려 사망 중 섭취가 새어 나간다(M5 4차 D5).
            if (!_health.IsAlive)
            {
                return;
            }

            if (_hotbar.IsPanelOpen
                || _hotbar.SelectedItemType != HotbarItemType.Resource
                || !_foodCatalog.TryGetEntry(_hotbar.SelectedResourceType, out _))
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                RequestEatServerRpc(_hotbar.SelectedIndex);
            }
        }

        // ── 호스트: 섭취 확정 (차감 + 허기 회복 + 버프, 원자적) ──────────────

        [Rpc(SendTo.Server)]
        private void RequestEatServerRpc(int slotIndex)
        {
            if (_foodCatalog == null || !_health.IsAlive)
            {
                return;
            }

            // 든 칸의 종류를 서버 상태로 재확인한다 — 음식이 아니면 기각 (호스트 검증 원칙).
            IResourceInventory inventory = GetComponent<IResourceInventory>();
            if (inventory == null
                || !_foodCatalog.TryGetEntry(inventory.GetResourceTypeAt(slotIndex), out FoodCatalog.Entry entry)
                || !inventory.ServerTryRemoveAt(slotIndex, 1))
            {
                return;
            }

            _hunger.ServerRestore(entry.HungerRestore);
            _buffs.ServerApplyFood(
                entry.RegenPerSecond, entry.RegenDurationSeconds,
                entry.ColdInsulationBonus, entry.WarmthDurationSeconds);
        }
    }
}
