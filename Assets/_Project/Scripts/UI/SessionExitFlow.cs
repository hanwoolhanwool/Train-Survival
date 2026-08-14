using System.Collections;
using Game.Core.Services;
using Game.Systems.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// 세션을 내리고 메인 씬으로 돌아가는 공통 절차 (M7 3차 검증 — 잔여 문서 §5 결정 ⑤-a).
    ///
    /// <para>NGO의 <c>Shutdown()</c>은 <b>지연 셧다운</b>이다 (<see cref="INetworkSessionService.Shutdown"/>
    /// 주석 — "호출 직후에는 IsSessionActive가 아직 true일 수 있다"). 호출 직후 <b>같은 프레임에</b>
    /// 씬을 로드하면 NetworkObject 파괴가 셧다운 절차와 경합해 <b>이탈 통지가 전송되기 전에
    /// 트랜스포트가 정리</b>될 수 있다. 그러면 호스트는 끊김을 감지하지 못해 접속 타임아웃
    /// (Boot 씬 30초)까지 유령 플레이어가 남는다.</para>
    ///
    /// <para>유령이 남으면 피해가 이탈 자체로 끝나지 않는다 — 재접속이 <b>중복 토큰 킥</b> 경로를
    /// 타면서 끊김 스냅샷 캡처와 새 플레이어 복원의 순서가 흔들려, 아이템이 통째로 사라질 수 있다
    /// (M7 3차 검증 실측). 그래서 <b>세션이 실제로 내려간 뒤에</b> 씬을 로드한다.</para>
    ///
    /// <para>정리가 끝내 오지 않는 경우를 위해 짧은 상한을 둔다 — <b>메인 화면 복귀 자체는 어떤
    /// 경우에도 성립해야 한다</b>. 플레이어를 게임 씬에 가두는 것이 유령보다 나쁘다.</para>
    /// </summary>
    internal static class SessionExitFlow
    {
        /// <summary>셧다운 완료를 기다리는 상한 (초). 정상 경로는 1~2프레임이면 끝난다.</summary>
        private const float ShutdownTimeoutSeconds = 2f;

        /// <summary>서버가 이탈 통지를 받아 연결을 끊어 주기를 기다리는 상한 (초).</summary>
        private const float LeaveAckSeconds = 1f;

        /// <summary>
        /// 세션을 정리한 뒤 메인 씬을 로드한다. 세션이 없으면 즉시 로드한다.
        /// 절차는 <b>⑤-b → ⑤-a</b> 2단이다 — 게스트는 서버에 먼저 알리고(결정적),
        /// 그래도 남아 있으면 로컬 셧다운으로 마무리한다(호스트이거나 통지가 실패한 경우).
        /// </summary>
        public static IEnumerator ShutdownThenLoadMain(string mainSceneName)
        {
            if (ServiceLocator.TryGet(out INetworkSessionService session) && session.IsSessionActive)
            {
                // ⑤-b — 게스트는 서버에 명시적으로 알린다. 서버가 끊어 주는 경로가
                // despawn → 스냅샷 캡처를 확정적으로 태운다 (M7 3차 재검증 W1-a 실패 대응).
                if (TryNotifyServer())
                {
                    yield return WaitWhileActive(session, LeaveAckSeconds);
                }

                // ⑤-a — 아직 살아 있으면(호스트이거나 통지 실패) 로컬 셧다운 후 완료를 기다린다.
                // NGO의 Shutdown은 지연 셧다운이라 같은 프레임에 씬을 로드하면 이탈 통지가
                // 전송 전에 트랜스포트와 함께 정리된다.
                if (session.IsSessionActive)
                {
                    session.Shutdown();
                    yield return WaitWhileActive(session, ShutdownTimeoutSeconds);
                }

                if (session.IsSessionActive)
                {
                    Debug.LogWarning("[SessionExitFlow] 세션 정리가 제때 끝나지 않았습니다 — "
                        + "메인 화면 복귀는 그대로 진행합니다(호스트에 유령이 남을 수 있음).");
                }
            }

            SceneManager.LoadScene(mainSceneName);
        }

        /// <summary>세션이 내려갈 때까지, 늦어도 상한까지 기다린다. timeScale 0에서도 흐르도록 실시간으로 잰다.</summary>
        private static IEnumerator WaitWhileActive(INetworkSessionService session, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (session.IsSessionActive && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        /// <summary>
        /// 게스트라면 서버에 이탈을 알린다 (⑤-b). 호스트·비접속·플레이어 오브젝트 부재면 false —
        /// 그 경우 호출부가 로컬 셧다운으로 넘어간다.
        /// UI는 Netcode를 참조하지 않으므로 실제 발신은 Gameplay 쪽 경계가 담당한다.
        /// </summary>
        private static bool TryNotifyServer()
        {
            return Gameplay.Session.SessionLeaveNotifier.TryNotifyServer();
        }
    }
}
