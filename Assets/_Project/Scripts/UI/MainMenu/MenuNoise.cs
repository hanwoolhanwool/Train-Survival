using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 로비 화면을 "살아 있게" 만드는 <b>저주파 흔들림</b> 한 곳 —
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §6.2 · §6.4.
    ///
    /// <para>카메라 드리프트와 창문 명멸은 겉보기엔 다른 연출이지만 계산은 같다 —
    /// <b>느리고, 끊기지 않고, 범위를 벗어나지 않는 잡음</b>. 두 곳에 따로 쓰면 한쪽만
    /// 고쳐지거나 주기가 어긋나므로 여기 모은다.</para>
    ///
    /// <para><b>랜덤이 아니라 잡음이어야 한다.</b> 프레임마다 난수를 뽑으면 카메라는 떨고
    /// 창문은 깜빡인다. 펄린 잡음은 시간에 대해 연속이라 같은 진폭이어도 <b>흐르는</b> 것처럼 보인다.</para>
    ///
    /// <para>시간을 인자로 받는 순수 함수라 EditMode에서 경계를 그대로 고정한다 —
    /// 진폭을 넘지 않는지, 튀지 않는지, 축끼리 같이 움직이지는 않는지.</para>
    /// </summary>
    internal static class MenuNoise
    {
        /// <summary>축·채널을 서로 떼어 놓기 위한 시드 간격. 너무 좁으면 두 축이 같이 움직인다.</summary>
        public const float SeedStride = 37.19f;

        /// <summary>
        /// −1 ~ 1 사이를 느리게 오가는 값. <paramref name="periodSeconds"/>가 한 번 오가는 대략의 주기다.
        /// </summary>
        public static float Wave(float time, float periodSeconds, float seed)
        {
            if (periodSeconds <= 0f)
            {
                return 0f;
            }

            // Mathf.PerlinNoise 는 0~1을 "약간" 벗어날 수 있다고 문서에 적혀 있다.
            // 진폭 보장이 이 함수의 계약이므로(카메라가 배경 밖을 비추면 안 된다) 여기서 잘라 낸다.
            float t = time / periodSeconds;
            return Mathf.Clamp(Mathf.PerlinNoise(t + seed, seed * 0.37f) * 2f - 1f, -1f, 1f);
        }

        /// <summary>
        /// 축마다 다른 주기로 흔들리는 변위 — 세 축이 같은 위상으로 움직이면 흔들림이 아니라
        /// 한 방향 이동으로 읽히므로 시드를 <see cref="SeedStride"/>만큼 벌린다.
        /// </summary>
        public static Vector3 Drift(float time, Vector3 amplitude, Vector3 periods, float seed)
        {
            return new Vector3(
                amplitude.x * Wave(time, periods.x, seed),
                amplitude.y * Wave(time, periods.y, seed + SeedStride),
                amplitude.z * Wave(time, periods.z, seed + SeedStride * 2f));
        }

        /// <summary>
        /// <paramref name="min"/> ~ <paramref name="max"/> 사이를 느리게 오가는 밝기 배율.
        /// 창문 불빛처럼 <b>꺼지지는 않고 흔들리기만</b> 하는 값에 쓴다.
        /// </summary>
        public static float Flicker(float time, float min, float max, float periodSeconds, float seed)
        {
            float lo = Mathf.Min(min, max);
            float hi = Mathf.Max(min, max);
            float unit = (Wave(time, periodSeconds, seed) + 1f) * 0.5f;   // 0~1
            return Mathf.Lerp(lo, hi, Mathf.Clamp01(unit));
        }
    }
}
