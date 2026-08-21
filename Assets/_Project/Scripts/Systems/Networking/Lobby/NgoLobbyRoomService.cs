using System;
using Unity.Netcode;
using UnityEngine;

namespace Game.Systems.Networking.Lobby
{
    /// <summary>
    /// NGO 기반 <see cref="ILobbyRoomService"/> 구현 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §6.1 · §7.2.
    ///
    /// <para>스폰된 <see cref="LobbyRoomState"/> 하나를 바라보고, 그것이 없으면
    /// <see cref="IsActive"/>가 거짓이다. 상태 객체는 세션과 함께 나고 죽으므로
    /// 이 서비스는 <b>수명을 소유하지 않고 중계만</b> 한다.</para>
    ///
    /// <para><b>풀링을 태우지 않는다.</b> 대기실 상태는 세션당 하나뿐이고 재사용할 일이 없어
    /// <c>PoolManager</c>를 거칠 이유가 없다(§7.2).</para>
    /// </summary>
    public sealed class NgoLobbyRoomService : ILobbyRoomService
    {
        private readonly GameObject _statePrefab;
        private LobbyRoomState _bound;

        public NgoLobbyRoomService(GameObject statePrefab)
        {
            _statePrefab = statePrefab;
            LobbyRoomState.CurrentChanged += OnCurrentChanged;
            OnCurrentChanged();
        }

        public event Action Changed;

        public bool IsActive => LobbyRoomState.Current != null;

        public int MemberCount => LobbyRoomState.Current != null ? LobbyRoomState.Current.MemberCount : 0;

        public bool TryGetSlot(int slot, out string displayName, out bool isHost)
        {
            displayName = null;
            isHost = false;

            LobbyRoomState state = LobbyRoomState.Current;
            if (state == null || !state.TryGetMember(slot, out ulong _))
            {
                return false;
            }

            // 첫 칸이 호스트라는 규칙은 RosterOrdering이 세운다 — 여기서 다시 판정하지 않는다.
            displayName = RosterOrdering.DisplayName(slot);
            isHost = slot == 0;
            return true;
        }

        public bool Open()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return false;
            }

            if (LobbyRoomState.Current != null)
            {
                return true;
            }

            if (_statePrefab == null)
            {
                Debug.LogError("[NgoLobbyRoomService] 대기실 상태 프리팹이 연결되지 않았습니다. "
                    + "Boot 씬 GameBootstrapper 구성을 확인하세요.");
                return false;
            }

            GameObject instance = UnityEngine.Object.Instantiate(_statePrefab);
            var netObject = instance.GetComponent<NetworkObject>();
            if (netObject == null)
            {
                Debug.LogError("[NgoLobbyRoomService] 대기실 상태 프리팹에 NetworkObject가 없습니다.");
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            netObject.Spawn();
            return true;
        }

        public void Close()
        {
            LobbyRoomState state = LobbyRoomState.Current;
            if (state == null || state.NetworkObject == null || !state.IsServer)
            {
                return;
            }

            if (state.NetworkObject.IsSpawned)
            {
                state.NetworkObject.Despawn();
            }
        }

        private void OnCurrentChanged()
        {
            if (_bound != null)
            {
                _bound.Changed -= OnStateChanged;
            }

            _bound = LobbyRoomState.Current;
            if (_bound != null)
            {
                _bound.Changed += OnStateChanged;
            }

            OnStateChanged();
        }

        private void OnStateChanged()
        {
            Changed?.Invoke();
        }
    }
}
