using Game.Core.Events;
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
    /// 창고 건축물(<see cref="StructureKind.Storage"/>)마다 독립 저장고를 가지되, 슬롯은
    /// 단일 평탄 NetworkList 하나에 담는다 — 인덱스 = 칸 인덱스 × 창고 슬롯 수 + 칸 내 슬롯
    /// (NetworkList는 NetworkBehaviour 필드여야 하고 창고 수가 가변이라 칸별 목록 나열보다 안전).
    /// 상호작용은 근접 + 시선 + E키 토글 (<see cref="Game.Gameplay.Crafting.CraftingStation"/> 규약).
    /// Train 루트(TrainState와 같은 GO)에 1개 배치한다.
    /// </summary>
    public sealed class TrainStorage : NetworkBehaviour, ITrainStorage
    {
        [SerializeField] private TrainLayoutSettings _layoutSettings;
        [SerializeField] private TrainExpansionSettings _expansionSettings;
        [SerializeField] private ResourceCatalog _catalog;

        [Tooltip("창고 하나의 저장 슬롯 수.")]
        [SerializeField, Min(1)] private int _slotsPerStorage = 10;

        [SerializeField, Min(0.5f)] private float _interactRadius = 3f;

        [Tooltip("창고를 '쳐다봤다'고 볼 시선 정렬 하한 (카메라 전방·창고 칸 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.8f;

        private readonly NetworkList<NetworkSlot> _slots = new NetworkList<NetworkSlot>();

        private bool _localInRange;
        private int _localInRangeCar = -1;
        private int _openCarIndex = -1;

        // QA 동시 경합 (M5 5차) — 요청 발행 후 총량을 다시 찍기까지의 대기. 음수 = 비활성.
        private float _contentionLogDelay = -1f;
        private int _contentionCarIndex = -1;

        public int SlotsPerStorage => _slotsPerStorage;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerInitializeSlots();
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

        private void ServerInitializeSlots()
        {
            // 편성 상한 × 창고 슬롯 수로 고정 확보 — 칸 증설과 무관하게 인덱스 규약이 유지된다.
            int maxCars = _expansionSettings != null ? _expansionSettings.MaxCarCount : 0;
            int total = Mathf.Max(1, maxCars) * _slotsPerStorage;

            _slots.Clear();
            for (int i = 0; i < total; i++)
            {
                _slots.Add(new NetworkSlot { ItemType = HotbarItemType.None, Count = 0 });
            }
        }

        public HotbarSlotView GetSlot(int carIndex, int slotIndex)
        {
            int flat = carIndex * _slotsPerStorage + slotIndex;
            if (carIndex < 0 || slotIndex < 0 || slotIndex >= _slotsPerStorage
                || flat < 0 || flat >= _slots.Count)
            {
                return new HotbarSlotView(HotbarItemType.None, 0);
            }

            NetworkSlot slot = _slots[flat];
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
                    LogContentionTotals(_contentionCarIndex, "확정 후");
                }
            }

            NetworkObject localPlayer = LocalInteraction.GetLocalPlayerObject();
            if (localPlayer == null)
            {
                SetLocalInRange(false, -1);
                SetPanelOpen(-1);
                return;
            }

            int nearCar = FindNearestAliveStorageCar(localPlayer.transform.position, out Vector3 nearPoint);
            bool inRange = nearCar >= 0
                && LocalInteraction.IsWithinRange(localPlayer, nearPoint, _interactRadius);
            bool ready = inRange && LocalInteraction.IsLookingAt(localPlayer, nearPoint, _lookDotThreshold);

            // 범위를 벗어나거나 창고가 파괴되면 창을 닫는다.
            if (_openCarIndex >= 0 && (!inRange || nearCar != _openCarIndex))
            {
                SetPanelOpen(-1);
            }

            SetLocalInRange(ready && _openCarIndex < 0, nearCar);

            HotbarController hotbar = localPlayer.GetComponent<HotbarController>();
            bool otherUiOpen = hotbar != null && hotbar.IsPanelOpen && _openCarIndex < 0;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame || otherUiOpen)
            {
                return;
            }

            if (_openCarIndex >= 0)
            {
                SetPanelOpen(-1);
            }
            else if (ready)
            {
                SetPanelOpen(nearCar);
            }
        }

        /// <summary>
        /// 살아 있는 창고 건축물이 있는 칸 중 위치에서 가장 가까운 칸 — 없으면 -1.
        /// 이탈 칸도 접근 가능(이탈 오프셋 반영 — 회수 작업 중 물자 회수를 막지 않는다).
        /// </summary>
        private int FindNearestAliveStorageCar(Vector3 position, out Vector3 nearestPoint)
        {
            nearestPoint = default;
            if (_layoutSettings == null || !ServiceLocator.TryGet(out ITrainState train))
            {
                return -1;
            }

            int best = -1;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < train.CarCount; i++)
            {
                if (!IsStorageAlive(train, i))
                {
                    continue;
                }

                float z = _layoutSettings.CarCenterZ(i, train.GetEjectOffset(i));
                var point = new Vector3(0f, _layoutSettings.DeckHeight, z);
                float sqr = (position - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                    nearestPoint = point;
                }
            }

            return best;
        }

        /// <summary>창고가 기능하는 상태인지 — 창고 종류 + 건축물 생존 + 칸 잔존(파괴 아님. 이탈은 허용).</summary>
        private static bool IsStorageAlive(ITrainState train, int carIndex)
        {
            return train.TryGetStructure(carIndex, out StructureState structure)
                && structure.Present && structure.Kind == StructureKind.Storage && structure.Health > 0f
                && train.TryGetCar(carIndex, out CarState car) && car.Health > 0f;
        }

        private void SetLocalInRange(bool inRange, int carIndex)
        {
            if (_localInRange != inRange || (_localInRange && _localInRangeCar != carIndex))
            {
                _localInRange = inRange;
                _localInRangeCar = inRange ? carIndex : -1;
                EventBus<StoragePromptLocalEvent>.Publish(
                    new StoragePromptLocalEvent(inRange, _localInRangeCar));
            }
        }

        private void SetPanelOpen(int carIndex)
        {
            if (_openCarIndex != carIndex)
            {
                _openCarIndex = carIndex;
                EventBus<StoragePanelToggledLocalEvent>.Publish(
                    new StoragePanelToggledLocalEvent(carIndex >= 0, carIndex));
            }
        }

        // ── 이동 요청 → 호스트 확정 ────────────────────

        public void RequestTransfer(int carIndex, byte fromContainer, int fromIndex, byte toContainer, int toIndex)
        {
            if (_openCarIndex == carIndex && carIndex >= 0)
            {
                RequestTransferServerRpc(carIndex, fromContainer, fromIndex, toContainer, toIndex);
            }
        }

        /// <summary>
        /// 이동 확정 — 창고 생존·거리를 호스트 상태로 재검증하고, 두 컨테이너의 복사본 위에서
        /// 순수 로직(<see cref="StorageLogic"/>)으로 판정한 뒤 성공 시에만 변경 칸을 되쓴다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestTransferServerRpc(int carIndex, byte fromContainer, int fromIndex,
            byte toContainer, int toIndex, RpcParams rpcParams = default)
        {
            if (_layoutSettings == null || !ServiceLocator.TryGet(out ITrainState train)
                || !IsStorageAlive(train, carIndex))
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

            // 서버 측 거리 검증 — 범위 밖 이동 요청은 기각한다 (호스트 검증 원칙).
            float z = _layoutSettings.CarCenterZ(carIndex, train.GetEjectOffset(carIndex));
            var point = new Vector3(0f, _layoutSettings.DeckHeight, z);
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
            HotbarSlotView[] storageSlots = CopyStorageSlots(carIndex);

            HotbarSlotView[] from = SelectContainer(fromContainer, inventorySlots, storageSlots);
            HotbarSlotView[] to = SelectContainer(toContainer, inventorySlots, storageSlots);
            if (from == null || to == null
                || fromIndex < 0 || fromIndex >= from.Length)
            {
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
            ApplyStorageSlots(carIndex, storageSlots);
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
        /// 전 피어가 <b>같은 프레임에 같은 이동</b>(창고 슬롯 0 → 개인 슬롯 0)을 요청하게 만든다.
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

            int carIndex = FindAnyAliveStorageCar();
            if (carIndex < 0)
            {
                Debug.Log("[TrainStorage] QA 동시 경합: 살아 있는 창고가 없다 — 창고를 먼저 설치한다.");
                return;
            }

            // 옮길 것이 없으면 두 요청 모두 조용히 기각돼 "총량 보존"이 참이지만 아무것도 검증하지
            // 못한다 — 시작 전에 전제를 막아 헛된 통과를 만들지 않는다.
            if (GetSlot(carIndex, 0).IsEmpty)
            {
                Debug.Log($"[TrainStorage] QA 동시 경합: #{carIndex}번 칸 창고의 0번 칸이 비어 있다 — " +
                    "경합할 대상이 없다. 0번 칸에 자원을 넣고 다시 누른다.");
                return;
            }

            LogContentionTotals(carIndex, "요청 전");
            _contentionCarIndex = carIndex;
            _contentionLogDelay = 0.5f;
            RunContentionTestRpc(carIndex);
        }

        /// <summary>
        /// 각 피어가 수신 프레임에 자기 이동 요청을 발행한다 — 서버 도착이 붙어 경합이 재현된다.
        /// 받는 칸은 <b>자기 인벤토리의 첫 빈 칸</b>이다: 0번 칸은 시작 배치가 집게라 고정으로 쓰면
        /// 이동이 아니라 <b>스왑</b>(집게가 창고로 나간다)이 돼 경합이 아닌 장비 교환을 재는 셈이 된다.
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void RunContentionTestRpc(int carIndex)
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
                Debug.Log("[TrainStorage] QA 동시 경합: 인벤토리에 빈 칸이 없어 이 피어는 요청을 보내지 않는다.");
                return;
            }

            RequestTransferServerRpc(
                carIndex, ITrainStorage.ContainerStorage, 0, ITrainStorage.ContainerInventory, toIndex);
        }

        private int FindAnyAliveStorageCar()
        {
            if (!ServiceLocator.TryGet(out ITrainState train))
            {
                return -1;
            }

            for (int i = 0; i < train.CarCount; i++)
            {
                if (IsStorageAlive(train, i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>창고 + 접속 중인 전 플레이어 인벤토리의 아이템 총량 — 경합 전후를 눈으로 비교한다.</summary>
        private void LogContentionTotals(int carIndex, string phase)
        {
            if (carIndex < 0)
            {
                return;
            }

            int storageTotal = CountItems(CopyStorageSlots(carIndex));
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

            Debug.Log($"[TrainStorage] QA 동시 경합 ({phase}) — #{carIndex}번 칸 창고 {storageTotal} + " +
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

        // ── 서버: 소실·복사·되쓰기 ────────────────────

        public void ServerClearStorage(int carIndex)
        {
            if (!IsServer || carIndex < 0)
            {
                return;
            }

            int start = carIndex * _slotsPerStorage;
            var empty = new NetworkSlot { ItemType = HotbarItemType.None, Count = 0 };
            for (int i = 0; i < _slotsPerStorage; i++)
            {
                int flat = start + i;
                if (flat < _slots.Count && !_slots[flat].Equals(empty))
                {
                    _slots[flat] = empty;
                }
            }
        }

        /// <summary>
        /// 창고 내용물을 보따리로 내놓고 비운다 (M5 8차) — 파괴 확정 지점 전용 (서버).
        /// 스냅샷을 먼저 뜨고 비운 뒤 스폰을 시도한다 — 스포너 부재·빈 내용물이면 기존 소실 규약 그대로다.
        /// </summary>
        public void ServerDropStorageAsBundle(int carIndex, bool deckAlive)
        {
            if (!IsServer || carIndex < 0)
            {
                return;
            }

            HotbarSlotView[] contents = CopyStorageSlots(carIndex);
            ServerClearStorage(carIndex);

            // 내용물이 전부 빈 창고는 보따리를 내지 않는다 (빈 보따리 스폰 방지).
            if (AreAllSlotsEmpty(contents)
                || !ServiceLocator.TryGet(out World.IStorageBundleSpawner spawner)
                || _layoutSettings == null || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            float ejectOffset = train.GetEjectOffset(carIndex);
            float centerZ = _layoutSettings.CarCenterZ(carIndex, ejectOffset);

            if (deckAlive)
            {
                spawner.ServerSpawnDeckResting(contents, carIndex, _layoutSettings.DeckHeight, centerZ, ejectOffset);
            }
            else
            {
                spawner.ServerSpawnOnGround(contents, centerZ);
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

        private HotbarSlotView[] CopyStorageSlots(int carIndex)
        {
            var copy = new HotbarSlotView[_slotsPerStorage];
            int start = carIndex * _slotsPerStorage;
            for (int i = 0; i < _slotsPerStorage; i++)
            {
                int flat = start + i;
                if (flat < _slots.Count)
                {
                    NetworkSlot slot = _slots[flat];
                    copy[i] = new HotbarSlotView(slot.ItemType, slot.Count, slot.Resource);
                }
            }

            return copy;
        }

        private void ApplyStorageSlots(int carIndex, HotbarSlotView[] slots)
        {
            int start = carIndex * _slotsPerStorage;
            for (int i = 0; i < slots.Length && start + i < _slots.Count; i++)
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
