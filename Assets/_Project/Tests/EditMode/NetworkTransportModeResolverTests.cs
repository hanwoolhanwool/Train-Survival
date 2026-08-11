using Game.Systems.Networking;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>트랜스포트 모드 결정·초대 부팅 인자 해석 검증 (M6 2차 결정 ②).</summary>
    public sealed class NetworkTransportModeResolverTests
    {
        [Test]
        public void 인자가_없으면_UnityTransport_직결이다()
        {
            Assert.That(NetworkTransportModeResolver.Resolve(null),
                Is.EqualTo(NetworkTransportMode.UnityDirect));
            Assert.That(NetworkTransportModeResolver.Resolve(new string[0]),
                Is.EqualTo(NetworkTransportMode.UnityDirect));
            Assert.That(NetworkTransportModeResolver.Resolve(new[] { "game.exe", "-batchmode" }),
                Is.EqualTo(NetworkTransportMode.UnityDirect));
        }

        [Test]
        public void steam_인자는_대소문자_무관하게_Steam_모드다()
        {
            Assert.That(NetworkTransportModeResolver.Resolve(new[] { "game.exe", "-steam" }),
                Is.EqualTo(NetworkTransportMode.SteamRelay));
            Assert.That(NetworkTransportModeResolver.Resolve(new[] { "-STEAM" }),
                Is.EqualTo(NetworkTransportMode.SteamRelay));
        }

        [Test]
        public void 초대_부팅_인자도_Steam_모드로_간주한다()
        {
            // 초대로 켜진 게임이 UnityTransport로 부팅하면 참가가 불가능하다.
            Assert.That(NetworkTransportModeResolver.Resolve(new[] { "game.exe", "+connect_lobby", "109775241" }),
                Is.EqualTo(NetworkTransportMode.SteamRelay));
        }

        [Test]
        public void 초대_부팅_로비_id를_해석한다()
        {
            bool found = NetworkTransportModeResolver.TryGetConnectLobbyId(
                new[] { "game.exe", "+connect_lobby", "109775241791635823" }, out ulong lobbyId);

            Assert.That(found, Is.True);
            Assert.That(lobbyId, Is.EqualTo(109775241791635823ul));
        }

        [Test]
        public void 로비_id가_없거나_잘못되면_해석하지_않는다()
        {
            Assert.That(NetworkTransportModeResolver.TryGetConnectLobbyId(null, out _), Is.False);
            Assert.That(NetworkTransportModeResolver.TryGetConnectLobbyId(
                new[] { "+connect_lobby" }, out _), Is.False);
            Assert.That(NetworkTransportModeResolver.TryGetConnectLobbyId(
                new[] { "+connect_lobby", "abc" }, out _), Is.False);
            Assert.That(NetworkTransportModeResolver.TryGetConnectLobbyId(
                new[] { "+connect_lobby", "0" }, out _), Is.False);
        }
    }
}
