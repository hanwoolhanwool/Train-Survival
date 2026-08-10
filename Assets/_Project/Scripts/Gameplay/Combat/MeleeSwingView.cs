using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 근접 스윙 표현 (M5 8차 — 전투 연출) — Animator 없이 코드로 구동하는 호 궤적 트윈.
    /// 이 컴포넌트가 붙은 피벗을 스윙 요(yaw) 각으로 돌리고, 칼날 시각은 스윙 중에만 보인다.
    /// 판정과 무관한 표시 전용 — 소유자는 입력 즉시, 원격은 중계 RPC로 재생된다.
    /// </summary>
    public sealed class MeleeSwingView : MonoBehaviour
    {
        [Tooltip("칼날 시각 — 스윙 중에만 활성화된다.")]
        [SerializeField] private GameObject _blade;

        [SerializeField, Min(0.05f)] private float _swingSeconds = 0.25f;

        [Tooltip("스윙 시작 요(yaw) 각 — 오른쪽에서 왼쪽으로 벤다.")]
        [SerializeField] private float _startYawDeg = 70f;

        [SerializeField] private float _endYawDeg = -70f;

        [Tooltip("스윙 내내 유지하는 내리막 피치 — 수평 베기가 살짝 아래로 향한다.")]
        [SerializeField] private float _pitchDeg = 20f;

        private Quaternion _baseRotation;
        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            _baseRotation = transform.localRotation;
            if (_blade != null)
            {
                _blade.SetActive(false);
            }
        }

        /// <summary>스윙 재생 — 이미 재생 중이면 처음부터 다시 벤다.</summary>
        public void PlaySwing()
        {
            _elapsed = 0f;
            _playing = true;
            if (_blade != null)
            {
                _blade.SetActive(true);
            }
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _swingSeconds);
            float yaw = Mathf.Lerp(_startYawDeg, _endYawDeg, t);
            transform.localRotation = _baseRotation * Quaternion.Euler(_pitchDeg, yaw, 0f);

            if (t >= 1f)
            {
                _playing = false;
                transform.localRotation = _baseRotation;
                if (_blade != null)
                {
                    _blade.SetActive(false);
                }
            }
        }
    }
}
