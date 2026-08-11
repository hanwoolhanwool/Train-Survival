using System.Collections.Generic;
using Game.Core.Services;
using Unity.Netcode;

namespace Game.Gameplay.Session
{
    /// <summary>
    /// 끊긴 플레이어의 상태 스냅샷 보관소 — 호스트 전용 (M6 1차 §2.2).
    /// 식별 토큰 → 스냅샷으로 보관하고, 같은 토큰의 재접속 스폰이 꺼내(<see cref="TryTake"/>) 적용한다.
    /// 보존 유예 = 세션 끝까지 (결정 ④ — 슬롯 상한 4인이라 부담 없음). 세션 종료 시 자동으로 비워
    /// 다음 세션에 이전 세션 스냅샷이 새어 들어가지 않게 한다.
    /// Gameplay 소속 — <see cref="PlayerStateSnapshot"/>이 Gameplay 타입(HotbarSlotView)을 담아
    /// Systems에 둘 수 없다 (어셈블리 단방향).
    /// </summary>
    public sealed class PlayerSessionRegistry
    {
        private readonly Dictionary<string, PlayerStateSnapshot> _snapshotsByToken =
            new Dictionary<string, PlayerStateSnapshot>();

        /// <summary>끊김 스냅샷 보관 — 같은 토큰의 이전 항목은 최신으로 대체된다.</summary>
        public void Store(string token, in PlayerStateSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(token))
            {
                _snapshotsByToken[token] = snapshot;
            }
        }

        /// <summary>토큰의 스냅샷을 꺼내며 제거한다 — 적용은 1회성이다. 없으면 false (= 신규 접속).</summary>
        public bool TryTake(string token, out PlayerStateSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(token) && _snapshotsByToken.TryGetValue(token, out snapshot))
            {
                _snapshotsByToken.Remove(token);
                return true;
            }

            snapshot = default;
            return false;
        }

        public void Clear()
        {
            _snapshotsByToken.Clear();
        }

        /// <summary>
        /// ServiceLocator에서 가져오거나, 없으면 생성·등록한다 (첫 사용 시점 등록 — Boot 일괄 등록은
        /// Systems 소속 GameBootstrapper가 Gameplay 타입을 알 수 없어 불가). 최초 생성 시 서버 종료
        /// 이벤트에 Clear를 걸어 세션 수명과 묶는다.
        /// </summary>
        public static PlayerSessionRegistry GetOrRegister()
        {
            if (ServiceLocator.TryGet(out PlayerSessionRegistry registry))
            {
                return registry;
            }

            registry = new PlayerSessionRegistry();
            ServiceLocator.Register(registry);

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.OnServerStopped += _ => registry.Clear();
            }

            return registry;
        }
    }
}
