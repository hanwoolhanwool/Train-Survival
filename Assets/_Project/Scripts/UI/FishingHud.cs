using Game.Core.Events;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 낚시 상태 표시 (바다 지역 구현 계획 §7).
    ///
    /// <para><b>왜 필수인가.</b> 챔질 창은 1.2초다. 입질을 <b>알려주지 않으면 낚시가
    /// 성립하지 않는다</b> — 물속에서 벌어지는 일이라 화면에 단서가 없다.</para>
    ///
    /// <para>소유자 로컬 표현 전용이다. 국면은 <see cref="FishingRodController"/>가
    /// 이벤트로 흘려보내고, 여기서는 그리기만 한다.</para>
    /// </summary>
    public sealed class FishingHud : MonoBehaviour
    {
        private const float BiteFlashSeconds = 1.6f;
        private const float CatchFlashSeconds = 1.8f;

        private FishingPhase _phase = FishingPhase.Idle;
        private float _biteAt = -99f;
        private float _catchAt = -99f;
        private int _catchCount;


        private void OnEnable()
        {
            EventBus<FishingPhaseChangedLocalEvent>.Subscribe(OnPhaseChanged);
            EventBus<FishCaughtLocalEvent>.Subscribe(OnCaught);
        }

        private void OnDisable()
        {
            EventBus<FishingPhaseChangedLocalEvent>.Unsubscribe(OnPhaseChanged);
            EventBus<FishCaughtLocalEvent>.Unsubscribe(OnCaught);
        }

        private void OnPhaseChanged(FishingPhaseChangedLocalEvent e)
        {
            _phase = e.Phase;
            if (e.Phase == FishingPhase.Biting)
            {
                _biteAt = Time.time;
            }
        }

        private void OnCaught(FishCaughtLocalEvent e)
        {
            _catchAt = Time.time;
            _catchCount = e.Count;
        }

        private void OnGUI()
        {
            float w = Screen.width;
            float centerX = w * 0.5f;

            // 입질 — 챔질 창이 짧으므로 크고 눈에 띄게.
            if (_phase == FishingPhase.Biting && Time.time - _biteAt < BiteFlashSeconds)
            {
                var rect = new Rect(centerX - 200f, Screen.height * 0.34f, 400f, 60f);
                InGameUi.Prompt(rect, "<size=32><b>입질!  왼쪽 클릭</b></size>", "G_Action_FishBite");
            }
            else if (_phase == FishingPhase.Waiting)
            {
                var rect = new Rect(centerX - 200f, Screen.height * 0.36f, 400f, 34f);
                InGameUi.Prompt(rect, "…기다리는 중", "I_Item_FishingRod");
            }

            if (Time.time - _catchAt < CatchFlashSeconds)
            {
                var rect = new Rect(centerX - 200f, Screen.height * 0.42f, 400f, 34f);
                InGameUi.Prompt(rect, $"생선 +{_catchCount}", "I_Res_Fish");
            }
        }

    }
}
