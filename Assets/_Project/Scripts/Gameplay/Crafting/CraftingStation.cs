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
        [SerializeField, Min(0.5f)] private float _interactRadius = 3f;

        [Tooltip("제작대를 '쳐다봤다'고 볼 시선 정렬 하한 (카메라 전방·제작대 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.8f;

        /// <summary>
        /// 판정 대상 지점 종류 — <see cref="CraftStationKind"/>에 종류를 늘리면 여기 한 줄만 추가한다
        /// (M7 3차 정수기 편입 시 일반화). 매 프레임 종류별 근접을 갱신하고, 레시피 필터가 조회한다.
        /// </summary>
        private static readonly CraftStationKind[] StationKinds =
        {
            CraftStationKind.Workbench,
            CraftStationKind.Campfire,
            CraftStationKind.Purifier,
        };

        private readonly bool[] _stationInRange = new bool[StationKinds.Length];

        private bool _localInRange;
        private bool _panelOpen;

        // 로컬 플레이어의 집게 등급·상한 (M5 5차) — 승급 레시피 목록 필터에 쓴다. 0 = 아직 모름.
        private int _localHarpoonTier;
        private int _localHarpoonMaxTier;

        public int RecipeCount => _recipes == null ? 0 : _recipes.Count;

        public bool IsLocalPlayerInRange => _localInRange;

        public CraftingRecipe GetRecipe(int index)
        {
            return _recipes == null ? null : _recipes.GetRecipe(index);
        }

        /// <summary>
        /// 레시피의 요구 지점(제작대/화덕)이 로컬 플레이어 범위 안에 있는가 (M5 4차).
        /// 집게 승급 레시피는 여기에 등급 조건이 하나 더 붙는다 (M5 5차) — 이미 가진 등급·건너뛴
        /// 등급은 목록에서 빠진다. 서버 확정도 같은 조건을 다시 본다 (판정이 갈리지 않는다).
        /// </summary>
        public bool IsRecipeAvailable(int index)
        {
            CraftingRecipe recipe = GetRecipe(index);
            if (recipe == null)
            {
                return false;
            }

            bool stationReady = IsStationInRange(recipe.Station);
            if (!stationReady || !recipe.IsHarpoonTierOutput)
            {
                return stationReady;
            }

            return IsNextHarpoonTier(recipe, _localHarpoonTier, _localHarpoonMaxTier);
        }

        /// <summary>지점 종류가 로컬 플레이어 근처인가 — 미등재 종류는 항상 false(레시피가 목록에서 빠진다).</summary>
        private bool IsStationInRange(CraftStationKind kind)
        {
            for (int i = 0; i < StationKinds.Length; i++)
            {
                if (StationKinds[i] == kind)
                {
                    return _stationInRange[i];
                }
            }

            return false;
        }

        /// <summary>승급이 성립하는가 — 목표가 "현재 + 1"이고 데이터 상한 안일 때만 (단계 건너뛰기 금지).</summary>
        private static bool IsNextHarpoonTier(CraftingRecipe recipe, int currentTier, int maxTier)
        {
            return currentTier > 0
                && recipe.OutputHarpoonTier == currentTier + 1
                && recipe.OutputHarpoonTier <= maxTier;
        }

        public override void OnNetworkSpawn()
        {
            if (!ServiceLocator.IsRegistered<ICraftingStation>())
            {
                ServiceLocator.Register<ICraftingStation>(this);
            }

            EventBus<Player.UiCloseRequestedLocalEvent>.Subscribe(OnUiCloseRequested);
        }

        public override void OnNetworkDespawn()
        {
            EventBus<Player.UiCloseRequestedLocalEvent>.Unsubscribe(OnUiCloseRequested);

            if (ServiceLocator.TryGet(out ICraftingStation station) && ReferenceEquals(station, this))
            {
                ServiceLocator.Unregister<ICraftingStation>();
            }

            SetPanelOpen(false);
        }

        /// <summary>Esc의 닫기 요청 (M5 4차 — Esc 우선순위): 열린 제작 창을 닫는다.</summary>
        private void OnUiCloseRequested(Player.UiCloseRequestedLocalEvent evt)
        {
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
                System.Array.Clear(_stationInRange, 0, _stationInRange.Length);
                _localHarpoonTier = 0;
                _localHarpoonMaxTier = 0;
                SetLocalInRange(false);
                SetPanelOpen(false);
                return;
            }

            // 승급 레시피 필터의 입력 (M5 5차) — 복제 값이라 로컬 조회로 충분하다.
            var tierHolder = localPlayer.GetComponent<IHarpoonTierHolder>();
            _localHarpoonTier = tierHolder != null ? tierHolder.Tier : 0;
            _localHarpoonMaxTier = tierHolder != null ? tierHolder.MaxTier : 0;

            // 지점 종류별 판정 (M5 4차) — 어느 한 종류라도 근처면 창이 열리고,
            // 종류별 범위 플래그는 레시피 필터(IsRecipeAvailable)가 쓴다.
            Vector3 playerPosition = localPlayer.transform.position;
            bool inRange = false;
            bool ready = false;
            for (int i = 0; i < StationKinds.Length; i++)
            {
                // |= 는 단락하지 않는다 — 앞 종류가 참이어도 모든 종류의 근접 플래그가 갱신된다.
                ready |= ResolveStation(localPlayer, playerPosition, StationKinds[i], out bool near);
                _stationInRange[i] = near;
                inRange |= near;
            }

            // 범위를 벗어나면 창을 닫는다 — 열림 상태 안내는 창이 대신하므로 프롬프트는 끈다.
            if (!inRange)
            {
                SetPanelOpen(false);
            }

            SetLocalInRange(ready && !_panelOpen);

            // 인벤토리 창은 제작을 막지 않는다 (M7 3차 검증 개선) — 제작 창과 함께 열리는 짝이다.
            // 막는 것은 개인 인벤토리를 이미 품은 창(창고·보따리)과 세션 메뉴뿐이다.
            HotbarController hotbar = localPlayer.GetComponent<HotbarController>();
            bool otherUiOpen = hotbar != null && hotbar.IsCraftBlockingPanelOpen;

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

        /// <summary>
        /// 제작 지점 종류 → 그 지점을 제공하는 건축물 종류. 제작대만 기관차 고정 지점이 추가로 유효하다
        /// (<see cref="TryGetNearestCraftPoint"/>) — 나머지는 건축물이 있어야만 성립한다.
        /// </summary>
        private static Train.StructureKind ResolveStructureKind(CraftStationKind kind)
        {
            switch (kind)
            {
                case CraftStationKind.Campfire:
                    return Train.StructureKind.Campfire;
                case CraftStationKind.Purifier:
                    return Train.StructureKind.Purifier;
                default:
                    return Train.StructureKind.Workbench;
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
        /// 위치에서 가장 가까운 유효 제작 지점 (M5 3차 — 제작 지점 다중, 건축 개편 1차 — 결정 ⑨:
        /// 판정 대상 = 그리드 상태에서 가장 가까운 제작 계열 건축물의 <b>점유 영역 중심</b>).
        /// 제작대 종류는 기관차 고정 지점이 항상 유효하고 제작대 건축물들이 추가된다.
        /// 화덕·정수기 종류는 해당 건축물이 살아 있어야만(칸 파괴 아님 — 이탈은 허용) 유효하다.
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

            if (!ServiceLocator.TryGet(out Train.ITrainState train))
            {
                return found;
            }

            // 목록 순회·점유 중심 계산은 상태(TrainState)가 맡는다 — 창고 접근과 같은 조회를 쓴다
            // (건축 개편 마무리 패스: "가장 가까운 그것"이 칸당 1개 전제를 대신하는 판정 기준).
            // 제작대는 기관차 고정 지점과 경쟁하므로 더 가까울 때만 이긴다.
            if (train.TryGetNearestStructure(ResolveStructureKind(kind), position, out _, out Vector3 nearest)
                && (position - nearest).sqrMagnitude < bestSqr)
            {
                point = nearest;
                found = true;
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
            if (inventory == null)
            {
                return;
            }

            if (recipe.IsHarpoonTierOutput)
            {
                ServerTryUpgradeHarpoon(client.PlayerObject, inventory, recipe);
                return;
            }

            inventory.ServerTryCraft(recipe);
        }

        /// <summary>
        /// 집게 승급 확정 (M5 5차) — ① 목표 등급이 현재 + 1인지 재검증 → ② 재료 소모 →
        /// ③ 등급 상승. 어느 하나라도 실패하면 아무것도 반영되지 않는다 (재료 보존 원자 규약 —
        /// 소모는 복사본 위에서만 이뤄지고 성공했을 때만 되쓰인다).
        /// </summary>
        private static void ServerTryUpgradeHarpoon(
            NetworkObject playerObject, PlayerInventory inventory, CraftingRecipe recipe)
        {
            var holder = playerObject.GetComponent<IHarpoonTierHolder>();
            if (holder == null || !IsNextHarpoonTier(recipe, holder.Tier, holder.MaxTier))
            {
                return;
            }

            // 보유 게이트 (M6 검증 후속, 2026-08-13 사용자 결정 ⓐ) — 등급은 플레이어 컴포넌트에
            // 있어 집게를 창고에 맡긴 채로도 승급이 성립하던 결함(M5 5차부터). 핫바에 집게
            // 아이템이 실제로 있을 때만 확정한다. UI(CraftingHud)도 같은 조건으로 비활성화한다.
            if (!HotbarLogic.ContainsItem(inventory.ServerCopySlotViews(), HotbarItemType.Harpoon))
            {
                return;
            }

            if (!inventory.ServerTryConsume(recipe))
            {
                return;
            }

            holder.ServerSetTier(recipe.OutputHarpoonTier);
        }
    }
}
