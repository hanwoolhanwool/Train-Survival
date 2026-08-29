using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터 도약 계산 (바다 지역 구현 계획 §8.2 · §12.2).
    ///
    /// <para><b>바다가 드러낸 것.</b> 기존 갑판 도약은 초기 속도를 <c>√(2g(갑판높이+1))</c>로 뽑았다 —
    /// <b>어디서 뛰는지를 보지 않는</b> 식이라 지면이 y 0일 때만 맞는다. 물면(−4)에서 뛰면
    /// 정점이 0.57에 그쳐 <b>갑판(3.566)에 4 m 모자란다.</b> 출발 높이를 넣으면 그 지역이
    /// 어디든 같은 곳에 닿는다.</para>
    /// </summary>
    public static class MonsterLeapMath
    {
        /// <summary>
        /// <paramref name="fromY"/>에서 뛰어 <paramref name="apexY"/>에 정확히 닿는 초기 속도.
        /// 이미 그 높이 위라면 0 — 뛰지 않는다.
        /// </summary>
        public static float LeapSpeed(float fromY, float apexY, float gravity)
        {
            float rise = apexY - fromY;
            if (rise <= 0f || gravity <= 0f)
            {
                return 0f;
            }

            return Mathf.Sqrt(2f * gravity * rise);
        }

        /// <summary>
        /// 물에서 <b>튀어오를</b> 것인가 (ㄴ 물고기 점프).
        ///
        /// <para>셋을 모두 만족해야 한다 — ① 내가 <b>수면에</b> 있고 ② 표적이 <b>물 밖에</b> 있고
        /// ③ 수평으로 <b>사거리 안</b>이다.</para>
        ///
        /// <para><b>②가 이 위협의 성격을 정한다.</b> 물속 표적에게는 도약하지 않고 그대로 추격한다 —
        /// 잠수 중인 플레이어에게는 도약 없는 근접 위협이고, 그것이 §6.1의 49초 창을 더 빠듯하게
        /// 쓰게 만든다. 물 밖으로 나간 표적에게만 튀어오르므로 <b>통로가 위험한 자리</b>가 된다.</para>
        /// </summary>
        /// <param name="surfaceY">물면 높이.</param>
        /// <param name="horizontalRange">튀어오를 수 있는 수평 거리.</param>
        /// <param name="emergeMargin">표적이 물면에서 이만큼 위여야 "물 밖"으로 본다.</param>
        public static bool ShouldSurfaceLeap(
            Vector3 self, Vector3 target, float surfaceY, float horizontalRange, float emergeMargin)
        {
            if (self.y > surfaceY + 0.01f)
            {
                return false;
            }

            if (target.y <= surfaceY + emergeMargin)
            {
                return false;
            }

            float dx = target.x - self.x;
            float dz = target.z - self.z;

            return dx * dx + dz * dz <= horizontalRange * horizontalRange;
        }

        /// <summary>
        /// 이 도약이 <paramref name="targetY"/>에 닿는가 — 규격 검증용.
        /// 결정 ⑨(*정점 +1.5*)가 <b>상판은 닿고 갑판은 닿지 않는다</b>를 지키는지 재는 자다.
        /// </summary>
        public static bool ReachesHeight(float fromY, float apexY, float targetY)
        {
            return apexY >= targetY && fromY <= targetY;
        }
    }
}
