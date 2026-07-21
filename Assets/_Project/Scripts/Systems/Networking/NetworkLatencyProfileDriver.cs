using Game.Core.Services;
using Unity.Multiplayer.Tools.NetworkSimulator.Runtime;
using UnityEngine;

namespace Game.Systems.Networking
{
    /// <summary>
    /// M1 손맛 검증의 측정 프로파일 (슬라이스 스펙 §3.1).
    /// UTP 시뮬레이터는 이 엔드포인트의 송·수신 양방향에 지연을 적용하므로,
    /// 프리셋의 PacketDelayMs는 편도 값이고 체감 RTT는 그 2배가 된다.
    /// </summary>
    public enum LatencyProfile
    {
        /// <summary>① 기준선 — 인공 지연 없음.</summary>
        None = 0,

        /// <summary>② 국내 일반 — RTT 60 ms + 지터 ±10 ms. 통과 판정 기준 프로파일.</summary>
        DomesticRtt60 = 1,

        /// <summary>③ 악조건 — RTT 120 ms + 지터 ±20 ms + 패킷로스 2 %. 참고 계측용.</summary>
        WorstRtt120 = 2,
    }

    /// <summary>
    /// 슬라이스 스펙 §3.1의 인공 지연 프로파일을 <b>순수 클라이언트로 접속한 인스턴스에만</b> 적용하는
    /// 드라이버. 측정 대상은 항상 클라이언트이므로(호스트는 지연 0) 호스트 인스턴스는 자동으로 None이 된다.
    /// 시뮬레이터는 에디터·개발 빌드에서만 동작한다 (릴리스 빌드에서는 UTP 시뮬레이터 스테이지 자체가 빠짐).
    /// 적용 결과는 Q2 로그로 교차 검증한다 — 프로파일 ②에서 Q2가 60~100 ms 대역이면 정상 적용된 것.
    /// </summary>
    [RequireComponent(typeof(NetworkSimulator))]
    public sealed class NetworkLatencyProfileDriver : MonoBehaviour
    {
        [SerializeField] private LatencyProfile _clientProfile = LatencyProfile.DomesticRtt60;

        private NetworkSimulator _simulator;
        private LatencyProfile _appliedProfile = LatencyProfile.None;

        private void Awake()
        {
            _simulator = GetComponent<NetworkSimulator>();
        }

        private void Update()
        {
            if (!ServiceLocator.TryGet(out INetworkSessionService session))
            {
                return;
            }

            // 인스펙터에서 플레이 중 프로파일을 바꿔도 다음 프레임에 반영된다.
            LatencyProfile desired = session.IsSessionActive && !session.IsHost
                ? _clientProfile
                : LatencyProfile.None;

            if (desired == _appliedProfile)
            {
                return;
            }

            _appliedProfile = desired;
            _simulator.ConnectionPreset = CreatePreset(desired);
            Debug.Log($"[NetworkLatencyProfileDriver] 지연 프로파일 적용: {desired} ({DescribePreset(desired)})");
        }

        private static INetworkSimulatorPreset CreatePreset(LatencyProfile profile)
        {
            switch (profile)
            {
                case LatencyProfile.DomesticRtt60:
                    return NetworkSimulatorPreset.Create(
                        "Slice Profile ② (RTT 60)", "국내 일반 — 슬라이스 스펙 §3.1",
                        packetDelayMs: 30, packetJitterMs: 5);

                case LatencyProfile.WorstRtt120:
                    return NetworkSimulatorPreset.Create(
                        "Slice Profile ③ (RTT 120)", "악조건 — 슬라이스 스펙 §3.1",
                        packetDelayMs: 60, packetJitterMs: 10, packetLossPercent: 2);

                default:
                    return NetworkSimulatorPresets.None;
            }
        }

        private static string DescribePreset(LatencyProfile profile)
        {
            switch (profile)
            {
                case LatencyProfile.DomesticRtt60:
                    return "편도 30 ms ± 5 → RTT 약 60 ms ± 10";

                case LatencyProfile.WorstRtt120:
                    return "편도 60 ms ± 10 + 로스 2 % → RTT 약 120 ms ± 20";

                default:
                    return "인공 지연 없음";
            }
        }
    }
}
