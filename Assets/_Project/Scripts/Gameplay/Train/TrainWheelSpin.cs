using Game.Core.Services;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 바퀴를 월드 스크롤 속도에 맞춰 제자리 회전시킨다 — 표현 전용이다.
    /// 열차는 원점 고정이고 세계가 뒤로 흐르므로(네트워크 문서 §4.1) 바퀴는 굴러 이동하지 않고
    /// 같은 자리에서 <b>구르는 각속도만</b> 재현한다: ω = 스크롤 속도 / 바퀴 반지름.
    /// 속도를 <see cref="IWorldScrollService"/>에서 읽으므로 연료 고갈·기상 감속에 저절로 따라가고,
    /// 열차가 서면 바퀴도 선다.
    /// </summary>
    /// <remarks>
    /// 네트워크 동기화하지 않는다. 각 피어가 같은 복제 속도로 같은 식을 돌리므로 결과가 저절로 맞고,
    /// 바퀴마다 NetworkTransform을 다는 것은 순전한 대역폭 낭비다.
    /// <para/>
    /// 성능 — 바퀴 하나마다 컴포넌트를 붙이면 Update 호출 수만큼 관리·네이티브 경계 비용이 붙는다.
    /// 그래서 열차 전체에 이 컴포넌트 <b>하나만</b> 두고 축 피벗 배열을 한 루프로 돌린다.
    /// 한 축의 두 바퀴는 같은 각도로 도므로 피벗도 축마다 하나면 된다(바퀴 수의 절반).
    /// 회전 쿼터니언은 루프 밖에서 한 번만 만들고, 열차가 멈춰 있으면 트랜스폼 쓰기를 통째로 건너뛴다.
    /// </remarks>
    public sealed class TrainWheelSpin : MonoBehaviour
    {
        [Tooltip("바퀴 반지름(m). 굴림 각속도 ω = 스크롤 속도 / 이 값 — 실제 바퀴 크기와 맞춰야 미끄러져 보이지 않는다.")]
        [SerializeField, Min(0.01f)] private float _wheelRadius = 0.959f;

        [Tooltip("회전시킬 축 피벗들. 비워 두면 Awake에서 자식 중 이름이 접두어로 시작하는 것을 모은다.")]
        [SerializeField] private Transform[] _axles;

        [Tooltip("_axles가 비었을 때 자동 수집에 쓰는 이름 접두어.")]
        [SerializeField] private string _autoCollectPrefix = "Axle_";

        [Tooltip("이 속도(m/s) 이하는 정지로 보고 트랜스폼 쓰기를 건너뛴다.")]
        [SerializeField, Min(0f)] private float _stopThreshold = 0.01f;

        private IWorldScrollService _scroll;
        private float _angleDegrees;

        /// <summary>이번 프레임에 굴린 누적 각도(도) — 검증용.</summary>
        public float AngleDegrees => _angleDegrees;

        /// <summary>회전 대상 축 개수 — 배선이 됐는지 확인하는 용도.</summary>
        public int AxleCount => _axles != null ? _axles.Length : 0;

        private void Awake()
        {
            if (_axles == null || _axles.Length == 0)
            {
                CollectAxles();
            }
        }

        private void CollectAxles()
        {
            Transform[] all = GetComponentsInChildren<Transform>(includeInactive: true);
            var found = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name.StartsWith(_autoCollectPrefix, System.StringComparison.Ordinal))
                {
                    found.Add(all[i]);
                }
            }

            _axles = found.ToArray();
        }

        private void Update()
        {
            if (_axles == null || _axles.Length == 0)
            {
                return;
            }

            // 서비스는 인게임 씬에서만 등록되므로 잡힐 때까지만 조회한다.
            if (_scroll == null && !ServiceLocator.TryGet(out _scroll))
            {
                return;
            }

            float speed = _scroll.ScrollSpeed;
            if (speed <= _stopThreshold)
            {
                // 정지 중에는 각도가 변하지 않는다 — 같은 값을 다시 쓰면 트랜스폼 계층만 더럽힌다.
                return;
            }

            // Mathf.Repeat로 0~360에 가둔다. 장시간 세션에서 각도가 커지면 float 정밀도가 떨어져
            // 회전이 계단처럼 끊긴다.
            _angleDegrees = Mathf.Repeat(
                _angleDegrees + speed / _wheelRadius * Mathf.Rad2Deg * Time.deltaTime, 360f);

            // 열차 전진(+Z) 기준 굴림 방향 — 축은 X, 바퀴 윗면이 앞으로 간다.
            Quaternion rotation = Quaternion.Euler(_angleDegrees, 0f, 0f);
            for (int i = 0; i < _axles.Length; i++)
            {
                Transform axle = _axles[i];
                if (axle != null)
                {
                    axle.localRotation = rotation;
                }
            }
        }
    }
}
