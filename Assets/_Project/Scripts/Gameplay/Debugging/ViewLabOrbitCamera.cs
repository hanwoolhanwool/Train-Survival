using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// ViewLab 전용 궤도 카메라 — 우클릭 드래그 회전 / 휠 클릭 드래그 팬 / 휠 줌
    /// (docs/plans/뷰랩-씬-계획.md §4-④, Mecha Survivor RigLabOrbitCamera 이식).
    /// 전부 마우스 조작 — 키보드 핫키 금지 제약과 충돌하지 않는다.
    /// FP 모드에서는 컨트롤러가 이 컴포넌트를 꺼서 카메라 조작권을 회수한다.
    /// </summary>
    public sealed class ViewLabOrbitCamera : MonoBehaviour
    {
        [SerializeField] private float _distance = 7f;
        [SerializeField] private float _minDistance = 1.5f;
        [SerializeField] private float _maxDistance = 25f;

        [Tooltip("피벗 높이 — 대상 루트 기준 (캐릭터 몸통 높이)")]
        [SerializeField] private float _pivotHeight = 1.2f;

        [Tooltip("시작 부앙각(°) — 양수 = 내려다봄")]
        [SerializeField] private float _startPitch = 5f;

        [SerializeField] private float _rotateSensitivity = 0.25f;

        [Tooltip("휠 1노치당 거리 변화 비율 (0.12 = 12%)")]
        [SerializeField] private float _zoomSensitivity = 0.12f;

        [Tooltip("휠 클릭 드래그 팬 — 거리 비례라 줌 아웃할수록 크게 움직인다")]
        [SerializeField] private float _panSensitivity = 0.0015f;

        private Transform _target;
        private float _yaw = 180f;
        private float _pitch;
        private Vector3 _panOffset;

        public void SetTarget(Transform target)
        {
            _target = target;
            _panOffset = Vector3.zero;   // 대상이 바뀌면 팬을 풀어 새 대상을 프레이밍
        }

        private void Awake()
        {
            _pitch = _startPitch;
        }

        private void LateUpdate()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();

                if (mouse.rightButton.isPressed)
                {
                    _yaw += delta.x * _rotateSensitivity;
                    _pitch = Mathf.Clamp(_pitch - delta.y * _rotateSensitivity, -80f, 80f);
                    rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                }

                // 휠 클릭 드래그: 카메라 평면(우/상) 기준 시점 이동.
                if (mouse.middleButton.isPressed)
                {
                    _panOffset += rotation
                        * new Vector3(-delta.x, -delta.y, 0f)
                        * (_panSensitivity * _distance);
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                {
                    // 기기별 스크롤 스케일 차이(±1 vs ±120)에 폭주하지 않게 노치 단위로 클램프.
                    float notch = Mathf.Clamp(scroll, -1f, 1f);
                    _distance = Mathf.Clamp(
                        _distance - notch * _zoomSensitivity * _distance,
                        _minDistance, _maxDistance);
                }
            }

            Vector3 pivot = (_target != null ? _target.position : Vector3.zero)
                + Vector3.up * _pivotHeight + _panOffset;
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * _distance, rotation);
        }
    }
}
