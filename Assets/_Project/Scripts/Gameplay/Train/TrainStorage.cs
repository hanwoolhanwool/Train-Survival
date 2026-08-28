using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 공유 창고 저장고 — 호스트 권위 (M5 3차, 권위 분담표: 공유 인벤토리 = 호스트 요청→승인).
    /// 건축 개편 2차 (§2.8, 결정 ⑦): 저장 블록 = <b>건축물 Id</b>. 창고 건축물마다 독립 블록을
    /// 가지되 슬롯은 단일 평탄 NetworkList 하나에 담는다 — 블록 i의 슬롯 = i × 창고 슬롯 수 +
    /// 칸 내 슬롯, 소유자는 _blockOwners[i]가 담보한다. 설치 시 블록 append, 파괴·철거 시
    /// 배출 후 swap-remove. 칸 이탈은 해제가 아니라 블록이 그대로 남는다 (재결합 보존 규약).
    /// 상호작용은 창고 실물의 점유 중심 기준 근접 + 시선 + E키 토글
    /// (<see cref="Game.Gameplay.Crafting.CraftingStation"/> 규약).
    /// Train 루트(TrainState와 같은 GO)에 1개 배치한다.
    /// </summary>
    public sealed class TrainStorage : NetworkBehaviour, ITrainStorage
    {
        [SerializeField] private TrainLayoutSettings _layoutSettings;
        [SerializeField] private ResourceCatalog _catalog;

        [Tooltip("건축물 카탈로그 — '공유 저장 블록을 갖는 종류'를 데이터로 판정한다 (2차 §2.8). " +
            "창고 계열 종류가 늘어도 이 스크립트는 수정하지 않는다.")]
        [SerializeField] private StructureCatalog _structureCatalog;

        [Tooltip("창고 하나의 저장 슬롯 수.")]
        [SerializeField, Min(1)] private int _slotsPerStorage = 10;

        [SerializeField, Min(0.5f)] private float _interactRadius = 3f;

        [Tooltip("창고를 '쳐다봤다'고 볼 시선 정렬 하한 (카메라 전방·창고 실물 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.8f;

        // 블록 i의 소유 건축물 Id — 슬롯 오프셋 규약과 swap-remove의 진실 (건축 개편 2차 §2.8).
        private readonly NetworkList<ushort> _blockOwners = new NetworkList<ushort>();

        private readonly NetworkList<NetworkSlot> _slots = new NetworkList<NetworkSlot>();

        private bool _localInRange;
        private int _localInRangeStorage = -1;
        private int _openStorageId = -1;

        // QA 동시 경합 (M5 5차) — 요청 발행 후 총량을 다시 찍기까지의 대기. 음수 = 비활성.
        private float _contentionLogDelay = -1f;
        private int _contentionStorageId = -1;

        public int SlotsPerStorage => _slotsPerStorage;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // 블록은 설치 확정(ServerAllocateBlock)으로만 생긴다 — 새 판은 빈 목록에서 시작.
                _blockOwners.Clear();
                _slots.Clear();
            }

            if (!ServiceLocator.IsRegistered<ITrainStorage>())
            {
                ServiceLocator.Register<ITrainStorage>(this);
            }

            EventBus<UiCloseRequestedLocalEvent>.Subscribe(OnUiCloseRequested);
        }

        public override void OnNetworkDespawn()
        {
            EventBus<UiCloseRequestedLocalEvent>.Unsubscribe(OnUiCloseRequested);

            if (ServiceLocator.TryGet(out ITrainStorage storage) && ReferenceEquals(storage, this))
            {
                ServiceLocator.Unregister<ITrainStorage>();
            }

            SetPanelOpen(-1);
        }

        /// <summary>Esc의 닫기 요청 (M5 4차 — Esc 우선순위): 열린 창고 창을 닫는다.</summary>
        private void OnUiCloseRequested(UiCloseRequestedLocalEvent evt)
        {
            SetPanelOpen(-1);
        }

        public HotbarSlotView GetSlot(int storageId, int slotIndex)
        {
            int block = FindBlock(storageId);
            if (block < 0 || slotIndex < 0 || slotIndex >= _slotsPerStorage)
            {
                return new HotbarSlotView(HotbarItemType.None, 0);
            }

            NetworkSlot slot = _slots[StorageBlockLogic.SlotOffset(block, _slotsPerStorage) + slotIndex];
            return new HotbarSlotView(slot.ItemType, slot.Count, slot.Resource);
        }

        // ── 로컬: 근접·시선 판정과 E키 토글 ────────────────────

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer && _contentionLogDelay >= 0f)
            {
                _contentionLogDelay -= Time.deltaTime;
                if (_contentionLogDelay < 0f)
                {
                    LogContentionTotals(_contentionStorageId, "확정 후");
                }
            }

            NetworkObject localPlayer = LocalInteraction.GetLocalPlayerObject();
            if (localPlayer == null)
            {
                SetLocalInRange(false, -1);
                SetPanelOpen(-1);
                return;
            }

            int nearStorage = FindNearestAliveStorage(localPlayer.transform.position, out Vector3 nearPoint);
            bool inRange = nearStorage > 0
                && LocalInteraction.IsWithinRange(localPlayer, nearPoint, _interactRadius);
            float lookDot = LocalInteraction.GetLookDot(localPlayer, nearPoint);
            bool ready = inRange && lookDot >= _lookDotThreshold;

            // 상호작용 대상 중재 — 상자와 작업대가 나란히 있어도 겨눈 쪽 하나만 안내·E키를 받는다.
            if (ready)
            {
                InteractionArbiter.Submit(InteractionSource.Storage, lookDot,
                    (localPlayer.transform.position - nearPoint).sqrMagnitude);
            }

            // IsFocused를 먼저 물어 프레임을 넘긴다 — 단락되면 중재가 갱신되지 않는다.
            bool focused = InteractionArbiter.IsFocused(InteractionSource.Storage) && ready;

            // 범위를 벗어나거나 창고가 파괴·철거되면 창을 닫는다. 한 칸에 창고가 여럿일 때도
            // 조준(최근접 + 시선) 대상이 바뀌면 그 창고 기준으로 다시 연다.
            if (_openStorageId > 0 && (!inRange || nearStorage != _openStorageId))
            {
                SetPanelOpen(-1);
            }

            SetLocalInRange(focused && _openStorageId < 0, nearStorage);

            HotbarController hotbar = localPlayer.GetComponent<HotbarController>();
            bool otherUiOpen = hotbar != null && hotbar.IsPanelOpen && _openStorageId < 0;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame || otherUiOpen)
            {
                return;
            }

            if (_openStorageId > 0)
            {
                SetPanelOpen(-1);
            }
            else if (focused)
            {
                SetPanelOpen(nearStorage);
            }
        }

        /// <summary>
        /// 살아 있는 창고 실물 중 위치에서 가장 가까운 것의 건축물 Id — 없으면 -1.
        /// 판정 지점은 점유 영역 중심 (건축 개편 — 조준한 그 창고가 열린다).
        /// 이탈 칸도 접근 가능(이탈 오프셋 반영 — 회수 작업 중 물자 회수를 막지 않는다).
        /// </summary>
        private int FindNearestAliveStorage(Vector3 position, out Vector3 nearestPoint)
        {
            nearestPoint = default;
            if (_structureCatalog == null || !ServiceLocator.TryGet(out ITrainState train))
            {
                return -1;
            }

            // 목록 순회·점유 중심 계산은 상태(TrainState)가 맡는다 — 창고·제작대·망치가 같은 조회를
            // 각자 구현하지 않게 하는 경계다 (건축 개편 마무리 패스).
            int best = -1;
            float bestSqr = float.PositiveInfinity;
            for (int kindIndex = 0; kindIndex < _structureCatalog.EntryCount; kindIndex++)
            {
                if (!_structureCatalog.TryGetKindAt(kindIndex, out StructureKind kind)
                    || !_structureCatalog.ProvidesStorageBlock(kind)
                    || !train.TryGetNearestStructure(kind, position, out StructureEntry entry, out Vector3 point))
                {
                    continue;
                }

                float sqr = (position - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = entry.Id;
                    nearestPoint = point;
                }
            }

            return best;
        }

        /// <summary>창고가 기능하는 상태인지 — 저장 블록 보유 종류 + 항목 생존 + 칸 잔존(파괴 아님. 이탈은 허용).</summary>
        private bool IsStorageAlive(ITrainState train, int storageId, out StructureEntry entry)
        {
            return train.TryGetStructureById(storageId, out entry)
                && _structureCatalog != null && _structureCatalog.ProvidesStorageBlock(entry.Kind)
                && entry.Health > 0f
                && train.TryGetCar(entry.CarIndex, out CarState car) && car.Health > 0f;
        }

        private int FindBlock(int storageId)
        {
            if (storageId <= 0)
            {
                return -1;
            }

            for (int i = 0; i < _blockOwners.Count; i++)
            {
                if (_blockOwners[i] == storageId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SetLocalInRange(bool inRange, int storageId)
        {
            if (_localInRange != inRange || (_localInRange && _localInRangeStorage != storageId))
            {
                _localInRange = inRange;
                _localInRangeStorage = inRange ? storageId : -1;
                EventBus<StoragePromptLocalEvent>.Publish(
                    new StoragePromptLocalEvent(inRange, _localInRangeStorage));
            }
        }

        private void SetPanelOpen(int storageId)
        {
            if (_openStorageId != storageId)
            {
                _openStorageId = storageId;

                // 창이 열린 동안 초점을 붙잡는다 — 열린 창 위로 제작·연료 안내가 겹치지 않게 하는 장치다.
                if (storageId > 0)
                {
                    InteractionArbiter.Capture(InteractionSource.Storage);
                }
                else
                {
                    InteractionArbiter.Release(InteractionSource.Storage);
                }

                EventBus<StoragePanelToggledLocalEvent>.Publish(
                    new StoragePanelToggledLocalEvent(storageId > 0, storageId));
            }
        }

        // ── 이동 요청 → 호스트 확정 ────────────────────

        public void RequestTransfer(int storageId, byte fromContainer, int fromIndex, byte toContainer, int toIndex)
        {
            if (_openStorageId == storageId && storageId > 0)
            {
                RequestTransferServerRpc(storageId, fromContainer, fromIndex, toContainer, toIndex);
            }
        }

        /// <summary>
        /// 이동 확정 — 창고 생존·거리를 호스트 상태로 재검증하고, 두 컨테이너의 복사본 위에서
        /// 순수 로직(<see cref="StorageLogic"/>)으로 판정한 뒤 성공 시에만 변경 블록을 되쓴다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestTransferServerRpc(int storageId, byte fromContainer, int fromIndex,
            byte toContainer, int toIndex, RpcParams rpcParams = default)
        {
            if (_layoutSettings == null || !ServiceLocator.TryGet(out ITrainState train)
                || !IsStorageAlive(train, storageId, out StructureEntry storageEntry)
                || FindBlock(storageId) < 0)
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (manager == null
                || !manager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return;
            }

            // 서버 측 거리 검증 — 로컬 판정과 같은 지점(창고 실물의 점유 영역 중심)을 쓴다.
            train.TryGetStructureCenter(storageEntry.Id, out Vector3 point);
            float maxDistance = _interactRadius + 1.5f;
            if ((client.PlayerObject.transform.position - point).sqrMagnitude > maxDistance * maxDistance)
            {
                return;
            }

            PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            HotbarSlotView[] inventorySlots = inventory.ServerCopySlotViews();
            HotbarSlotView[] storageSlots = CopyBlockSlots(storageId);

            HotbarSlotView[] from = SelectContainer(fromContainer, inventorySlots, storageSlots);
            HotbarSlotView[] to = SelectContainer(toContainer, inventorySlots, storageSlots);
            if (from == null || to == null
                || fromIndex < 0 || fromIndex >= from.Length)
            {
                return;
            }

            // 보따리 아이템 특례 (M5 8차 2차 — R3 후속 요청): 인벤토리의 보따리를 창고로 옮기면
            // 창고에 <b>전부 들어갈 때</b> 풀어서 들어간다. 대상 칸이 점유돼 있으면 그 칸이
            // 보따리 자리로 나가는 스왑 결과 기준으로도 시도한다 (3차 발견 — 꽉 찬 창고의
            // 마지막 아이템과 바꿔 넣기). 둘 다 부족하면 아래 기존 이동으로 폴백 — 보따리
            // 아이템이 창고 1칸을 점유한 채 보관된다 (내용물은 보관소 유지).
            if (fromContainer == ITrainStorage.ContainerInventory
                && toContainer == ITrainStorage.ContainerStorage
                && from[fromIndex].ItemType == HotbarItemType.Bundle
                && ServiceLocator.TryGet(out World.IBundleItemStore store)
                && store.ServerTryPeek((byte)from[fromIndex].Count, out HotbarSlotView[] bundleContents)
                && StorageLogic.TryUnpackBundle(storageSlots, toIndex, bundleContents,
                    type => _catalog != null ? _catalog.GetMaxStack(type, inventory.StackSize) : inventory.StackSize,
                    out HotbarSlotView[] unpacked, out HotbarSlotView swappedOut))
            {
                byte id = (byte)from[fromIndex].Count;
                inventorySlots[fromIndex] = swappedOut;
                inventory.ServerApplySlotViews(inventorySlots);
                ApplyBlockSlots(storageId, unpacked);
                store.ServerRemove(id);
                return;
            }

            // 병합 상한은 옮기는 쪽 종류의 스택 상한 — 서버가 카탈로그에서 푼다.
            int stackSize = _catalog != null
                ? _catalog.GetMaxStack(from[fromIndex].Resource, inventory.StackSize)
                : inventory.StackSize;

            if (!StorageLogic.TryTransfer(from, fromIndex, to, toIndex, stackSize))
            {
                return;
            }

            inventory.ServerApplySlotViews(inventorySlots);
            ApplyBlockSlots(storageId, storageSlots);
        }

        // ── 보따리 (M5 8차) — 창고 창 재사용의 컨테이너 파사드: UI는 NetworkObjectId로만 다룬다 ──

        public HotbarSlotView GetBundleSlot(ulong bundleObjectId, int slotIndex)
        {
            World.StorageBundle bundle = ResolveBundle(bundleObjectId);
            return bundle != null ? bundle.GetSlot(slotIndex) : new HotbarSlotView(HotbarItemType.None, 0);
        }

        public int GetBundleSlotCount(ulong bundleObjectId)
        {
            World.StorageBundle bundle = ResolveBundle(bundleObjectId);
            return bundle != null ? bundle.SlotCount : 0;
        }

        public void RequestBundleTransfer(
            ulong bundleObjectId, byte fromContainer, int fromIndex, byte toContainer, int toIndex)
        {
            RequestBundleTransferServerRpc(bundleObjectId, fromContainer, fromIndex, toContainer, toIndex);
        }

        /// <summary>
        /// 보따리 이동 확정 — 창고 이동과 같은 규약: 보따리 생존(비워져 회수·운반 중이면 기각)과
        /// 거리를 호스트 상태로 재검증하고, 복사본 위 순수 로직 판정 후 성공 시에만 되쓴다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestBundleTransferServerRpc(ulong bundleObjectId, byte fromContainer, int fromIndex,
            byte toContainer, int toIndex, RpcParams rpcParams = default)
        {
            World.StorageBundle bundle = ResolveBundle(bundleObjectId);
            if (bundle == null || !bundle.IsSpawned || bundle.IsClaimed)
            {
                return;
            }

            NetworkManager manager = NetworkManager.Singleton;
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            if (manager == null
                || !manager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return;
            }

            // 서버 측 거리 검증 — 보따리의 실제 위치 기준 (창고와 같은 여유 폭).
            float maxDistance = _interactRadius + 1.5f;
            if ((client.PlayerObject.transform.position - bundle.transform.position).sqrMagnitude
                > maxDistance * maxDistance)
            {
                return;
            }

            PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            HotbarSlotView[] inventorySlots = inventory.ServerCopySlotViews();
            HotbarSlotView[] bundleSlots = bundle.ServerCopySlots();

            HotbarSlotView[] from = SelectBundleContainer(fromContainer, inventorySlots, bundleSlots);
            HotbarSlotView[] to = SelectBundleContainer(toContainer, inventorySlots, bundleSlots);
            if (from == null || to == null
                || fromIndex < 0 || fromIndex >= from.Length
                || toIndex < 0 || toIndex >= to.Length)
            {
                return;
            }

            int stackSize = _catalog != null
                ? _catalog.GetMaxStack(from[fromIndex].Resource, inventory.StackSize)
                : inventory.StackSize;

            if (!StorageLogic.TryTransfer(from, fromIndex, to, toIndex, stackSize))
            {
                return;
            }

            inventory.ServerApplySlotViews(inventorySlots);
            bundle.ServerApplySlots(bundleSlots);
        }

        private static World.StorageBundle ResolveBundle(ulong bundleObjectId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (bundleObjectId == 0UL || manager == null || manager.SpawnManager == null
                || !manager.SpawnManager.SpawnedObjects.TryGetValue(bundleObjectId, out NetworkObject obj))
            {
                return null;
            }

            return obj.GetComponent<World.StorageBundle>();
        }

        private static HotbarSlotView[] SelectBundleContainer(
            byte container, HotbarSlotView[] inventorySlots, HotbarSlotView[] bundleSlots)
        {
            switch (container)
            {
                case ITrainStorage.ContainerInventory:
                    return inventorySlots;
                case ITrainStorage.ContainerBundle:
                    return bundleSlots;
                default:
                    return null;
            }
        }

        private static HotbarSlotView[] SelectContainer(
            byte container, HotbarSlotView[] inventorySlots, HotbarSlotView[] storageSlots)
        {
            switch (container)
            {
                case ITrainStorage.ContainerInventory:
                    return inventorySlots;
                case ITrainStorage.ContainerStorage:
                    return storageSlots;
                default:
                    return null;
            }
        }

        // ── QA: 동시 경합 재현 (M5 5차 — 검증 G2) ────────────────────

        /// <summary>
        /// 전 피어가 <b>같은 프레임에 같은 이동</b>(창고 슬롯 0 → 개인 빈 칸)을 요청하게 만든다.
        /// 사람이 두 피어를 같은 프레임에 조작할 수 없어 3·4차 연속 미검이던 동시 경합(G2)을
        /// 한 번의 입력으로 재현하는 QA 수단이다. 서버 검증 경로는 평소와 완전히 같다 —
        /// 창고 생존·거리 재검증과 순수 로직 판정을 그대로 거친다.
        /// 판정 기준: 두 요청 중 하나만 반영되거나 갱신된 상태 기준으로 처리되어 <b>총량이 보존</b>되는가.
        /// </summary>
        public void ServerTriggerContentionTest()
        {
            if (!IsServer)
            {
                return;
            }

            int storageId = FindAnyAliveStorage();
            if (storageId < 0)
            {
                GameLog.Info(LogCategory.Train, "QA 동시 경합: 살아 있는 창고가 없다 — 창고를 먼저 설치한다.");
                return;
            }

            // 옮길 것이 없으면 두 요청 모두 조용히 기각돼 "총량 보존"이 참이지만 아무것도 검증하지
            // 못한다 — 시작 전에 전제를 막아 헛된 통과를 만들지 않는다.
            if (GetSlot(storageId, 0).IsEmpty)
            {
                GameLog.Info(LogCategory.Train, $"QA 동시 경합: 창고 #{storageId}의 0번 칸이 비어 있다 — " +
                                          "경합할 대상이 없다. 0번 칸에 자원을 넣고 다시 누른다.");
                return;
            }

            LogContentionTotals(storageId, "요청 전");
            _contentionStorageId = storageId;
            _contentionLogDelay = 0.5f;
            RunContentionTestRpc(storageId);
        }

        /// <summary>
        /// 각 피어가 수신 프레임에 자기 이동 요청을 발행한다 — 서버 도착이 붙어 경합이 재현된다.
        /// 받는 칸은 <b>자기 인벤토리의 첫 빈 칸</b>이다: 0번 칸은 시작 배치가 집게라 고정으로 쓰면
        /// 이동이 아니라 <b>스왑</b>(집게가 창고로 나간다)이 돼 경합이 아닌 장비 교환을 재는 셈이 된다.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void RunContentionTestRpc(int storageId)
        {
            NetworkObject localPlayer = LocalInteraction.GetLocalPlayerObject();
            PlayerInventory inventory = localPlayer != null
                ? localPlayer.GetComponent<PlayerInventory>()
                : null;
            if (inventory == null)
            {
                return;
            }

            int toIndex = -1;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                if (inventory.GetSlot(i).IsEmpty)
                {
                    toIndex = i;
                    break;
                }
            }

            if (toIndex < 0)
            {
                GameLog.Info(LogCategory.Train, "QA 동시 경합: 인벤토리에 빈 칸이 없어 이 피어는 요청을 보내지 않는다.");
                return;
            }

            RequestTransferServerRpc(
                storageId, ITrainStorage.ContainerStorage, 0, ITrainStorage.ContainerInventory, toIndex);
        }

        private int FindAnyAliveStorage()
        {
            if (!ServiceLocator.TryGet(out ITrainState train))
            {
                return -1;
            }

            for (int i = 0; i < _blockOwners.Count; i++)
            {
                if (IsStorageAlive(train, _blockOwners[i], out _))
                {
                    return _blockOwners[i];
                }
            }

            return -1;
        }

        /// <summary>창고 + 접속 중인 전 플레이어 인벤토리의 아이템 총량 — 경합 전후를 눈으로 비교한다.</summary>
        private void LogContentionTotals(int storageId, string phase)
        {
            if (storageId < 0)
            {
                return;
            }

            int storageTotal = CountItems(CopyBlockSlots(storageId));
            int playerTotal = 0;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                foreach (NetworkClient client in manager.ConnectedClientsList)
                {
                    PlayerInventory inventory = client.PlayerObject != null
                        ? client.PlayerObject.GetComponent<PlayerInventory>()
                        : null;
                    if (inventory != null)
                    {
                        playerTotal += CountItems(inventory.ServerCopySlotViews());
                    }
                }
            }

            GameLog.Info(LogCategory.Train, $"QA 동시 경합 ({phase}) — 창고 #{storageId} {storageTotal} + " +
                                      $"플레이어 합계 {playerTotal} = 총 {storageTotal + playerTotal}");
        }

        private static int CountItems(HotbarSlotView[] slots)
        {
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemType != HotbarItemType.None)
                {
                    // 자원은 스택 수량, 무기·도구는 1개로 센다.
                    total += Mathf.Max(1, slots[i].Count);
                }
            }

            return total;
        }

        // ── 서버: 블록 할당·해제·복사·되쓰기 (건축 개편 2차 §2.8) ────────────────────

        public void ServerAllocateBlock(int storageId)
        {
            if (!IsServer || storageId <= 0 || FindBlock(storageId) >= 0)
            {
                return;
            }

            _blockOwners.Add((ushort)storageId);
            for (int i = 0; i < _slotsPerStorage; i++)
            {
                _slots.Add(new NetworkSlot { ItemType = HotbarItemType.None, Count = 0 });
            }
        }

        public void ServerReleaseBlock(int storageId, StorageReleaseMode mode)
        {
            if (!IsServer)
            {
                return;
            }

            int block = FindBlock(storageId);
            if (block < 0)
            {
                return;
            }

            HotbarSlotView[] contents = CopyBlockSlots(storageId);

            // swap-remove — 마지막 블록을 빈자리로 옮기고 꼬리를 잘라낸다 (Id 매핑은 _blockOwners가 담보).
            if (StorageBlockLogic.TryPlanSwapRemove(_blockOwners.Count, block, out int moveFromBlock))
            {
                if (moveFromBlock >= 0)
                {
                    int destination = StorageBlockLogic.SlotOffset(block, _slotsPerStorage);
                    int source = StorageBlockLogic.SlotOffset(moveFromBlock, _slotsPerStorage);
                    for (int i = 0; i < _slotsPerStorage; i++)
                    {
                        if (!_slots[destination + i].Equals(_slots[source + i]))
                        {
                            _slots[destination + i] = _slots[source + i];
                        }
                    }

                    _blockOwners[block] = _blockOwners[moveFromBlock];
                }

                int tail = StorageBlockLogic.SlotOffset(_blockOwners.Count - 1, _slotsPerStorage);
                for (int i = _slotsPerStorage - 1; i >= 0; i--)
                {
                    _slots.RemoveAt(tail + i);
                }

                _blockOwners.RemoveAt(_blockOwners.Count - 1);
            }

            if (mode == StorageReleaseMode.Discard || AreAllSlotsEmpty(contents))
            {
                return;
            }

            // 배출 — 위치는 항목의 점유 중심. 해제는 항목 제거 <b>전</b>에 호출되는 규약이라 항목이 남아 있다.
            if (!ServiceLocator.TryGet(out World.IStorageBundleSpawner spawner)
                || _layoutSettings == null || !ServiceLocator.TryGet(out ITrainState train)
                || !train.TryGetStructureById(storageId, out StructureEntry entry))
            {
                return;
            }

            train.TryGetStructureCenter(entry.Id, out Vector3 point);
            if (mode == StorageReleaseMode.DeckBundle)
            {
                spawner.ServerSpawnDeckResting(contents, entry.CarIndex,
                    _layoutSettings.DeckHeight, point.z, train.GetEjectOffset(entry.CarIndex));
            }
            else
            {
                // 칸 파괴 — 그 자리 갑판 높이에서 지상으로 느린 포물선 투척 (비행은 각 피어 로컬 재생).
                spawner.ServerSpawnOnGround(contents, point);
            }
        }

        private static bool AreAllSlotsEmpty(HotbarSlotView[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        private HotbarSlotView[] CopyBlockSlots(int storageId)
        {
            var copy = new HotbarSlotView[_slotsPerStorage];
            int block = FindBlock(storageId);
            if (block < 0)
            {
                return copy;
            }

            int start = StorageBlockLogic.SlotOffset(block, _slotsPerStorage);
            for (int i = 0; i < _slotsPerStorage; i++)
            {
                NetworkSlot slot = _slots[start + i];
                copy[i] = new HotbarSlotView(slot.ItemType, slot.Count, slot.Resource);
            }

            return copy;
        }

        private void ApplyBlockSlots(int storageId, HotbarSlotView[] slots)
        {
            int block = FindBlock(storageId);
            if (block < 0)
            {
                return;
            }

            int start = StorageBlockLogic.SlotOffset(block, _slotsPerStorage);
            for (int i = 0; i < slots.Length && i < _slotsPerStorage; i++)
            {
                var next = new NetworkSlot
                {
                    ItemType = slots[i].ItemType,
                    Count = (byte)slots[i].Count,
                    Resource = slots[i].Resource,
                };
                if (!_slots[start + i].Equals(next))
                {
                    _slots[start + i] = next;
                }
            }
        }
    }
}
