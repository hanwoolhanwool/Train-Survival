using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 바다 교각 사다리 — 물에서 상판으로 올라오는 유일한 경로 (바다 지역 구현 계획 §6.3 ③).
    ///
    /// <para><b>열차 사다리(<c>BoardingLadder</c>)와 다른 점.</b>
    /// ① <b>월드 소속</b>이라 뒤로 흐른다 — 매달린 사람이 <b>실제 이동량을 따라간다</b>.
    /// ② 물에서 접근하므로 볼륨이 <b>잠수 깊이까지</b> 내려간다.
    /// ③ 통로가 1.15 m뿐이라 <b>올라선 자리를 볼륨에서 떼어</b> 두었다 — 겹치면 재부착된다.</para>
    ///
    /// <para>볼륨은 <see cref="BoxCollider"/> 트리거다. 이 컴포넌트는 <b>알려 주기만</b> 하고
    /// 붙을지·오를지는 플레이어 컨트롤러가 <see cref="SeaLadderMotion"/>으로 판정한다.</para>
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SeaLadder : MonoBehaviour
    {
        [Tooltip("오르내리는 속도 (m/s).")]
        [SerializeField, Min(0.1f)] private float _climbSpeed = 2.6f;

        [Tooltip("사다리 중심선에서 몸 중심까지 유지할 거리 (m) — 캡슐 반경 0.35 + 여유.")]
        [SerializeField, Min(0.1f)] private float _holdDistance = 0.45f;

        [Tooltip("발이 이 높이에 닿으면 올라선다 (m). 상판 상면보다 살짝 위.")]
        [SerializeField] private float _topY = 0.1f;

        [Tooltip("이 높이 아래로 내려가면 놓아 준다 (m). 물속에서 계속 붙잡으면 잠수가 막힌다.")]
        [SerializeField] private float _bottomY = -7.2f;

        [Tooltip("올라선 뒤 안쪽으로 밀어 넣을 거리 (m). 부족하면 캡슐이 상판 밖으로 나가 미끄러진다. " +
            "바다 통로는 1.15 m뿐이라 열차 기본값(0.7)이 맞지 않는다.")]
        [SerializeField, Min(0.1f)] private float _exitInward = 1.25f;

        private BoxCollider _volume;

        /// <summary>사다리 축의 밑동 위치 (월드).</summary>
        public Vector3 Origin => transform.position;

        /// <summary>
        /// 오르는 사람이 서는 쪽 — 사다리 앞면 법선(수평).
        /// <b>축 약속</b>: 이 오브젝트의 forward(+Z)가 물 쪽이다.
        /// </summary>
        public Vector3 Outward
        {
            get
            {
                Vector3 f = transform.forward;
                f.y = 0f;
                return f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
            }
        }

        public float ClimbSpeed => _climbSpeed;

        public float HoldDistance => _holdDistance;

        /// <summary>올라서는 높이 (월드). 사다리가 오르내려도 따라가도록 오브젝트 높이에 더한다.</summary>
        public float TopY => transform.position.y + _topY;

        /// <summary>놓아 주는 높이 (월드).</summary>
        public float BottomY => transform.position.y + _bottomY;

        public float ExitInward => _exitInward;

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Awake()
        {
            _volume = GetComponent<BoxCollider>();
            _volume.isTrigger = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 o = Origin;
            Gizmos.DrawLine(new Vector3(o.x, BottomY, o.z), new Vector3(o.x, TopY, o.z));

            // 올라설 자리 — 상판 위에 온전히 들어가는지 눈으로 본다.
            Gizmos.color = Color.yellow;
            Vector3 exit = SeaLadderMotion.ExitPosition(o, Outward, _holdDistance, _exitInward, TopY);
            Gizmos.DrawWireSphere(exit, 0.35f);
        }
    }
}
