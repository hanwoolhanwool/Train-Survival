using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 발밑 지지면 보정 (북극 지역 구현 계획 §3.1 · §8.1 ⑤).
    ///
    /// <para><b>왜 필요한가.</b> <c>RegionDefinition.SurfaceY</c>는 <b>지역당 하나</b>다. 바다는 사방이
    /// 물이라 문제가 없었지만, 북극은 얼음(y 0)과 물(y −1.5)이 <b>같은 지역 안에서 교차</b>한다 —
    /// 단일값을 그대로 쓰면 얼음 구간에서 몬스터가 <b>1.5 m 묻힌 채</b> 걸어온다.</para>
    ///
    /// <para><b>레이캐스트 한 번으로 끝낸다.</b> 위에서 아래로 쏴 무엇이든 맞으면 그 높이를 쓰고,
    /// 허공이면 지역 물면으로 돌아간다. 물에는 콜라이더가 없으므로 <b>맞지 않는 것이 곧 물</b>이다 —
    /// 물 판정을 따로 만들 필요가 없다.</para>
    ///
    /// <para><b>매 프레임 쏘지 않는다.</b> 개체 수가 밤 웨이브에서 수십 기이고 지지면은 6 m/s로
    /// 흐르는 지형 위에서 천천히 바뀌므로, 호출부가 간격을 두고 갱신한다.</para>
    /// </summary>
    public static class GroundSupportProbe
    {
        /// <summary>지지면을 찾을 때 개체 위에서 시작하는 높이(m).</summary>
        public const float ProbeStartHeight = 3f;

        /// <summary>아래로 훑는 거리(m). 물면 아래로 조금 더 내려가야 "아무것도 없다"를 확정할 수 있다.</summary>
        public const float ProbeDistance = 5f;

        /// <summary>
        /// 지지면 높이 — 맞은 것이 <paramref name="fallbackSurfaceY"/>보다 높을 때만 채택한다.
        /// 순수 함수라 EditMode 가 고정한다.
        ///
        /// <para><b>낮은 것은 무시한다.</b> 해저(−6.5)나 얕은 물 바닥(−2.3)이 맞아도 몬스터는
        /// <b>물면 위를 걷는다</b>(바다 4차 규약) — 물속을 걸어오면 위협이 보이지 않는다.</para>
        /// </summary>
        public static float ResolveSupportY(bool hit, float hitY, float fallbackSurfaceY)
        {
            return hit && hitY > fallbackSurfaceY ? hitY : fallbackSurfaceY;
        }

        /// <summary>
        /// <paramref name="position"/> 발밑의 지지면 높이. 물리 조회 한 번 + 위 순수 판정.
        /// </summary>
        public static float Sample(Vector3 position, float fallbackSurfaceY)
        {
            Vector3 origin = new Vector3(position.x, position.y + ProbeStartHeight, position.z);
            bool hit = Physics.Raycast(
                origin, Vector3.down, out RaycastHit info, ProbeDistance, ~0, QueryTriggerInteraction.Ignore);

            return ResolveSupportY(hit, hit ? info.point.y : 0f, fallbackSurfaceY);
        }
    }
}
