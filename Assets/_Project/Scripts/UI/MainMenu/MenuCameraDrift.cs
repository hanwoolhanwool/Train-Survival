using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 카메라를 아주 조금 흔든다 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §6.4.
    ///
    /// <para><b>이 화면의 성패를 가르는 연출이다.</b> 배경이 2D 한 장이라 카메라가 완전히 멈추면
    /// 전체가 그림 한 장으로 읽힌다. 카메라가 조금만 움직이면 <b>3D 기차만 배경에 대해 미끄러지고</b>,
    /// 그 시차가 공간감을 만든다 — 배경은 스크린 스페이스라 따라 움직이지 않기 때문이다.</para>
    ///
    /// <para><b>진폭이 작아야 한다.</b> 위치 ±0.03 m, 회전 ±0.4°. 이보다 크면 기차가 배경에서
    /// 떨어져 나온 것처럼 보이고, 배너·명판이 함께 흔들리지 않는다는 사실이 드러난다.</para>
    ///
    /// <para>흔들림 계산은 <see cref="MenuNoise"/>에 있다. 이 컴포넌트는 <b>기준 자세를 기억했다가
    /// 되돌려 놓는 일</b>만 한다 — 그러지 않으면 플레이를 반복할 때마다 카메라가 조금씩 밀린다.</para>
    /// </summary>
    public sealed class MenuCameraDrift : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("위치 흔들림 진폭 (m). 계획 §6.4의 ±0.03.")]
        private Vector3 _positionAmplitude = new Vector3(0.03f, 0.03f, 0.03f);

        [SerializeField]
        [Tooltip("회전 흔들림 진폭 (도). 계획 §6.4의 ±0.4.")]
        private Vector3 _rotationAmplitude = new Vector3(0.4f, 0.4f, 0.15f);

        [SerializeField]
        [Tooltip("축별 위치 주기 (초). 서로 다른 값이어야 한 방향 이동으로 보이지 않는다.")]
        private Vector3 _positionPeriods = new Vector3(14f, 17f, 12f);

        [SerializeField]
        [Tooltip("축별 회전 주기 (초).")]
        private Vector3 _rotationPeriods = new Vector3(19f, 16f, 21f);

        [SerializeField]
        private float _seed = 5f;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private bool _captured;

        private void OnEnable()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _captured = true;
        }

        private void OnDisable()
        {
            Restore();
        }

        private void LateUpdate()
        {
            if (!_captured)
            {
                return;
            }

            float t = Time.unscaledTime;
            Vector3 offset = MenuNoise.Drift(t, _positionAmplitude, _positionPeriods, _seed);
            Vector3 tilt = MenuNoise.Drift(t, _rotationAmplitude, _rotationPeriods, _seed + 101f);

            transform.localPosition = _basePosition + offset;
            transform.localRotation = _baseRotation * Quaternion.Euler(tilt);
        }

        /// <summary>기준 자세로 되돌린다 — 씬에 저장된 카메라 값이 드리프트로 오염되지 않게 한다.</summary>
        public void Restore()
        {
            if (!_captured)
            {
                return;
            }

            transform.localPosition = _basePosition;
            transform.localRotation = _baseRotation;
        }
    }
}
