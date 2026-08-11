using Game.Core.Services;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 보따리 아이템 사용 (M5 8차 — 1차 검증 개선 2): 보따리 칸을 든 상태의 좌클릭 = 풀기.
    /// 확정은 호스트 — 내용물이 전부 들어가면(보따리 칸 자체도 빈 칸으로 계산) 풀어서 수납하고,
    /// 여전히 부족하면 <b>발밑에 보따리 오브젝트로 내려놓는다</b> (갑판 = 휴지 / 지상 = 월드
    /// 바인딩 — 그랩 해제 낙하와 같은 판정). 어느 쪽이든 아이템 칸은 비워지고 내용물은 보존된다.
    /// 입력 규약은 음식 섭취(<see cref="PlayerFoodConsumer"/>)와 동일 — 사망·창 열림 중 차단.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class BundleItemController : NetworkBehaviour
    {
        private PlayerHealth _health;
        private HotbarController _hotbar;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _hotbar = GetComponent<HotbarController>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _hotbar == null || !_health.IsAlive)
            {
                return;
            }

            if (_hotbar.IsPanelOpen || _hotbar.SelectedItemType != HotbarItemType.Bundle)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                RequestUnpackServerRpc(_hotbar.SelectedIndex);
            }
        }

        // ── 호스트: 풀기 확정 (수납 또는 발밑 재설치, 원자적) ──────────────

        [Rpc(SendTo.Server)]
        private void RequestUnpackServerRpc(int slotIndex)
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            if (inventory == null || !_health.IsAlive
                || !ServiceLocator.TryGet(out World.IBundleItemStore store))
            {
                return;
            }

            // 든 칸의 종류를 서버 상태로 재확인한다 — 보따리가 아니면 기각 (호스트 검증 원칙).
            HotbarSlotView[] slots = inventory.ServerCopySlotViews();
            if (slotIndex < 0 || slotIndex >= slots.Length
                || slots[slotIndex].ItemType != HotbarItemType.Bundle)
            {
                return;
            }

            byte id = (byte)slots[slotIndex].Count;
            if (!store.ServerTryPeek(id, out HotbarSlotView[] contents))
            {
                // 보관물이 없는 고아 슬롯 — 칸만 정리한다 (세션 한정 보관의 안전망).
                slots[slotIndex] = new HotbarSlotView(HotbarItemType.None, 0);
                inventory.ServerApplySlotViews(slots);
                return;
            }

            // 1안 — 보따리 칸을 먼저 비운 복사본에 풀기 (그 칸도 수납 공간이 된다).
            slots[slotIndex] = new HotbarSlotView(HotbarItemType.None, 0);
            if (HotbarLogic.TryAddAll(slots, contents, inventory.GetMaxStack))
            {
                inventory.ServerApplySlotViews(slots);
                store.ServerRemove(id);
                return;
            }

            // 2안 — 여전히 부족: 발밑에 보따리 오브젝트로 내려놓는다 (내용물 보존).
            if (ServiceLocator.TryGet(out World.IStorageBundleSpawner spawner)
                && spawner.ServerSpawnResting(contents, transform.position))
            {
                HotbarSlotView[] fresh = inventory.ServerCopySlotViews();
                if (slotIndex < fresh.Length && fresh[slotIndex].ItemType == HotbarItemType.Bundle)
                {
                    fresh[slotIndex] = new HotbarSlotView(HotbarItemType.None, 0);
                    inventory.ServerApplySlotViews(fresh);
                }

                store.ServerRemove(id);
            }
        }
    }
}
