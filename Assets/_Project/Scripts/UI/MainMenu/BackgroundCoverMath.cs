using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 로비 배경 한 장(<c>T_Menu_Background</c>)을 화면 종횡비와 무관하게 <b>여백 없이 덮는</b> 크기 계산 —
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §4.1-3.
    ///
    /// <para><b>왜 cover인가</b>: 배경은 밤의 정차역 한 장이고, 21:9에서 좌우에 검은 띠가 생기면
    /// 그림이 아니라 창으로 읽힌다. 그래서 <b>넘치게 두고 잘라낸다</b> — 종횡비를 유지한 채
    /// 가로·세로 중 <b>모자란 쪽</b>에 맞춰 확대한다.</para>
    ///
    /// <para><b>Screen이 아니라 Canvas 크기를 받는 이유</b>: <c>CanvasScaler</c>가 이미 화면을 기준
    /// 해상도로 환산한 뒤이므로, UI가 보는 프레임은 픽셀 해상도가 아니라 캔버스 rect다.
    /// 그 환산까지 <see cref="CanvasSize"/>로 여기 들여와 두 단계를 한 곳에서 검증한다.</para>
    ///
    /// <para><b>축소가 일어나지 않는 성질</b>: 기준 해상도와 소스가 모두 1920×1080이고
    /// match가 0.5면 <see cref="CoverScale"/>은 √(긴쪽비/짧은쪽비)가 되어 <b>항상 1 이상</b>이다.
    /// 즉 어느 종횡비에서도 원본을 1:1 아래로 줄여 쓰지 않는다 — 계획 §8.1의 고정 항목이다.</para>
    /// </summary>
    internal static class BackgroundCoverMath
    {
        /// <summary>CanvasScaler 기준 해상도 — 1080p를 최소 지원 해상도로 본다.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        /// <summary>
        /// CanvasScaler <c>matchWidthOrHeight</c> 기본값. 0.5(가로·세로 균등)여야
        /// 위 "축소 없음" 성질이 성립한다 — 0이나 1로 치우치면 한 축이 기준을 밑돈다.
        /// </summary>
        public const float DefaultMatch = 0.5f;

        /// <summary>
        /// <c>CanvasScaler.ScaleWithScreenSize</c> + <c>MatchWidthOrHeight</c>의 배율.
        /// 유니티 구현과 같은 식(로그 가중 평균)을 쓴다.
        /// </summary>
        public static float CanvasScaleFactor(Vector2 screenSize, Vector2 referenceResolution, float match)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f ||
                referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            {
                return 1f;
            }

            float logWidth = Mathf.Log(screenSize.x / referenceResolution.x, 2f);
            float logHeight = Mathf.Log(screenSize.y / referenceResolution.y, 2f);
            float logAverage = Mathf.Lerp(logWidth, logHeight, Mathf.Clamp01(match));
            return Mathf.Pow(2f, logAverage);
        }

        /// <summary>실제 픽셀 해상도가 CanvasScaler를 거쳐 UI에 보이는 캔버스 rect 크기.</summary>
        public static Vector2 CanvasSize(Vector2 screenSize, Vector2 referenceResolution, float match)
        {
            float scale = CanvasScaleFactor(screenSize, referenceResolution, match);
            return scale <= 0f ? screenSize : screenSize / scale;
        }

        /// <summary>기본 기준값(<see cref="ReferenceResolution"/>·<see cref="DefaultMatch"/>)으로 계산한 캔버스 rect 크기.</summary>
        public static Vector2 CanvasSize(Vector2 screenSize)
        {
            return CanvasSize(screenSize, ReferenceResolution, DefaultMatch);
        }

        /// <summary>
        /// 프레임을 빈틈없이 덮는 데 필요한 배율 — 가로·세로 필요 배율 중 <b>큰 쪽</b>.
        /// 작은 쪽을 고르면 그것이 곧 letterbox다.
        /// </summary>
        public static float CoverScale(Vector2 frameSize, Vector2 sourceSize)
        {
            if (sourceSize.x <= 0f || sourceSize.y <= 0f || frameSize.x <= 0f || frameSize.y <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(frameSize.x / sourceSize.x, frameSize.y / sourceSize.y);
        }

        /// <summary>프레임을 덮도록 확대한 이미지 크기. 소스 종횡비는 그대로 유지된다.</summary>
        public static Vector2 CoverSize(Vector2 frameSize, Vector2 sourceSize)
        {
            if (sourceSize.x <= 0f || sourceSize.y <= 0f)
            {
                return frameSize;
            }

            return sourceSize * CoverScale(frameSize, sourceSize);
        }

        /// <summary>
        /// 앵커를 전체 스트레치로 둔 RectTransform에 넣을 <c>sizeDelta</c> —
        /// 프레임보다 넘치는 양이다. 음수는 나오지 않는다(넘치기만 한다).
        /// </summary>
        public static Vector2 CoverSizeDelta(Vector2 frameSize, Vector2 sourceSize)
        {
            return CoverSize(frameSize, sourceSize) - frameSize;
        }
    }
}
