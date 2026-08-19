using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 임계 시에만 등장하는 줄(비주얼·UI/UX 가이드 §9.2 <b>B계층</b>)의 등장·퇴장 계산.
    ///
    /// <para>가이드가 요구하는 것은 두 가지다 — <b>주의 단계에서 페이드 인</b>, <b>안전 복귀 후
    /// 유예를 두고 페이드 아웃</b>. 유예가 없으면 임계값 근처에서 값이 흔들릴 때 줄이 깜빡인다.</para>
    ///
    /// <para>축마다 하나씩 들고(허기·체온) 매 프레임 <see cref="Evaluate"/>를 부른다.
    /// 시간을 주입받으므로 EditMode에서 경계를 그대로 검증할 수 있다.</para>
    /// </summary>
    internal sealed class HudTransientFade
    {
        /// <summary>안전 복귀 후 그대로 머무는 시간 — 가이드 §9.2 "안전 복귀 시 2초 후 페이드 아웃".</summary>
        public const float GraceSeconds = 2f;

        /// <summary>사라지는 데 걸리는 시간. 즉시 없애면 무엇이 사라졌는지 못 본다.</summary>
        public const float FadeSeconds = 0.6f;

        /// <summary>등장에 걸리는 시간 — 퇴장보다 빠르다. 위험은 먼저 눈에 들어와야 한다.</summary>
        public const float RiseSeconds = 0.2f;

        private float _alpha;
        private float _easedAt = float.NegativeInfinity;
        private bool _stressed;

        /// <summary>마지막으로 계산된 불투명도 (0~1).</summary>
        public float Alpha => _alpha;

        /// <summary>불투명도가 0보다 크면 그릴 값이 있다는 뜻이다.</summary>
        public bool IsVisible => _alpha > 0f;

        /// <param name="stressed">지금 이 축이 비정상 범위인가.</param>
        /// <param name="now">현재 시각 (보통 <c>Time.unscaledTime</c>).</param>
        /// <param name="deltaSeconds">직전 호출로부터 흐른 시간.</param>
        /// <returns>이번 프레임의 불투명도 (0~1).</returns>
        public float Evaluate(bool stressed, float now, float deltaSeconds)
        {
            if (stressed != _stressed)
            {
                _stressed = stressed;

                // 안전으로 <b>돌아온</b> 순간만 기록한다 — 유예는 거기서부터 센다.
                if (!stressed)
                {
                    _easedAt = now;
                }
            }

            float target = stressed || now - _easedAt < GraceSeconds ? 1f : 0f;
            float duration = target > _alpha ? RiseSeconds : FadeSeconds;

            _alpha = duration <= 0f
                ? target
                : Mathf.MoveTowards(_alpha, target, deltaSeconds / duration);

            return _alpha;
        }

        /// <summary>즉시 감춘다 — 비활성화·재시작처럼 이전 상태가 의미를 잃는 순간에 쓴다.</summary>
        public void Reset()
        {
            _alpha = 0f;
            _easedAt = float.NegativeInfinity;
            _stressed = false;
        }
    }
}
