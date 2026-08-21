using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Game.Systems.Networking
{
    /// <summary>
    /// 직결 모드에서 <b>남들이 이 방에 접속할 때 쓰는 주소</b> —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §6.2 · §12 미결 8번.
    ///
    /// <para>준비 화면의 "초대 하기"가 이 문자열을 클립보드에 넣는다. Steam 모드에서는
    /// 오버레이가 초대를 맡으므로 부를 일이 없다.</para>
    ///
    /// <para><b>지어내지 않고 트랜스포트가 실제로 물고 있는 값을 읽는다.</b> §6.2는
    /// "바인드 주소가 아니라 실제 접속 가능한 주소"를 요구했고, 4차에서 확인해 보니
    /// <b>이 프로젝트의 호스트는 루프백에만 귀를 연다</b> — Boot 씬 <c>UnityTransport</c>의
    /// <c>ServerListenAddress</c>가 <c>127.0.0.1</c>이다. 그래서 지금 LAN IPv4를 골라 주면
    /// <b>붙지 않는 주소를 건네는 셈</b>이 된다. 직결은 같은 PC에서 두 벌 띄우는 개발 전용
    /// 모드이므로(§7.3), 여기서는 바인드 값을 그대로 보여 준다.</para>
    ///
    /// <para>LAN에서 쓰려면 먼저 <c>ServerListenAddress</c>를 <c>0.0.0.0</c>으로 열어야 하고,
    /// 그때 이 함수가 LAN IPv4를 골라야 한다 — <b>순서가 반대면 아무것도 고쳐지지 않는다.</b></para>
    /// </summary>
    public static class HostJoinAddress
    {
        /// <summary>
        /// "주소:포트" 문자열. 트랜스포트를 읽지 못하면 넘겨받은 기본값을 쓴다.
        /// </summary>
        /// <param name="fallbackAddress">읽지 못했을 때 쓸 주소.</param>
        /// <param name="fallbackPort">읽지 못했을 때 쓸 포트.</param>
        public static string Resolve(string fallbackAddress, ushort fallbackPort)
        {
            string address = fallbackAddress;
            ushort port = fallbackPort;

            UnityTransport transport = FindTransport();
            if (transport != null)
            {
                if (!string.IsNullOrWhiteSpace(transport.ConnectionData.Address))
                {
                    address = transport.ConnectionData.Address;
                }

                if (transport.ConnectionData.Port != 0)
                {
                    port = transport.ConnectionData.Port;
                }
            }

            return address + ":" + port;
        }

        private static UnityTransport FindTransport()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return null;
            }

            // 두 트랜스포트가 공존하므로(M6 2차 결정 ②) 지금 쓰이는 쪽을 먼저 본다.
            var active = networkManager.NetworkConfig != null
                ? networkManager.NetworkConfig.NetworkTransport as UnityTransport
                : null;

            return active != null ? active : networkManager.GetComponent<UnityTransport>();
        }
    }
}
