using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Game.Systems.Networking.Lobby
{
    /// <summary>
    /// 대기실의 네트워크 상태 — 누가 방에 있는지 한 줄로 나른다.
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §7.2.
    ///
    /// <para><b>실어 나르는 것은 클라이언트 id뿐이다.</b> 표시 이름은 칸 번호에서 파생하므로
    /// (<see cref="RosterOrdering.DisplayName"/>) 리스트에 실을 것이 없고, 호스트 여부도
    /// "첫 칸이 호스트"라는 규칙에서 나온다. 계획 §7.2는 이름·호스트 여부·접속 상태를 함께
    /// 나르는 그림이었지만, 셋 다 <b>id 목록에서 파생</b>돼 필드가 사라졌다.</para>
    ///
    /// <para><b>목록은 호스트만 쓴다.</b> 그것도 <see cref="NetworkManager.ConnectedClientsIds"/>에서만
    /// 파생시킨다 — 클라이언트가 자기 칸을 직접 주장하게 두면 유령 칸이 남거나 중복된다
    /// (§10 리스크 6).</para>
    ///
    /// <para><see cref="NetworkList{T}"/>를 고른 이유는 <b>늦게 들어온 사람</b> 때문이다. 스폰 시점에
    /// 전체 상태가 자동으로 전달되므로, 초기 동기화를 손으로 짜다 틀리는 자리가 아예 없다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyRoomState : NetworkBehaviour
    {
        /// <summary>지금 살아 있는 대기실 상태. 스폰된 것은 언제나 하나뿐이다.</summary>
        public static LobbyRoomState Current { get; private set; }

        /// <summary>스폰·디스폰으로 <see cref="Current"/>가 바뀌었다.</summary>
        public static event Action CurrentChanged;

        private readonly NetworkList<ulong> _members = new NetworkList<ulong>();

        /// <summary>서버가 보는 접속 순서 — <see cref="NetworkManager.ConnectedClientsIds"/>는
        /// 순서를 약속하지 않으므로 들어온 차례를 따로 쌓는다.</summary>
        private readonly List<ulong> _order = new List<ulong>();

        private readonly ulong[] _slots = new ulong[RosterOrdering.Capacity];

        /// <summary>멤버 목록이 바뀌었다 (호스트·게스트 공통).</summary>
        public event Action Changed;

        /// <summary>지금 방에 있는 사람 수.</summary>
        public int MemberCount => _members.Count;

        /// <summary>칸에 앉은 사람의 클라이언트 id. 빈 칸이면 <c>false</c>.</summary>
        public bool TryGetMember(int slot, out ulong clientId)
        {
            if (slot < 0 || slot >= _members.Count)
            {
                clientId = 0;
                return false;
            }

            clientId = _members[slot];
            return true;
        }

        public override void OnNetworkSpawn()
        {
            Current = this;
            _members.OnListChanged += OnMembersChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

                _order.Clear();
                foreach (ulong id in NetworkManager.ConnectedClientsIds)
                {
                    _order.Add(id);
                }

                Rebuild();
            }

            CurrentChanged?.Invoke();
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _members.OnListChanged -= OnMembersChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            if (Current == this)
            {
                Current = null;
                CurrentChanged?.Invoke();
            }

            Changed?.Invoke();
        }

        private void OnMembersChanged(NetworkListEvent<ulong> _)
        {
            Changed?.Invoke();
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!_order.Contains(clientId))
            {
                _order.Add(clientId);
            }

            Rebuild();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            _order.Remove(clientId);
            Rebuild();
        }

        /// <summary>
        /// 접속 순서를 칸에 앉히고 목록을 그 결과로 맞춘다.
        ///
        /// <para>지우고 다시 채우지 않고 <b>다른 곳만 고친다</b> — <see cref="NetworkList{T}"/>는
        /// 바뀐 항목만 델타로 보내므로, 한 명이 나갔을 때 넷을 전부 다시 보내는 낭비를 피한다.</para>
        /// </summary>
        private void Rebuild()
        {
            if (!IsServer)
            {
                return;
            }

            ulong hostId = NetworkManager != null ? NetworkManager.LocalClientId : 0UL;
            int count = RosterOrdering.Arrange(_order, hostId, _slots);

            for (int i = 0; i < count; i++)
            {
                if (i < _members.Count)
                {
                    if (_members[i] != _slots[i])
                    {
                        _members[i] = _slots[i];
                    }
                }
                else
                {
                    _members.Add(_slots[i]);
                }
            }

            for (int i = _members.Count - 1; i >= count; i--)
            {
                _members.RemoveAt(i);
            }
        }
    }
}
