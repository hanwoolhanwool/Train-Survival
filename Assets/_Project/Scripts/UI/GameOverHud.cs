using Game.Core.Events;
using Game.Gameplay.Player;
using Game.Gameplay.Session;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 게임오버(전멸) 결과 오버레이 (M6 3차) — <see cref="GameOverEvent"/> 수신 시 표시.
    /// 결과를 보는 동안 세션은 유지되고, "메인 화면으로"가 각자 세션을 내린다
    /// (호스트가 먼저 나가면 남은 피어는 게임오버 화면의 같은 버튼으로 복귀 — 세션 종료
    /// 오버레이는 <see cref="SessionExitHud"/>가 게임오버 중 억제한다).
    /// Game 씬 HUD 오브젝트(SliceHud)에 부착한다.
    /// </summary>
    public sealed class GameOverHud : MonoBehaviour
    {
        private const string MainSceneName = "Main";

        private bool _visible;
        private int _dayReached;
        private float _elapsedSeconds;

        /// <summary>이탈 절차가 진행 중인가 — 셧다운을 기다리는 동안 버튼이 다시 눌리는 것을 막는다.</summary>
        private bool _leaving;

        private void OnEnable()
        {
            EventBus<GameOverEvent>.Subscribe(OnGameOver);
        }

        private void OnDisable()
        {
            EventBus<GameOverEvent>.Unsubscribe(OnGameOver);
        }

        private void OnGameOver(GameOverEvent evt)
        {
            _visible = true;
            _dayReached = evt.DayReached;
            _elapsedSeconds = evt.ElapsedSeconds;

            // 시점 회전 정지 + 커서 해제 — 세션 메뉴와 같은 로컬 규약을 재사용한다
            // (전원 사망 상태라 이동 입력은 이미 부활 대기가 막고 있다).
            EventBus<SessionMenuToggledLocalEvent>.Publish(new SessionMenuToggledLocalEvent(true));
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            var box = new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.35f, 360f, 168f);
            GUI.Box(box, "전멸 — 게임오버");

            int minutes = Mathf.FloorToInt(_elapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(_elapsedSeconds % 60f);
            GUI.Label(new Rect(box.x + 20f, box.y + 36f, box.width - 40f, 24f),
                $"도달: Day {_dayReached}");
            GUI.Label(new Rect(box.x + 20f, box.y + 62f, box.width - 40f, 24f),
                $"생존 시간: {minutes}분 {seconds:D2}초");

            if (GUI.Button(new Rect(box.x + 20f, box.y + 104f, box.width - 40f, 36f), "메인 화면으로"))
            {
                LeaveToMain();
            }
        }

        /// <summary>로컬 세션을 내리고 Main 씬으로 — <see cref="SessionExitHud"/>의 복귀 경로와 동일하다.</summary>
        /// <summary>
        /// 세션을 내리고 메인으로 — <b>셧다운 완료를 기다린 뒤</b> 로드한다 (잔여 문서 §5 ⑤-a).
        /// 같은 프레임에 로드하면 이탈 통지가 전송 전에 잘려 호스트에 유령이 남는다.
        /// </summary>
        private void LeaveToMain()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            StartCoroutine(SessionExitFlow.ShutdownThenLoadMain(MainSceneName));
        }
    }
}
