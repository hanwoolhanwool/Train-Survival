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

        /// <summary>승인 시 매핑을 기록한다. 같은 토큰의 이전 clientId 매핑은 최신 접속으로 대체된다.</summary>
        public void Record(ulong clientId, string token)
        {
            if (_clientIdByToken.TryGetValue(token, out ulong previousClientId))
            {
                _tokenByClientId.Remove(previousClientId);
            }

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
