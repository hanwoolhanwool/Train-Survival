using UnityEngine;

namespace Game.UI.Ready
{
    /// <summary>
    /// 패널이 화면 밖에서 제자리로 들어오는 연출.
    ///
    /// <para><b>로스터는 위에서 내려오고 조작 패널은 오른쪽에서 당겨져 온다</b>
    /// (2026-08-22 사용자 지시). 간판을 내려 걸고, 옆의 것을 끌어오는 그림이다 —
    /// 두 패널이 동시에 나타나면 "창이 떴다"가 되고, 시차를 두면 <b>손이 하나씩 놓은 것</b>이 된다.</para>
    ///
    /// <para><b>자리 계산은 건드리지 않는다.</b> 패널의 최종 위치는 실측표가 정하므로
    /// (<see cref="ReadyPanelLayout"/>) 이 부품은 <see cref="IReadyPanel.IntroOffset"/>에
    /// <b>더할 값만</b> 넣는다. 연출이 배치 수식과 섞이면 화면비가 바뀔 때 서로를 덮어쓴다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReadyPanelSlide : MonoBehaviour
    {
        /// <summary>어디서 들어오는가.</summary>
        public enum Origin
        {
            /// <summary>위에서 내려온다 — 간판을 내려 거는 그림.</summary>
            Top = 0,

            /// <summary>오른쪽에서 당겨져 온다.</summary>
            Right = 1,
        }

        [SerializeField]
        [Tooltip("어디서 들어오는가.")]
        private Origin _from = Origin.Top;

        [SerializeField]
        [Tooltip("들어오는 데 걸리는 시간(초).")]
        private float _seconds = 0.42f;

        [SerializeField]
        [Tooltip("시작을 미루는 시간(초). 두 패널에 시차를 주면 손이 하나씩 놓는 것처럼 보인다.")]
        private float _delay;

        [SerializeField]
        [Tooltip("반동 세기. 0이면 반동 없이 감속만 한다.")]
        private float _back = ReadyPanelSlideMath.DefaultBack;

        [SerializeField]
        [Tooltip("출발 거리 — 패널 자기 크기의 몇 배 밖에서 오는가.")]
        private float _travel = 1.2f;

        private IReadyPanel _panel;
        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            _panel = GetComponent<IReadyPanel>();
        }

        private void OnDisable()
        {
            // 꺼진 채로 오프셋이 남으면 다음에 켤 때 화면 밖에서 시작한다.
            _playing = false;
            Rest();
        }

        /// <summary>연출을 처음부터 다시 시작한다.</summary>
        public void Play()
        {
            if (_panel == null)
            {
                _panel = GetComponent<IReadyPanel>();
            }

            _elapsed = 0f;
            _playing = true;
            Apply();
        }

        /// <summary>연출을 건너뛰고 제자리에 둔다.</summary>
        public void Rest()
        {
            if (_panel != null)
            {
                _panel.IntroOffset = Vector2.zero;
            }
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            if (_panel == null)
            {
                _playing = false;
                return;
            }

            float t = _seconds <= 0f ? 1f : (_elapsed - _delay) / _seconds;
            if (t >= 1f)
            {
                _playing = false;
                _panel.IntroOffset = Vector2.zero;
                return;
            }

            // 아직 차례가 오지 않았으면 출발 지점에 붙어 기다린다.
            float remaining = t <= 0f ? 1f : ReadyPanelSlideMath.Remaining(t, _back);
            _panel.IntroOffset = StartOffset() * remaining;
        }

        private Vector2 StartOffset()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            return _from == Origin.Right
                ? new Vector2(size.x * _travel, 0f)
                : new Vector2(0f, size.y * _travel);
        }
    }
}
