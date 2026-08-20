using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 로비에 들어올 때 검은 화면에서 1.2초에 걸쳐 밝아진다 —
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §6.6.
    ///
    /// <para>Boot에서 Main으로 넘어오는 순간 화면이 툭 바뀌면 로딩 사고처럼 보인다.
    /// 짧은 페이드 하나로 "장면이 시작됐다"는 신호가 된다.</para>
    ///
    /// <para><b>에디터에서는 검은 판이 보이지 않는다.</b> 플레이 중에만 알파를 1로 올렸다가 내리므로,
    /// 씬 편집 중에 화면이 가려지지 않는다.</para>
    ///
    /// <para>페이드가 끝나면 <b>스스로 꺼진다</b> — 전체 화면 그래픽이 남아 있으면 오버드로가 계속 든다.</para>
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class MenuScreenFade : MonoBehaviour
    {
        /// <summary>계획 §6.6이 정한 길이.</summary>
        public const float DefaultSeconds = 1.2f;

        [SerializeField]
        [Tooltip("검은 화면에서 밝아지는 데 걸리는 시간 (초).")]
        private float _seconds = DefaultSeconds;

        private Graphic _graphic;
        private float _elapsed;
        private bool _running;

        private void OnEnable()
        {
            _graphic = GetComponent<Graphic>();
            _graphic.raycastTarget = false;      // 페이드 판이 클릭을 먹으면 안 된다

            if (!Application.isPlaying)
            {
                SetAlpha(0f);
                _running = false;
                return;
            }

            _elapsed = 0f;
            _running = true;
            SetAlpha(1f);
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            float t = _seconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / _seconds);

            // 끝에서 부드럽게 멎도록 smoothstep — 선형이면 마지막에 툭 끊긴다.
            float eased = t * t * (3f - 2f * t);
            SetAlpha(1f - eased);

            if (t >= 1f)
            {
                _running = false;
                gameObject.SetActive(false);
            }
        }

        private void SetAlpha(float a)
        {
            if (_graphic == null)
            {
                return;
            }

            Color c = _graphic.color;
            c.a = a;
            _graphic.color = c;
        }
    }
}
