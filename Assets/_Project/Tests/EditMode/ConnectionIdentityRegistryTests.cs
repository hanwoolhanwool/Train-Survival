using Game.Systems.Networking;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 접속 식별 매핑 검증 (M6 1차 재접속 기반, M7 3차 검증에서 결함 발견).
    /// 고정하는 계약은 하나다: <b>새 접속을 기록해도 이전 clientId의 토큰 조회가 살아 있어야 한다</b> —
    /// 끊김 스냅샷 캡처(<c>PlayerSessionAgent.OnNetworkDespawn</c>)가 그 조회에 의존하기 때문이다.
    /// </summary>
    public sealed class ConnectionIdentityRegistryTests
    {
        private const string TokenA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string TokenB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [Test]
        public void 기록한_토큰을_clientId로_되찾는다()
        {
            var registry = new ConnectionIdentityRegistry();

            registry.Record(1UL, TokenA);

            Assert.That(registry.TryGetToken(1UL, out string token), Is.True);
            Assert.That(token, Is.EqualTo(TokenA));
            Assert.That(registry.TryGetClientId(TokenA, out ulong clientId), Is.True);
            Assert.That(clientId, Is.EqualTo(1UL));
        }

        [Test]
        public void 미등록_clientId와_토큰은_false다()
        {
            var registry = new ConnectionIdentityRegistry();

            Assert.That(registry.TryGetToken(99UL, out _), Is.False);
            Assert.That(registry.TryGetClientId(TokenA, out _), Is.False);
        }

        [Test]
        public void 재접속은_토큰의_clientId를_최신으로_바꾼다()
        {
            // 중복 접속 킥 판정(NgoNetworkSessionService)이 "이 토큰의 현재 연결"을 이 방향으로 찾는다.
            var registry = new ConnectionIdentityRegistry();

            registry.Record(1UL, TokenA);
            registry.Record(2UL, TokenA);

            Assert.That(registry.TryGetClientId(TokenA, out ulong clientId), Is.True);
            Assert.That(clientId, Is.EqualTo(2UL), "최신 접속이 이긴다");
        }

        [Test]
        public void 재접속을_기록해도_이전_clientId의_토큰_조회가_살아_있다()
        {
            // ★ 회귀 방지 (M7 3차 검증 발견) — 이 매핑을 지우면 재접속 시 아이템이 전부 사라진다.
            // 유령 연결 킥 경로는 DisconnectClient 직후 Record를 부르는데, 킥당한 쪽의 despawn이
            // 그보다 늦게 처리되면 캡처가 토큰을 찾지 못해 스냅샷이 유실되기 때문이다.
            var registry = new ConnectionIdentityRegistry();

            registry.Record(1UL, TokenA);
            registry.Record(2UL, TokenA);

            Assert.That(registry.TryGetToken(1UL, out string previous), Is.True,
                "뒤늦게 오는 despawn도 자기 토큰을 찾을 수 있어야 한다");
            Assert.That(previous, Is.EqualTo(TokenA));
            Assert.That(registry.TryGetToken(2UL, out string current), Is.True);
            Assert.That(current, Is.EqualTo(TokenA));
        }

        [Test]
        public void 서로_다른_토큰은_간섭하지_않는다()
        {
            var registry = new ConnectionIdentityRegistry();

            registry.Record(1UL, TokenA);
            registry.Record(2UL, TokenB);
            registry.Record(3UL, TokenA);

            Assert.That(registry.TryGetClientId(TokenA, out ulong a), Is.True);
            Assert.That(a, Is.EqualTo(3UL));
            Assert.That(registry.TryGetClientId(TokenB, out ulong b), Is.True);
            Assert.That(b, Is.EqualTo(2UL), "다른 토큰의 최신 연결은 그대로다");
            Assert.That(registry.TryGetToken(2UL, out string tokenOfTwo), Is.True);
            Assert.That(tokenOfTwo, Is.EqualTo(TokenB));
        }

        [Test]
        public void Clear는_세션_경계에서_전부_비운다()
        {
            // 다음 세션에 이전 세션의 매핑이 새어 들어가지 않게 한다 (호스트 시작·종료 시 호출).
            var registry = new ConnectionIdentityRegistry();

            registry.Record(1UL, TokenA);
            registry.Record(2UL, TokenB);
            registry.Clear();

            Assert.That(registry.TryGetToken(1UL, out _), Is.False);
            Assert.That(registry.TryGetToken(2UL, out _), Is.False);
            Assert.That(registry.TryGetClientId(TokenA, out _), Is.False);
            Assert.That(registry.TryGetClientId(TokenB, out _), Is.False);
        }
    }
}
