using System.Collections.Generic;

namespace Game.Systems.Networking
{
    /// <summary>
    /// <see cref="IConnectionIdentityRegistry"/> 구현 — 승인 콜백에서 기록되는 호스트 세션 상태.
    /// 매핑은 끊김 즉시 지우지 않고 세션 단위로 유지한다(호스트 시작 시 Clear) —
    /// 끊김 시점 스냅샷 캡처(OnNetworkDespawn)가 clientId → 토큰 조회를 해야 하기 때문.
    /// </summary>
    public sealed class ConnectionIdentityRegistry : IConnectionIdentityRegistry
    {
        private readonly Dictionary<ulong, string> _tokenByClientId = new Dictionary<ulong, string>();
        private readonly Dictionary<string, ulong> _clientIdByToken = new Dictionary<string, ulong>();

        public bool TryGetToken(ulong clientId, out string token)
        {
            return _tokenByClientId.TryGetValue(clientId, out token);
        }

        /// <summary>토큰의 가장 최근 clientId를 찾는다. 중복 접속 킥 판정용 (결정 ⑥).</summary>
        public bool TryGetClientId(string token, out ulong clientId)
        {
            return _clientIdByToken.TryGetValue(token, out clientId);
        }

        /// <summary>
        /// 승인 시 매핑을 기록한다. 토큰 → clientId는 최신 접속으로 대체되지만,
        /// <b>이전 clientId → 토큰 매핑은 지우지 않는다</b>.
        ///
        /// <para>지우면 재접속 시 아이템이 사라진다 (M7 3차 검증 발견): 유령 연결을 킥하는
        /// 경로(<see cref="NgoNetworkSessionService"/> 중복 토큰 처리)는 <c>DisconnectClient</c> 직후
        /// 이 메서드를 부르는데, 킥당한 쪽의 despawn이 그보다 늦게 처리되면
        /// <see cref="Game.Gameplay.Session.PlayerStateSnapshotOps"/>의 캡처가 토큰을 찾지 못해
        /// <b>스냅샷이 통째로 유실</b>된다. 그 결과 새 접속은 복원할 것이 없어 초기 지급 상태가 된다.</para>
        ///
        /// <para>남은 매핑은 세션당 접속 수만큼이고(슬롯 상한 4인 × 재접속 횟수) clientId는 NGO에서
        /// 재사용되지 않으므로 오염되지 않는다 — <see cref="Clear"/>가 세션 경계에서 비운다.</para>
        /// </summary>
        public void Record(ulong clientId, string token)
        {
            _tokenByClientId[clientId] = token;
            _clientIdByToken[token] = clientId;
        }

        /// <summary>모든 매핑을 지운다. 호스트 세션 시작·종료 시 호출.</summary>
        public void Clear()
        {
            _tokenByClientId.Clear();
            _clientIdByToken.Clear();
        }
    }
}
