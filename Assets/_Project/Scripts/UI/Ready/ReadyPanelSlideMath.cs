using UnityEngine;

namespace Game.UI.Ready
{
    /// <summary>
    /// 패널이 제자리로 들어오는 곡선 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §9.1.
    ///
    /// <para><b>제자리를 살짝 지나쳤다 돌아온다.</b> 간판을 내려 걸면 줄이 한 번 튕기고,
    /// 무거운 것을 옆으로 당기면 관성으로 조금 더 간다 — 그 반동이 없으면 패널이
    /// <b>미끄러져 붙는 것이 아니라 그냥 나타난다.</b></para>
    ///
    /// <para><b>반동은 아주 살짝이다.</b> 표준 <c>easeOutBack</c>의 계수 1.70158은 10 % 가까이
    /// 튀어 장난스러워진다. 이 화면은 무쇠와 황동의 화면이라 3 % 남짓이면 충분하다 —
    /// "튕겼다"가 아니라 "묵직하게 자리를 잡았다"로 읽혀야 한다.</para>
    ///
    /// <para>순수 계산이라 EditMode가 경계를 고정한다.</para>
    /// </summary>
    internal static class ReadyPanelSlideMath
    {
        /// <summary>기본 반동 세기 — 최대 3 % 남짓 지나친다.</summary>
        public const float DefaultBack = 0.6f;

        /// <summary>
        /// 0(출발)에서 1(제자리)로 가는 진행도. <paramref name="back"/>이 0보다 크면
        /// 도중에 <b>1을 넘었다가</b> 돌아온다.
        /// </summary>
        /// <param name="t">0~1 바깥은 접어 넣는다.</param>
        /// <param name="back">반동 세기. 0이면 반동 없이 감속만 한다.</param>
        public static float Ease(float t, float back)
        {
            float x = Mathf.Clamp01(t);
            float c1 = back < 0f ? 0f : back;
            float c3 = c1 + 1f;
            float u = x - 1f;

            // easeOutBack — u=−1(출발)에서 0, u=0(끝)에서 정확히 1이 된다.
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>
        /// 남은 거리 비율 — 출발 지점에서 얼마나 떨어져 있는지. 오프셋에 그대로 곱한다.
        /// 반동 구간에서는 <b>음수</b>가 되어 제자리 반대편으로 조금 넘어간다.
        /// </summary>
        public static float Remaining(float t, float back)
        {
            return 1f - Ease(t, back);
        }
    }
}
