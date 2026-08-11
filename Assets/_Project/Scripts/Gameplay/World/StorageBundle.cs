using Game.Core.Events;
using Game.Gameplay.Harpoon;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 창고 보따리 (M5 8차) — 파괴된 창고의 슬롯 전체를 담는 단일 회수물.
    /// 창고와 <b>같은 슬롯 표현</b>(<see cref="NetworkSlot"/> NetworkList)이라 무기·장비도 그대로 담긴다.
    /// 집게 도착 = <b>일괄 획득</b> (1차 검증 개선 2 — 자원과 같은 회수 감각): 내용물이 전부
    /// 들어가면 풀어서 수납하고 소멸, 1칸이라도 부족하면 <b>보따리 아이템</b>(1칸)으로 획득한다
    /// (내용물은 <see cref="IBundleItemStore"/>에 보관). 해제 낙하는 안착 파이프라인
    /// (<see cref="SettleableGrabbable"/> — 갑판 휴지·하강 로컬 재생·이탈 추종·소실 회수)을 그대로 탄다.
    /// E 창(창고 창 재사용) 코드는 휴면 — 사용하지 않되 추후 확장 대비로 남긴다 (1차 검증 결정).
    /// 슬롯이 전부 비면 서버가 자동 회수한다. 내구도는 없다 — 공격에 부서지지 않고,
    /// 소실은 후방 회수·소실 칸 회수 규약뿐이다 ("회수 기회"라는 존재 이유 보존, 착수 전 결정).
    /// </summary>
    public sealed class StorageBundle : SettleableGrabbable
    {
        [Tooltip("집게 무게 등급 — 이 값 이상의 집게 등급이어야 운반할 수 있다.")]
        [SerializeField, Range(1, 3)] private int _grabWeight = 1;

        [Tooltip("E 상호작용 반경 — 창고와 같은 규약.")]
        [SerializeField, Min(0.5f)] private float _interactRadius = 3f;

        [Tooltip("보따리를 '쳐다봤다'고 볼 시선 정렬 하한 (카메라 전방·보따리 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.8f;

        [Tooltip("칸 파괴 투척의 비행 시간 (초) — 느린 포물선 (M5 8차 §2).")]
        [SerializeField, Min(0.1f)] private float _throwFlightSeconds = 1.6f;

        [Tooltip("투척 포물선의 정점 높이 (m) — 시작·착지를 잇는 직선 위로 이만큼 부풀린다.")]
        [SerializeField, Min(0f)] private float _throwArcHeight = 3f;

        private readonly NetworkList<NetworkSlot> _slots = new NetworkList<NetworkSlot>();

        // 칸 파괴 투척 (M5 8차 §2) — (시작 위치, 착지 위치)만 복제하고 각 피어가 같은 포물선을
        // 로컬 재생한다 (7차 하강 로컬 재생과 같은 규약 — 프레임 동기화 없음 = 떨림 없음).
        // 시작 위치는 착지와 같은 월드 바인딩 좌표계라 비행 중에도 세계와 함께 흐른다.
        // 시간축은 전 피어가 동기화된 ServerTime — 늦게 접속해도 같은 t를 계산한다.
        private readonly NetworkVariable<Vector3> _flightStartPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<double> _flightStartTime = new NetworkVariable<double>();
        private readonly NetworkVariable<float> _flightDuration = new NetworkVariable<float>();

        private Vector3 _pendingFlightStart;
        private bool _hasPendingFlight;

        private HotbarSlotView[] _pendingContents;

        // 빈 보따리 자동 회수 — 목록 변경 콜백 안에서 곧바로 Despawn하지 않고 Update로 미룬다
        // (전송 RPC 처리 중의 재진입을 피한다).
        private bool _emptyCheckPending;

        // 로컬 E창 상태 (M5 8차 — 회수 UX: E창 옮겨 담기) — 창고 창과 같은 근접·시선·E키 규약.
        private bool _localInRange;
        private bool _localPanelOpen;
        private bool _storagePanelOpen;

        /// <summary>
        /// 비행 중에는 3단계 집게만 낚아챌 수 있다 (1차 검증 개선 1 — "불허"의 등급 예외화).
        /// 그랩 검증이 이미 등급 ≥ 무게를 강제하므로 무게 축으로 표현한다 — 착지하면 원래 등급.
        /// </summary>
        public override int GrabWeight => IsInFlight ? FlightGrabWeight : _grabWeight;

        private const int FlightGrabWeight = 3;

        /// <summary>투척 비행 중인가 — 3단계 집게 외에는 착지 후부터 잡힌다.</summary>
        public bool IsInFlight => _flightDuration.Value > 0f && IsSpawned
            && NetworkManager != null
            && NetworkManager.ServerTime.Time < _flightStartTime.Value + _flightDuration.Value;

        /// <summary>
        /// 서버 전용 — 스폰 직전에 투척 비행(칸 파괴 지점 → 착지 지점)을 예약한다.
        /// startPosition은 착지와 같은 월드 바인딩 좌표 (스폰 순간 traveled == spawnDistance).
        /// </summary>
        public void ServerSetThrowFlight(Vector3 startPosition)
        {
            _pendingFlightStart = startPosition;
            _hasPendingFlight = true;
        }

        /// <summary>보따리 슬롯 수 — 이관된 창고의 슬롯 수 그대로다.</summary>
        public int SlotCount => _slots.Count;

        /// <summary>슬롯 조회 — 복제 상태 기반이라 전 피어 동일 (UI 표시용).</summary>
        public HotbarSlotView GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Count)
            {
                return new HotbarSlotView(HotbarItemType.None, 0);
            }

            NetworkSlot slot = _slots[index];
            return new HotbarSlotView(slot.ItemType, slot.Count, slot.Resource);
        }

        /// <summary>서버 전용 — 스폰 직전에 내용물(파괴된 창고의 슬롯 스냅샷)을 예약한다.</summary>
        public void ServerSetContents(HotbarSlotView[] contents)
        {
            _pendingContents = contents;
        }

        /// <summary>서버 전용 — 전송 판정용 슬롯 스냅샷 (창고 CopyStorageSlots와 같은 규약).</summary>
        public HotbarSlotView[] ServerCopySlots()
        {
            var copy = new HotbarSlotView[_slots.Count];
            for (int i = 0; i < _slots.Count; i++)
            {
                NetworkSlot slot = _slots[i];
                copy[i] = new HotbarSlotView(slot.ItemType, slot.Count, slot.Resource);
            }

            return copy;
        }

        /// <summary>서버 전용 — 전송 확정 반영 (변경 칸만 되쓴다).</summary>
        public void ServerApplySlots(HotbarSlotView[] slots)
        {
            for (int i = 0; i < slots.Length && i < _slots.Count; i++)
            {
                var next = new NetworkSlot
                {
                    ItemType = slots[i].ItemType,
                    Count = (byte)slots[i].Count,
                    Resource = slots[i].Resource,
                };
                if (!_slots[i].Equals(next))
                {
                    _slots[i] = next;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // 풀 재사용 시 이전 내용물이 새지 않게 예약 스냅샷으로 다시 채운다.
                _slots.Clear();
                if (_pendingContents != null)
                {
                    for (int i = 0; i < _pendingContents.Length; i++)
                    {
                        _slots.Add(new NetworkSlot
                        {
                            ItemType = _pendingContents[i].ItemType,
                            Count = (byte)_pendingContents[i].Count,
                            Resource = _pendingContents[i].Resource,
                        });
                    }

                    _pendingContents = null;
                }
            }

            if (IsServer)
            {
                // 풀 재사용 시 이전 비행이 새지 않게 — 투척 스폰만 비행 값을 얻는다.
                _flightDuration.Value = _hasPendingFlight ? _throwFlightSeconds : 0f;
                if (_hasPendingFlight)
                {
                    _flightStartPosition.Value = _pendingFlightStart;
                    _flightStartTime.Value = NetworkManager.ServerTime.Time;
                    _hasPendingFlight = false;
                }
            }

            _emptyCheckPending = false;
            _slots.OnListChanged += OnSlotsChanged;

            _storagePanelOpen = false;
            EventBus<Player.UiCloseRequestedLocalEvent>.Subscribe(OnUiCloseRequested);
            EventBus<Train.StoragePanelToggledLocalEvent>.Subscribe(OnStoragePanelToggled);
        }

        public override void OnNetworkDespawn()
        {
            _slots.OnListChanged -= OnSlotsChanged;
            EventBus<Player.UiCloseRequestedLocalEvent>.Unsubscribe(OnUiCloseRequested);
            EventBus<Train.StoragePanelToggledLocalEvent>.Unsubscribe(OnStoragePanelToggled);

            // 비워져 회수되거나 소실돼도 열린 창·안내가 남지 않게 한다.
            SetPanelOpen(false);
            SetLocalInRange(false);

            base.OnNetworkDespawn();
        }

        /// <summary>Esc의 닫기 요청 (M5 4차 규약) — 열린 보따리 창을 닫는다.</summary>
        private void OnUiCloseRequested(Player.UiCloseRequestedLocalEvent evt)
        {
            SetPanelOpen(false);
        }

        private void OnStoragePanelToggled(Train.StoragePanelToggledLocalEvent evt)
        {
            _storagePanelOpen = evt.IsOpen;
        }

        private void OnSlotsChanged(NetworkListEvent<NetworkSlot> changeEvent)
        {
            _emptyCheckPending = true;
        }

        protected override void Update()
        {
            base.Update();

            if (!IsSpawned)
            {
                return;
            }

            // 슬롯이 전부 비면 자동 회수 — "회수 기회"를 다한 보따리는 세상에 남지 않는다.
            if (IsServer && _emptyCheckPending)
            {
                _emptyCheckPending = false;
                if (AreAllSlotsEmpty())
                {
                    // destroy: true여야 PooledNetworkPrefabHandler를 거쳐 풀로 반환된다.
                    NetworkObject.Despawn(true);
                    return;
                }
            }

            // 서버 — 그랩(3단계 낚아채기) 확정 순간 비행을 끝낸다. 견인이 위치를 쥐고,
            // 이후 해제 시 포물선으로 되돌아가지 않게 한다.
            if (IsServer && IsClaimed && _flightDuration.Value > 0f)
            {
                _flightDuration.Value = 0f;
            }

            if (!IsClaimed && IsInFlight)
            {
                // 베이스가 놓은 안착 위치를 비행 위치로 덮는다 — 착지(t=1) 이후는 자연히 안착 유도로 복귀.
                ApplyFlightPosition();
                return;
            }

            UpdateLocalInteraction();
        }

        /// <summary>
        /// 투척 포물선 로컬 재생 — 시작·착지 모두 월드 바인딩 좌표라 세계 스크롤과 함께 흐르고,
        /// 높이는 직선 보간 위에 4·t·(1−t) 아치를 더한다. 전 피어가 같은 수식·같은 시계를 쓴다.
        /// </summary>
        private void ApplyFlightPosition()
        {
            float duration = _flightDuration.Value;
            float t = duration > 0f
                ? Mathf.Clamp01((float)((NetworkManager.ServerTime.Time - _flightStartTime.Value) / duration))
                : 1f;

            Vector3 target = transform.position;
            Vector3 start = target;
            if (Game.Core.Services.ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                start = WorldScrollMath.GetScrolledPosition(
                    _flightStartPosition.Value, SpawnDistance, scroll.TraveledDistance);
            }

            Vector3 position = Vector3.Lerp(start, target, t);
            position.y += _throwArcHeight * 4f * t * (1f - t);
            transform.position = position;
        }

        // ── 로컬: 근접·시선 판정과 E키 토글 (창고 창과 같은 규약) ────────
        // E창 휴면 (1차 검증 결정 2026-08-11) — 회수는 집게 일괄 획득으로 재설계됐다.
        // 코드는 추후 확장(선별 회수 등)을 고려해 남긴다 — 게이트만 닫는다.

        private static readonly bool EWindowEnabled = false;

        private void UpdateLocalInteraction()
        {
            if (!EWindowEnabled)
            {
                return;
            }

            NetworkObject localPlayer = Player.LocalInteraction.GetLocalPlayerObject();
            if (localPlayer == null)
            {
                SetLocalInRange(false);
                SetPanelOpen(false);
                return;
            }

            Vector3 point = transform.position;
            bool inRange = !IsClaimed
                && Player.LocalInteraction.IsWithinRange(localPlayer, point, _interactRadius);
            bool ready = inRange
                && Player.LocalInteraction.IsLookingAt(localPlayer, point, _lookDotThreshold);

            // 범위를 벗어나거나 운반(견인)이 시작되면 창을 닫는다.
            if (_localPanelOpen && !inRange)
            {
                SetPanelOpen(false);
            }

            SetLocalInRange(ready && !_localPanelOpen && !_storagePanelOpen);

            var hotbar = localPlayer.GetComponent<Inventory.HotbarController>();
            bool otherUiOpen = (_storagePanelOpen || (hotbar != null && hotbar.IsPanelOpen)) && !_localPanelOpen;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame || otherUiOpen)
            {
                return;
            }

            if (_localPanelOpen)
            {
                SetPanelOpen(false);
            }
            else if (ready)
            {
                SetPanelOpen(true);
            }
        }

        private void SetLocalInRange(bool inRange)
        {
            if (_localInRange != inRange)
            {
                _localInRange = inRange;
                EventBus<Train.BundlePromptLocalEvent>.Publish(new Train.BundlePromptLocalEvent(inRange));
            }
        }

        private void SetPanelOpen(bool open)
        {
            if (_localPanelOpen != open)
            {
                _localPanelOpen = open;
                EventBus<Train.BundlePanelToggledLocalEvent>.Publish(
                    new Train.BundlePanelToggledLocalEvent(open, open ? NetworkObjectId : 0UL));
            }
        }

        private bool AreAllSlotsEmpty()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].ItemType != HotbarItemType.None)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 집게 도착 = 일괄 획득 (1차 검증 개선 2 — 운반 전용에서 재설계):
        /// 내용물 전 슬롯(무기·장비 포함)이 들어가면 풀어서 수납하고 소멸(Consumed),
        /// 1칸이라도 부족하면 빈 칸 1개에 <b>보따리 아이템</b>으로 획득한다 — 내용물은
        /// 보관소(<see cref="IBundleItemStore"/>)에 맡기고 슬롯 Count에 보관 id를 싣는다.
        /// 빈 칸조차 없거나 보관소가 가득이면 Rejected — 집게가 그 자리 낙하로 처리한다.
        /// </summary>
        public override GrabCompletionResult TryCompleteGrab(in GrabCompletion completion)
        {
            if (!IsServer || completion.Grabber == null)
            {
                return GrabCompletionResult.Rejected;
            }

            var inventory = completion.Grabber.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return GrabCompletionResult.Rejected;
            }

            HotbarSlotView[] contents = ServerCopySlots();

            // 1안 — 전부 풀어서 수납 (복사본 원자: 전 슬롯이 들어갈 때만 반영).
            HotbarSlotView[] slots = inventory.ServerCopySlotViews();
            if (HotbarLogic.TryAddAll(slots, contents, inventory.GetMaxStack))
            {
                inventory.ServerApplySlotViews(slots);
                // destroy: true여야 PooledNetworkPrefabHandler를 거쳐 풀로 반환된다.
                NetworkObject.Despawn(true);
                return GrabCompletionResult.Consumed;
            }

            // 2안 — 공간 부족: 보따리 아이템(1칸)으로 획득. 내용물은 보관소에.
            slots = inventory.ServerCopySlotViews();
            int emptyIndex = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                {
                    emptyIndex = i;
                    break;
                }
            }

            if (emptyIndex < 0
                || !Game.Core.Services.ServiceLocator.TryGet(out IBundleItemStore store))
            {
                return GrabCompletionResult.Rejected;
            }

            byte id = store.ServerStore(contents);
            if (id == 0)
            {
                return GrabCompletionResult.Rejected;
            }

            slots[emptyIndex] = new HotbarSlotView(HotbarItemType.Bundle, id);
            inventory.ServerApplySlotViews(slots);
            NetworkObject.Despawn(true);
            return GrabCompletionResult.Consumed;
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            _pendingContents = null;
            _emptyCheckPending = false;
        }
    }
}
