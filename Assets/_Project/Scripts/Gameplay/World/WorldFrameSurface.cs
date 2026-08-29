using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지상(월드 소속) 표면 마커. 이 컴포넌트가 붙은 콜라이더 위에 선 플레이어는
    /// 스크롤 속도만큼 컨베이어 밀림을 로컬 적용받는다 (네트워크 문서 §4.2 상시 외력형).
    ///
    /// <para><b>표면 마찰도 여기 있다</b> (북극 계획 §5.5). 새 마커를 만들지 않은 이유는 두 가지다 —
    /// ① 밟고 있는 표면을 찾는 경로가 <b>이미</b> 이것 하나뿐이고(<c>ProbeGround</c>),
    /// ② 열차 갑판·지붕은 월드 소속이 아니라 이 경로를 타지 않아 <b>자동으로 제외</b>된다.
    /// 지붕 결빙은 그래서 "구조는 열어 두고 적용만 보류"가 된다(결정 ⑬): 나중에 갑판에
    /// 이 컴포넌트를 얹고 값을 넣으면 켜진다.</para>
    /// </summary>
    public sealed class WorldFrameSurface : MonoBehaviour
    {
        [Tooltip("접지 가속(m/s²). 0 = 마찰 무한 — 목표 속도가 즉시 걸리는 종전 동작이다(숲·사막·바다·대초원). " +
                 "북극 as-built: 눈 덮인 유빙 12(걷기 제동 0.84 m · 달리기 2.04 m) · 맨 얼음 3(3.38 m · 8.17 m).")]
        [SerializeField, Min(0f)] private float _groundAcceleration;

        /// <summary>이 표면의 접지 가속(m/s²). 0이면 마찰 무한(종전 동작).</summary>
        public float GroundAcceleration => _groundAcceleration;

        /// <summary>
        /// 콜라이더가 밟혔을 때 적용할 접지 가속 — 없으면 0(마찰 무한).
        /// <b>가장 가까운 조상</b>의 값을 쓰므로, 타일 루트에 기본값을 두고 특정 얼음 판에만
        /// 다른 값을 얹을 수 있다.
        /// </summary>
        public static float ResolveGroundAcceleration(Collider collider)
        {
            if (collider == null)
            {
                return 0f;
            }

            var surface = collider.GetComponentInParent<WorldFrameSurface>();
            return surface == null ? 0f : surface.GroundAcceleration;
        }
    }
}
