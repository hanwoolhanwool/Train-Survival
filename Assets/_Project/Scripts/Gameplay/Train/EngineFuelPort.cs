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
    /// 기관차 엔진 연료 투입구 (기획서 §3.4) — E키 1회 = 자원 1개 투입.
    /// 투입 확정은 호스트: 요청 RPC를 받아 (개인 인벤토리 차감 + 연료 충전)을 원자적으로 확정한다
    /// (네트워크 문서 §4 — 개인 인벤토리도 호스트 권위, 복제 방지).
    /// 기관차 위 투입 지점에 배치한다 (열차 원점 고정이므로 씬 고정 위치).
    /// </summary>
    public sealed class EngineFuelPort : NetworkBehaviour
    {
        [SerializeField] private FuelSettings _fuelSettings;
        [SerializeField, Min(0.5f)] private float _interactRadius = 3f;

        private bool _localInRange;

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            Transform localPlayer = GetLocalPlayerTransform();
            if (localPlayer == null)
            {
                SetLocalInRange(false);
                return;
            }

            bool inRange = (localPlayer.position - transform.position).sqrMagnitude
                <= _interactRadius * _interactRadius;
            SetLocalInRange(inRange);

            Keyboard keyboard = Keyboard.current;
            if (inRange && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                RequestDepositServerRpc();
            }
        }

        private static Transform GetLocalPlayerTransform()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.LocalClient == null || manager.LocalClient.PlayerObject == null)
            {
                return null;
            }

            return manager.LocalClient.PlayerObject.transform;
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
        private void RequestDepositServerRpc(RpcParams rpcParams = default)
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

            IResourceInventory inventory = client.PlayerObject.GetComponent<IResourceInventory>();
            if (inventory == null || !inventory.ServerTryRemove(1))
            {
                return;
            }

            if (ServiceLocator.TryGet(out IFuelService fuel))
            {
                fuel.AddFuel(_fuelSettings.FuelPerResource);
            }
        }
    }
}
