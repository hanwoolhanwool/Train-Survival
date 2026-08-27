using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 승하차 사다리 (사다리 승하차 계획 §3.4) — 오를 수 있는 구간과 방향을 <b>알려 주기만</b> 한다.
    /// 붙을지 떨어질지는 <c>LadderClimbLogic</c>이 판정하고 플레이어 컨트롤러가 구동한다.
    ///
    /// <para><b>축 약속</b>: 이 오브젝트의 <b>forward(+Z)가 오르는 사람이 서는 쪽</b>이다.
    /// 모델은 자식으로 넣고 제 축에 맞게 돌린다 — 그래야 모델 축이 어떻든 배치가 이 규칙 하나로 끝난다.</para>
    ///
    /// <para>볼륨은 <see cref="BoxCollider"/> 트리거다. <c>CharacterController</c>가 트리거 이벤트를
    /// 받으므로 컨트롤러가 매 프레임 <c>OverlapBox</c>를 돌 필요가 없다.</para>
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BoardingLadder : MonoBehaviour
    {
        [Tooltip("갑판 상면 y 를 여기서 읽는다 — QA 높이 토글(F2)로 열차가 오르내려도 사다리 꼭대기가 따라간다. "
            + "비우면 볼륨 상단을 꼭대기로 쓴다.")]
        [SerializeField] private TrainLayoutSettings _layoutSettings;

        [Tooltip("사다리 중심선에서 몸 중심까지 유지할 거리(m) — 캡슐 반경 0.35 + 여유.")]
        [SerializeField, Min(0.1f)] private float _holdDistance = 0.45f;

        [Tooltip("오르내리는 속도(m/s).")]
        [SerializeField, Min(0.1f)] private float _climbSpeed = 2.4f;

        [Tooltip("꼭대기에서 갑판 안쪽으로 밀어 넣을 거리(m) — 캡슐 반경의 2배가 기준이다.")]
        [SerializeField, Min(0.1f)] private float _mantleInwardDistance = 0.7f;

        [Tooltip("이 사다리가 <b>월드 소속</b>인가 (지형 타일에 붙어 뒤로 흐른다). " +
            "열차 사다리는 정지 프레임이라 꺼 두고, 바다 교각 사다리처럼 흐르는 것에만 켠다. " +
            "켜면 매달린 사람이 사다리와 <b>같은 속도로 밀려</b> 상대 위치를 유지한다.")]
        [SerializeField] private bool _worldFrame;

        private BoxCollider _volume;

        private BoxCollider Volume
        {
            get
            {
                if (_volume == null)
                {
                    _volume = GetComponent<BoxCollider>();
                }

                return _volume;
            }
        }

        /// <summary>오르는 사람이 서는 쪽 — 사다리 앞면 법선(수평).</summary>
        public Vector3 Normal
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            }
        }

        /// <summary>오르는 사람이 바라보는 방향 — 사다리를 향하는 수평 방향.</summary>
        public Vector3 ApproachDirection => -Normal;

        /// <summary>사다리 중심선의 수평 기준점.</summary>
        public Vector3 Origin => transform.position;

        /// <summary>매달릴 수 있는 아래 끝 — 볼륨 바닥이다. 지상에 선 사람이 닿을 수 있어야 한다.</summary>
        public float BottomY => Volume.bounds.min.y;

        /// <summary>
        /// 매달릴 수 있는 위 끝 = 갑판 상면. 여기 닿으면 올라서기로 넘어간다.
        /// <b>씬 상수가 아니라 설정에서 읽는다</b> — QA 높이 토글이 갑판을 올리면 사다리도 따라가야 한다.
        /// </summary>
        public float TopY => _layoutSettings != null ? _layoutSettings.DeckHeight : Volume.bounds.max.y;

        public float HoldDistance => _holdDistance;

        public float ClimbSpeed => _climbSpeed;

        public float MantleInwardDistance => _mantleInwardDistance;

        /// <summary>
        /// 월드 소속 사다리인가 — 지형에 붙어 뒤로 흐르는가.
        /// 이걸 안 보면 <b>매달린 사람만 제자리에 남아 사다리가 빠져나간다.</b>
        /// </summary>
        public bool IsWorldFrame => _worldFrame;

        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        private void OnDrawGizmosSelected()
        {
            // 축 약속(forward = 서는 쪽)을 눈으로 확인할 수단 — 방향을 틀리면 아예 안 붙는다.
            Gizmos.color = Color.cyan;
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            Gizmos.DrawLine(origin, origin + Normal * 1.5f);
            Gizmos.DrawSphere(origin + Normal * 1.5f, 0.08f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(origin.x, BottomY, origin.z), new Vector3(origin.x, TopY, origin.z));
        }
    }
}
