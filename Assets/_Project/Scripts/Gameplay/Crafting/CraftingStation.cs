using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Crafting
{
    /// <summary>
    /// 기관차 고정 제작 지점 (M5 1차 골격) — 근접 + 시선에서 E키로 제작 창을 연다
    /// (<see cref="Game.Gameplay.Train.EngineFuelPort"/>와 같은 상호작용 규약).
    /// 제작 확정은 호스트: 요청 RPC를 받아 거리 재검증 후 (재료 차감 + 산출 지급)을 원자적으로 확정한다.
    /// 제작대 건축물이 생기는 차수(M5 3차)에는 이 트리거 표면만 건축물로 옮긴다 — 레시피·확정 경로 재사용.
    /// </summary>
    public sealed class CraftingStation : NetworkBehaviour, ICraftingStation
    {
        [SerializeField] private RecipeCatalog _recipes;
        [SerializeField, Min(0.5f)] private float _interactRadius = 3f;

        [Tooltip("제작대를 '쳐다봤다'고 볼 시선 정렬 하한 (카메라 전방·제작대 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.8f;

        private bool _localInRange;
        private bool _panelOpen;

        public int RecipeCount => _recipes == null ? 0 : _recipes.Count;

        public bool IsLocalPlayerInRange => _localInRange;

        public CraftingRecipe GetRecipe(int index)
        {
            return _recipes == null ? null : _recipes.GetRecipe(index);
        }

        public override void OnNetworkSpawn()
        {
            if (!ServiceLocator.IsRegistered<ICraftingStation>())
            {
                ServiceLocator.Register<ICraftingStation>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ServiceLocator.TryGet(out ICraftingStation station) && ReferenceEquals(station, this))
            {
                ServiceLocator.Unregister<ICraftingStation>();
            }

            SetPanelOpen(false);
        }

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
                SetPanelOpen(false);
                return;
            }

            bool inRange = (localPlayer.transform.position - transform.position).sqrMagnitude
                <= _interactRadius * _interactRadius;
            bool ready = inRange && IsLookingAtStation(localPlayer);

            // 범위를 벗어나면 창을 닫는다 — 열림 상태 안내는 창이 대신하므로 프롬프트는 끈다.
            if (!inRange)
            {
                SetPanelOpen(false);
            }

            SetLocalInRange(ready && !_panelOpen);

            HotbarController hotbar = localPlayer.GetComponent<HotbarController>();
            bool otherUiOpen = hotbar != null && hotbar.IsPanelOpen && !_panelOpen;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame || otherUiOpen)
            {
                return;
            }

            if (_panelOpen)
            {
                SetPanelOpen(false);
            }
            else if (ready)
            {
                SetPanelOpen(true);
            }
        }

        public void RequestCraft(int recipeIndex)
        {
            if (_panelOpen && recipeIndex >= 0 && recipeIndex < RecipeCount)
            {
                RequestCraftServerRpc(recipeIndex);
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

        /// <summary>로컬 플레이어 카메라가 제작대를 향하고 있는지 — 카메라를 못 찾으면 거리만으로 폴백한다.</summary>
        private bool IsLookingAtStation(NetworkObject localPlayer)
        {
            Camera camera = localPlayer.GetComponentInChildren<Camera>();
            if (camera == null)
            {
                return true;
            }

            Vector3 toStation = transform.position - camera.transform.position;
            if (toStation.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            return Vector3.Dot(camera.transform.forward, toStation.normalized) >= _lookDotThreshold;
        }

        private void SetLocalInRange(bool inRange)
        {
            if (_localInRange != inRange)
            {
                _localInRange = inRange;
                EventBus<CraftingPromptLocalEvent>.Publish(new CraftingPromptLocalEvent(inRange));
            }
        }

        private void SetPanelOpen(bool open)
        {
            if (_panelOpen != open)
            {
                _panelOpen = open;
                EventBus<CraftingPanelToggledLocalEvent>.Publish(new CraftingPanelToggledLocalEvent(open));
            }
        }

        // ── 호스트: 제작 확정 (재료 차감 + 산출 지급, 원자적) ──────────────

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestCraftServerRpc(int recipeIndex, RpcParams rpcParams = default)
        {
            CraftingRecipe recipe = GetRecipe(recipeIndex);
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            NetworkManager manager = NetworkManager.Singleton;
            if (recipe == null || manager == null ||
                !manager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            // 서버 측 거리 검증 — 범위 밖 제작 요청은 기각한다 (호스트 검증 원칙).
            float maxDistance = _interactRadius + 1.5f;
            if ((client.PlayerObject.transform.position - transform.position).sqrMagnitude
                > maxDistance * maxDistance)
            {
                return;
            }

            PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.ServerTryCraft(recipe);
            }
        }
    }
}
