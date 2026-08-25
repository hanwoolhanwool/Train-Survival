using System.Collections.Generic;

namespace Game.UI.Loading
{
    /// <summary>
    /// 워밍업이 미리 구울 <b>문자와 크기</b> —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.4.
    ///
    /// <para><b>왜 필요한가</b>: 인게임 HUD는 전부 IMGUI이고 대부분 <c>GUIStyle</c> 없이
    /// 기본 스킨을 쓴다 — 즉 <b>내장 폰트의 동적 래스터화</b> 대상이다. 창고를 처음 열면
    /// 아이템 표시명의 글리프가 그 프레임에 한꺼번에 요청되고, 아틀라스가 재구축되며
    /// 텍스처가 다시 업로드된다. 두 번째부터는 안 튄다 — 그래서 미리 굽는다.</para>
    ///
    /// <para><b>에셋을 모른다.</b> 카탈로그를 여기 들이지 않고 <b>문자열 목록</b>만 받는다.
    /// 어디서 모을지는 <see cref="UiWarmupStep"/>이 알고, 여기서는 "받은 것에서 문자를 추린다"만
    /// 한다 — 그래야 EditMode가 씬도 에셋도 없이 전부 덮는다.</para>
    /// </summary>
    internal static class UiWarmupText
    {
        /// <summary>
        /// 문자열 묶음에서 <b>구울 값이 있는</b> 문자만 추린다 — 중복과 공백·제어문자를 뺀다.
        ///
        /// <para>공백을 빼는 이유는 글리프가 없기 때문이고, 중복을 빼는 이유는 요청 문자열이
        /// 길수록 아틀라스 계산이 비싸지기 때문이다. 순서는 보장하지 않는다.</para>
        /// </summary>
        public static char[] Collect(IEnumerable<string> sources)
        {
            var seen = new HashSet<char>();
            if (sources == null)
            {
                return System.Array.Empty<char>();
            }

            foreach (string source in sources)
            {
                if (string.IsNullOrEmpty(source))
                {
                    continue;
                }

                for (int i = 0; i < source.Length; i++)
                {
                    char c = source[i];
                    if (char.IsWhiteSpace(c) || char.IsControl(c))
                    {
                        continue;
                    }

                    seen.Add(c);
                }
            }

            var result = new char[seen.Count];
            seen.CopyTo(result);
            return result;
        }

        /// <summary>
        /// 이 화면에서 구워야 할 글자 크기들 (px).
        ///
        /// <para><b>0이 들어 있는 것은 실수가 아니다</b> — <c>Font.RequestCharactersInTexture</c>에서
        /// 0은 "폰트의 기본 크기"를 뜻하고, HUD 대부분이 <c>GUIStyle</c> 없이 기본 스킨으로
        /// 그리므로 <b>실제로 가장 많이 쓰이는 크기</b>다.</para>
        ///
        /// <para>나머지는 <see cref="UiMetrics.HudSizes1440"/>를 이 화면 높이로 환산한 값이다.
        /// 크기를 하나라도 빠뜨리면 그 크기에서만 렉이 남는다(§11-4).</para>
        /// </summary>
        public static int[] FontSizes(float screenHeight)
        {
            var sizes = new List<int> { 0 };

            for (int i = 0; i < UiMetrics.HudSizes1440.Length; i++)
            {
                int size = UiMetrics.FontFor(UiMetrics.HudSizes1440[i], screenHeight);
                if (size > 0 && !sizes.Contains(size))
                {
                    sizes.Add(size);
                }
            }

            return sizes.ToArray();
        }
    }
}
