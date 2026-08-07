using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Crafting
{
    /// <summary>
    /// 제작 서비스 — 레시피 목록·요청 RPC·확정 경로의 단일 소유자 (M5 1차 골격).
    /// 유효 제작 지점은 다중이다 (M5 3차 — "제작 서비스 단일 + 제작 지점 다중"):
    /// 기관차 고정 지점(이 오브젝트 위치 — 시작 직후 건자재 없이도 탄약 루프가 성립해야 한다)
    /// + 제작대 건축물(<see cref="Train.StructureKind.Workbench"/>)이 살아 있는 칸들.
    /// 근접 + 시선에서 E키로 제작 창을 열고, 확정은 호스트: 거리 재검증 후
    /// (재료 차감 + 산출 지급)을 원자적으로 확정한다. 로컬 판정과 서버 재검증이
    /// 같은 지점 산출 함수를 쓴다 — 판정이 갈리지 않는다.
    /// </summary>
    public sealed class CraftingStation : NetworkBehaviour, ICraftingStation
    {
        [SerializeField] private RecipeCatalog _recipes;
        [SerializeField] private Train.TrainLayoutSettings _layoutSettings;
        [SerializeField, Min(0.5f)] private float _interactRadius = 3f;

        [Tooltip("제작대를 '쳐다봤다'고 볼 시선 정렬 하한 (카메라 전방·제작대 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.8f;

        private bool _localInRange;
        private bool _panelOpen;
        private bool _workbenchInRange;
        private bool _campfireInRange;

        public int RecipeCount => _recipes == null ? 0 : _recipes.Count;

        public bool IsLocalPlayerInRange => _localInRange;

        public CraftingRecipe GetRecipe(int index)
        {
            return _recipes == null ? null : _recipes.GetRecipe(index);
        }

        /// <summary>레시피의 요구 지점(제작대/화덕)이 로컬 플레이어 범위 안에 있는가 (M5 4차).</summary>
        public bool IsRecipeAvailable(int index)
        {
            CraftingRecipe recipe = GetRecipe(index);
            if (recipe == null)
            {
                return false;
            }

            return recipe.Station == CraftStationKind.Campfire ? _campfireInRange : _workbenchInRange;
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

            NetworkObject localPlayer = Player.LocalInteraction.GetLocalPlayerObject();
            if (localPlayer == null)
            {
                _workbenchInRange = false;
                _campfireInRange = false;
                SetLocalInRange(false);
                SetPanelOpen(false);
                return;
            }

            // 지점 종류별 판정 (M5 4차) — 어느 한 종류라도 근처면 창이 열리고,
            // 종류별 범위 플래그는 레시피 필터(IsRecipeAvailable)가 쓴다.
            Vector3 playerPosition = localPlayer.transform.position;
            bool workbenchReady = ResolveStation(
                localPlayer, playerPosition, CraftStationKind.Workbench, out _workbenchInRange);
            bool campfireReady = ResolveStation(
                localPlayer, playerPosition, CraftStationKind.Campfire, out _campfireInRange);

            bool inRange = _workbenchInRange || _campfireInRange;
            bool ready = workbenchReady || campfireReady;

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

        /// <summary>지점 종류 하나의 로컬 판정 — 근접 플래그를 내고, 근접 + 시선이면 true(창 열기 가능).</summary>
        private bool ResolveStation(
            NetworkObject localPlayer, Vector3 position, CraftStationKind kind, out bool inRange)
        {
            inRange = TryGetNearestCraftPoint(position, kind, out Vector3 point)
                && Player.LocalInteraction.IsWithinRange(localPlayer, point, _interactRadius);

            return inRange && Player.LocalInteraction.IsLookingAt(localPlayer, point, _lookDotThreshold);
        }

        /// <summary>
        /// 위치에서 가장 가까운 유효 제작 지점 (M5 3차 — 제작 지점 다중, 4차 — 종류 분리).
        /// 제작대 종류는 기관차 고정 지점이 항상 유효하고 제작대 건축물 칸이 추가된다.
        /// 화덕 종류는 화덕 건축물이 살아 있는 칸(파괴 아님 — 이탈은 허용)만 유효하다.
        /// 로컬 근접·시선 판정과 서버 거리 재검증이 이 함수 하나를 공유한다.
        /// </summary>
        private bool TryGetNearestCraftPoint(Vector3 position, CraftStationKind kind, out Vector3 point)
        {
            bool found = false;
            point = default;
            float bestSqr = float.PositiveInfinity;

            if (kind == CraftStationKind.Workbench)
            {
                // 기관차 고정 지점 — 시작 직후(건자재 0)에도 탄약 루프가 성립해야 한다.
                point = transform.position;
                bestSqr = (position - point).sqrMagnitude;
                found = true;
            }

            if (_layoutSettings == null || !ServiceLocator.TryGet(out Train.ITrainState train))
            {
                return found;
            }

            Train.StructureKind structureKind = kind == CraftStationKind.Campfire
                ? Train.StructureKind.Campfire
                : Train.StructureKind.Workbench;

            for (int i = 0; i < train.CarCount; i++)
            {
                if (!train.TryGetStructure(i, out Train.StructureState structure)
                    || !structure.Present || structure.Kind != structureKind
                    || structure.Health <= 0f
                    || !train.TryGetCar(i, out Train.CarState car) || car.Health <= 0f)
                {
                    continue;
                }

                float z = _layoutSettings.CarCenterZ(i, train.GetEjectOffset(i));
                var carPoint = new Vector3(0f, _layoutSettings.DeckHeight, z);
                float sqr = (position - carPoint).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    point = carPoint;
                    found = true;
                }
            }

            return found;
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

            // 서버 측 거리 검증 — 로컬 판정과 같은 지점 산출로, 레시피의 요구 지점 종류 기준
            // 최근접 유효 지점을 본다 (요리를 기관차·제작대에서 확정받을 수 없다 — M5 4차).
            Vector3 playerPosition = client.PlayerObject.transform.position;
            if (!TryGetNearestCraftPoint(playerPosition, recipe.Station, out Vector3 craftPoint))
            {
                return;
            }

            float maxDistance = _interactRadius + 1.5f;
            if ((playerPosition - craftPoint).sqrMagnitude > maxDistance * maxDistance)
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
