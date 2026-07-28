using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 후미 칸 증설 포트 (§M3 — 칸 증설/연결, 기획서 §7.1) — 현재 후미 칸 뒤끝에 붙어 다니는 상호작용 지점.
    /// 근처에서 포트를 쳐다보고 E키 = 개인 인벤토리 자원으로 비용을 지불하고 온실칸 1칸을 잇는다.
    /// 확정은 호스트: 요청 RPC를 받아 (자원 차감 + 편성 증설)을 원자적으로 확정한다 (엔진 투입과 동일 패턴).
    /// Train 루트(씬 NetworkObject)의 자식으로 배치한다 — 위치는 복제 편성 상태로 전 피어가 동일하게 계산한다.
    /// </summary>
    public sealed class TrainExpansionPort : NetworkBehaviour
    {
        [SerializeField] private TrainLayoutSettings _layoutSettings;
        [SerializeField] private TrainExpansionSettings _expansionSettings;
        [SerializeField, Min(0.5f)] private float _interactRadius = 3.5f;

        [Tooltip("포트를 '쳐다봤다'고 볼 시선 정렬 하한 (카메라 전방·포트 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.7f;

        [Tooltip("포트 높이(Y) — 후미 칸 갑판 근처로 맞춘다.")]
        [SerializeField] private float _portHeight = 2.5f;

        private bool _localInRange;
        private bool _localAffordable;

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            RepositionToRear();
            UpdateLocalInteraction();
        }

        /// <summary>현재 후미(살아 붙은 마지막 칸) 뒤 연결 지점으로 이동한다 — 증설·이탈로 후미가 바뀌면 따라간다.</summary>
        private void RepositionToRear()
        {
            if (_layoutSettings == null || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            int rearIndex = -1;
            for (int i = 0; i < train.CarCount; i++)
            {
                if (train.TryGetCar(i, out CarState car) && TrainStateLogic.IsCarPresent(car))
                {
                    rearIndex = i;
                }
            }

            if (rearIndex < 0)
            {
                return;
            }

            float z = _layoutSettings.CarCenterZ(rearIndex)
                - _layoutSettings.CarLength * 0.5f
                - _layoutSettings.CouplingGap * 0.5f;
            transform.position = new Vector3(0f, _portHeight, z);
        }

        private void UpdateLocalInteraction()
        {
            NetworkObject localPlayer = GetLocalPlayerObject();
            if (localPlayer == null || _expansionSettings == null)
            {
                SetLocalPrompt(false, false);
                return;
            }

            bool inRange = (localPlayer.transform.position - transform.position).sqrMagnitude
                <= _interactRadius * _interactRadius;
            bool available = ServiceLocator.TryGet(out ITrainExpansion expansion)
                && expansion.CanAppendCar(CarType.Greenhouse);
            bool ready = inRange && available && IsLookingAtPort(localPlayer);

            int cost = _expansionSettings.GreenhouseCarCost;
            IResourceInventory inventory = localPlayer.GetComponent<IResourceInventory>();
            bool affordable = inventory != null && inventory.Count >= cost;

            SetLocalPrompt(ready, affordable);

            if (!ready || !affordable)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                RequestBuildServerRpc();
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

        /// <summary>로컬 플레이어 카메라가 포트를 향하고 있는지 — 카메라를 못 찾으면 거리만으로 폴백한다.</summary>
        private bool IsLookingAtPort(NetworkObject localPlayer)
        {
            Camera camera = localPlayer.GetComponentInChildren<Camera>();
            if (camera == null)
            {
                return true;
            }

            Vector3 toPort = transform.position - camera.transform.position;
            if (toPort.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            return Vector3.Dot(camera.transform.forward, toPort.normalized) >= _lookDotThreshold;
        }

        private void SetLocalPrompt(bool inRange, bool affordable)
        {
            if (_localInRange == inRange && _localAffordable == affordable)
            {
                return;
            }

            _localInRange = inRange;
            _localAffordable = affordable;
            int cost = _expansionSettings != null ? _expansionSettings.GreenhouseCarCost : 0;
            EventBus<ExpansionPromptLocalEvent>.Publish(new ExpansionPromptLocalEvent(inRange, cost, affordable));
        }

        // ── 호스트: 증설 확정 (자원 차감 + 편성 증설, 원자적) ──────────

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestBuildServerRpc(RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            NetworkManager manager = NetworkManager.Singleton;
            if (_expansionSettings == null || manager == null ||
                !manager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            // 서버 측 거리 검증 — 범위 밖 증설 요청은 기각한다 (호스트 검증 원칙).
            float maxDistance = _interactRadius + 1.5f;
            if ((client.PlayerObject.transform.position - transform.position).sqrMagnitude
                > maxDistance * maxDistance)
            {
                return;
            }

            if (!ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !expansion.CanAppendCar(CarType.Greenhouse))
            {
                return;
            }

            IResourceInventory inventory = client.PlayerObject.GetComponent<IResourceInventory>();
            if (inventory == null || !inventory.ServerTryRemove(_expansionSettings.GreenhouseCarCost))
            {
                return;
            }

            // 차감 후 증설이 실패하면(같은 프레임 경쟁 등) 자원을 되돌려 원자성을 지킨다.
            if (!expansion.ServerTryAppendCar(CarType.Greenhouse))
            {
                inventory.ServerTryAdd(_expansionSettings.GreenhouseCarCost);
            }
        }
    }
}
