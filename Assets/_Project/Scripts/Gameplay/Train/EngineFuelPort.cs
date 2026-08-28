using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Game.Gameplay.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 기관차 엔진 연료 투입구 (기획서 §3.4) — 자원 슬롯을 든 상태에서 E키 또는 좌클릭 1회 = 든 칸의 자원 1개 투입.
    /// 자원이 아닌 칸(집게·리볼버)을 들고 있으면 투입되지 않으며, 소모되는 칸은 항상 "현재 든 칸"이다.
    /// 투입 확정은 호스트: 요청 RPC를 받아 (개인 핫바의 지정 칸 차감 + 연료 충전)을 원자적으로 확정한다
    /// (네트워크 문서 §4 — 개인 인벤토리도 호스트 권위, 복제 방지).
    /// 기관차 위 투입 지점에 배치한다 (열차 원점 고정이므로 씬 고정 위치).
    /// </summary>
    public sealed class EngineFuelPort : NetworkBehaviour
    {
        [SerializeField] private FuelSettings _fuelSettings;
        [SerializeField] private ResourceCatalog _catalog;
        [SerializeField, Min(0.5f)] private float _interactRadius = 3f;

        [Tooltip("투입구를 '쳐다봤다'고 볼 시선 정렬 하한 (카메라 전방·투입구 방향 내적). 1에 가까울수록 정면만 인정.")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.8f;

        private bool _localInRange;

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            NetworkObject localPlayer = GetLocalPlayerObject();
            if (localPlayer == null)
            {
                SetLocalInRange(false);
                return;
            }

            Vector3 port = transform.position;
            float sqrDistance = (localPlayer.transform.position - port).sqrMagnitude;
            bool inRange = sqrDistance <= _interactRadius * _interactRadius;
            // 근처 + 투입구를 쳐다봤을 때만 안내·투입한다 (지나가기만 해도 뜨던 안내 제거).
            float lookDot = Player.LocalInteraction.GetLookDot(localPlayer, port);
            bool ready = inRange && lookDot >= _lookDotThreshold;

            // 상호작용 대상 중재 — 기관차 고정 제작 지점과 투입구는 거의 같은 자리라 늘 함께 성립한다.
            if (ready)
            {
                Player.InteractionArbiter.Submit(Player.InteractionSource.EngineFuel, lookDot, sqrDistance);
            }

            // IsFocused를 먼저 물어 프레임을 넘긴다 — 단락되면 중재가 갱신되지 않는다.
            bool focused = Player.InteractionArbiter.IsFocused(Player.InteractionSource.EngineFuel) && ready;
            SetLocalInRange(focused);

            if (!focused)
            {
                return;
            }

            // 자원 슬롯을 든 상태에서만 투입한다 — 무기 슬롯이면 발사·조작과 겹치지 않고, 어떤 칸이 소모될지 명확하다.
            // 발열량 0 이하(화약 원료·탄약)는 연료가 아니므로 로컬에서 먼저 거른다 (서버도 재검증).
            HotbarController hotbar = localPlayer.GetComponent<HotbarController>();
            if (hotbar == null || hotbar.IsPanelOpen ||
                hotbar.SelectedItemType != HotbarItemType.Resource ||
                GetFuelValue(hotbar.SelectedResourceType) <= 0f)
            {
                return;
            }

            int selectedSlot = hotbar.SelectedIndex;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                RequestDepositServerRpc(selectedSlot);
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                RequestDepositServerRpc(selectedSlot);
            }
        }

        private static NetworkObject GetLocalPlayerObject()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.LocalClient == null)
            {
                return null;
            }

            return manager.LocalClient.PlayerObject;
        }

        private void SetLocalInRange(bool inRange)
        {
            if (_localInRange != inRange)
            {
                _localInRange = inRange;
                EventBus<EnginePromptLocalEvent>.Publish(new EnginePromptLocalEvent(inRange));
            }
        }

        // ── 호스트: 투입 확정 (인벤토리 차감 + 연료 충전, 원자적) ──────────

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestDepositServerRpc(int slotIndex, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            NetworkManager manager = NetworkManager.Singleton;
            if (_fuelSettings == null || manager == null ||
                !manager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            // 서버 측 거리 검증 — 범위 밖 투입 요청은 기각한다 (호스트 검증 원칙).
            float maxDistance = _interactRadius + 1.5f;
            if ((client.PlayerObject.transform.position - transform.position).sqrMagnitude
                > maxDistance * maxDistance)
            {
                return;
            }

            // 든 칸의 자원만 소모한다 — 그 칸이 자원이 아니면 ServerTryRemoveAt이 실패한다 (호스트 검증).
            // 발열량은 차감 전에 종류로 확정한다 — 발열 0 이하(비연료)는 투입 자체를 기각한다.
            IResourceInventory inventory = client.PlayerObject.GetComponent<IResourceInventory>();
            if (inventory == null)
            {
                return;
            }

            float fuelValue = GetFuelValue(inventory.GetResourceTypeAt(slotIndex));
            if (fuelValue <= 0f || !inventory.ServerTryRemoveAt(slotIndex, 1))
            {
                return;
            }

            if (ServiceLocator.TryGet(out IFuelService fuel))
            {
                fuel.AddFuel(fuelValue);
            }

            // 투입이 성사된 사실 자체를 연출용으로 되돌려준다 — 잔량 복제만으로는 이 순간을 못 짚는다
            // (호스트가 매 프레임 깎으므로 한 프레임의 증가가 소모와 상쇄된다).
            PlayDepositEffectRpc(fuelValue);
        }

        /// <summary>
        /// 투입 성사 연출 — 모든 피어의 화구가 같은 순간 타오른다. 게임 상태는 담지도 바꾸지도 않으므로
        /// 유실돼도 연료 확정에는 영향이 없다.
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        private void PlayDepositEffectRpc(float fuelValue)
        {
            EventBus<EngineFuelDepositedEvent>.Publish(new EngineFuelDepositedEvent(fuelValue));
        }

        /// <summary>종류의 발열량 — 카탈로그 미배선 시 기존 고정값(FuelPerResource)으로 폴백한다.</summary>
        private float GetFuelValue(ResourceType type)
        {
            if (_catalog == null)
            {
                return type == ResourceType.None ? 0f : _fuelSettings.FuelPerResource;
            }

            return _catalog.GetFuelValue(type);
        }
    }
}
