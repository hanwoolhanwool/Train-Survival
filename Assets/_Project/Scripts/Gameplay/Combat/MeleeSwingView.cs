using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 근접 스윙 표현 (M5 8차 — 전투 연출) — Animator 없이 코드로 구동하는 호 궤적 트윈.
    /// 이 컴포넌트가 붙은 피벗을 스윙 요(yaw) 각으로 돌리고, 칼날 시각은 스윙 중에만 보인다.
    /// 판정과 무관한 표시 전용 — 소유자는 입력 즉시, 원격은 중계 RPC로 재생된다.
    ///
    /// <para><b>통합 1인칭</b>에서는 이 화면 전용 스윙을 재생하지 않는다 — 눈높이 피벗의 칼날과
    /// 손에 쥔 마체테가 이중으로 보이기 때문이다 (1인칭 통합 시점 전환 계획 §3.6).
    /// 원격 프록시의 모드는 항상 분리에 머물러 있어 <b>원격 화면의 스윙은 그대로 재생된다</b>.</para>
    ///
    /// <para><b>수용된 회귀</b>: 손 파지 구현이 합류하기 전까지 통합 모드에서는 근접 휘두르기
    /// 모션이 보이지 않는다. 대체 경로(소켓 절차 스윙)는 계획 §3.6 ⓑ — 단계 8.</para>
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

        private Player.PlayerViewModeController _viewMode;
        private Quaternion _baseRotation;
        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            _viewMode = GetComponentInParent<Player.PlayerViewModeController>();
            _baseRotation = transform.localRotation;
            if (_blade != null)
            {
                _blade.SetActive(false);
            }
        }

        /// <summary>스윙 재생 — 이미 재생 중이면 처음부터 다시 벤다.</summary>
        public void PlaySwing()
        {
            if (_viewMode != null && _viewMode.Mode == Player.PlayerViewMode.UnifiedFirstPerson)
            {
                return;
            }

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
