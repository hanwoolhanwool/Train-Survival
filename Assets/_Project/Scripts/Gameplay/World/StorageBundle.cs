using Game.Gameplay.Harpoon;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 창고 보따리 (M5 8차) — 파괴된 창고의 슬롯 전체를 담는 단일 회수물.
    /// 창고와 <b>같은 슬롯 표현</b>(<see cref="NetworkSlot"/> NetworkList)이라 무기·장비도 그대로 담긴다.
    /// 집게는 <b>운반 전용</b> — <see cref="TryCompleteGrab"/>이 항상 Rejected라 도착·해제가
    /// 그 자리 낙하로 처리되고, 안착 파이프라인(<see cref="SettleableGrabbable"/> — 갑판 휴지·
    /// 하강 로컬 재생·이탈 추종·소실 회수)을 그대로 탄다. 옮겨 담기는 E 창(창고 창 재사용)이 담당한다.
    /// 슬롯이 전부 비면 서버가 자동 회수한다. 내구도는 없다 — 공격에 부서지지 않고,
    /// 소실은 후방 회수·소실 칸 회수 규약뿐이다 ("회수 기회"라는 존재 이유 보존, 착수 전 결정).
    /// </summary>
    public sealed class StorageBundle : SettleableGrabbable
    {
        [Tooltip("집게 무게 등급 — 이 값 이상의 집게 등급이어야 운반할 수 있다.")]
        [SerializeField, Range(1, 3)] private int _grabWeight = 1;

        private readonly NetworkList<NetworkSlot> _slots = new NetworkList<NetworkSlot>();

        private HotbarSlotView[] _pendingContents;

        // 빈 보따리 자동 회수 — 목록 변경 콜백 안에서 곧바로 Despawn하지 않고 Update로 미룬다
        // (전송 RPC 처리 중의 재진입을 피한다).
        private bool _emptyCheckPending;

        public override int GrabWeight => _grabWeight;

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

            _emptyCheckPending = false;
            _slots.OnListChanged += OnSlotsChanged;
        }

        public override void OnNetworkDespawn()
        {
            _slots.OnListChanged -= OnSlotsChanged;
            base.OnNetworkDespawn();
        }

        private void OnSlotsChanged(NetworkListEvent<NetworkSlot> changeEvent)
        {
            _emptyCheckPending = true;
        }

        protected override void Update()
        {
            base.Update();

            // 슬롯이 전부 비면 자동 회수 — "회수 기회"를 다한 보따리는 세상에 남지 않는다.
            if (IsServer && IsSpawned && _emptyCheckPending)
            {
                _emptyCheckPending = false;
                if (AreAllSlotsEmpty())
                {
                    // destroy: true여야 PooledNetworkPrefabHandler를 거쳐 풀로 반환된다.
                    NetworkObject.Despawn(true);
                }
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

        /// <summary>운반 전용 (착수 전 결정) — 무기·장비가 들어 있어 자동 수납이 성립하지 않는다.
        /// Rejected면 집게가 그 자리 해제(낙하)로 처리해 안착 파이프라인을 탄다.</summary>
        public override GrabCompletionResult TryCompleteGrab(in GrabCompletion completion)
        {
            return GrabCompletionResult.Rejected;
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            _pendingContents = null;
            _emptyCheckPending = false;
        }
    }
}
