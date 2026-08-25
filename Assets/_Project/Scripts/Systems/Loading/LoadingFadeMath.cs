using UnityEngine;

namespace Game.Systems.Loading
{
    /// <summary>
    /// 로딩 화면이 떠 있는 <b>시간</b>의 규칙 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §8.3.
    ///
    /// <para><b>최소 표시 시간이 있는 이유</b>: 1인 플레이·빠른 PC에서는 로딩이 눈 깜짝할 사이에
    /// 끝나 화면이 <b>깜빡이기만 한다</b> — 그게 "빨라 보인다"가 아니라 "뭔가 잘못됐다"로 읽힌다.</para>
    ///
    /// <para><b>페이드가 비대칭인 이유</b>: 들어올 때는 대기실을 덮는 것이라 짧게 끊고(0.15초),
    /// 나갈 때는 <b>인게임 첫 화면으로 열리는 느낌</b>이라 조금 더 길게 둔다(0.35초).</para>
    ///
    /// <para>전부 순수 함수다 — 시간을 읽지 않고 인자로 받는다.</para>
    /// </summary>
    public static class LoadingFadeMath
    {
        /// <summary>대기실을 덮는 시간 (초).</summary>
        public const float FadeInSeconds = 0.15f;

        /// <summary>인게임으로 열리는 시간 (초).</summary>
        public const float FadeOutSeconds = 0.35f;

        /// <summary>화면이 최소한 떠 있어야 하는 시간 (초) — 깜빡임 방지.</summary>
        public const float MinVisibleSeconds = 0.6f;

        /// <summary>
        /// 출발 단계가 머물러야 하는 시간. <b>최소 표시 시간의 잔여</b>와 <b>페이드 아웃 길이</b> 중
        /// 긴 쪽이다 — 짧은 쪽을 고르면 둘 중 하나가 잘린다.
        /// </summary>
        /// <param name="visibleSecondsAtDepart">출발 단계에 들어선 시점까지 화면이 떠 있던 시간.</param>
        public static float DepartSeconds(float visibleSecondsAtDepart)
        {
            float remainingMinimum = MinVisibleSeconds - Mathf.Max(0f, visibleSecondsAtDepart);
            return Mathf.Max(remainingMinimum, FadeOutSeconds);
        }

        /// <summary>
        /// 지금 화면의 불투명도 (0~1).
        ///
        /// <para><b>페이드 아웃은 출발 단계의 끝에 붙는다.</b> 시작에 붙이면 최소 표시 시간이
        /// 긴 경우(빠른 로딩) 화면이 <b>투명해진 채로 남은 시간을 서 있게</b> 된다.</para>
        /// </summary>
        /// <param name="visibleSeconds">화면이 떠 있은 총 시간.</param>
        /// <param name="departElapsed">출발 단계에 들어선 뒤 지난 시간.</param>
        /// <param name="departTotal"><see cref="DepartSeconds"/>가 정한 출발 단계 길이.</param>
        /// <param name="departing">지금이 출발 단계인가.</param>
        public static float Alpha(
            float visibleSeconds, float departElapsed, float departTotal, bool departing)
        {
            float fadeIn = FadeInSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(visibleSeconds / FadeInSeconds);

            if (!departing)
            {
                return fadeIn;
            }

            float remaining = departTotal - departElapsed;
            float fadeOut = FadeOutSeconds <= 0f
                ? 0f
                : Mathf.Clamp01(remaining / FadeOutSeconds);

            // 들어오는 중에 곧바로 나가게 되면(아주 짧은 로딩) 낮은 쪽을 따른다 — 튀지 않는다.
            return Mathf.Min(fadeIn, fadeOut);
        }
    }
}
