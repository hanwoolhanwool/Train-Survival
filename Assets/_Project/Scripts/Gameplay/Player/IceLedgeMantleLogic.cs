using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 물에서 얼음 턱으로 기어오르는 판정 (북극 지역 구현 계획 §5.4 · §8.1 ④) — 전부 순수 함수.
    ///
    /// <para><b>왜 필요한가.</b> 점프 높이가 1.2 m 인데 얼음 두께가 1.5 m 다. 얕은 물 바닥(−2.3)에서
    /// 얼음 상면(0)까지는 <b>2.3 m</b>라 더 멀다 — 빠지면 <b>반드시</b> 기어오르는 동작을 거친다.
    /// *"탈출 가능하되 몇 초 걸린다"* 가 결정 ③의 정의이고, 그 "몇 초"를 만드는 것이 이 판정이다.</para>
    ///
    /// <para><b>왜 사다리를 재사용하지 않는가.</b> 사다리는 볼륨을 두고 붙는 물건이라 저작이 필요하다.
    /// 얼음 턱은 <b>지형 어디에나</b> 있으므로 저작할 수 없다 — 대신 올려놓는 동작
    /// (<see cref="LadderClimbLogic.ComputeMantleMotion"/>)은 그대로 쓴다.</para>
    /// </summary>
    public static class IceLedgeMantleLogic
    {
        /// <summary>턱으로 인정하는 최소 상승(m). 이보다 낮으면 그냥 걸어 올라갈 수 있다.</summary>
        public const float DefaultMinRise = 0.3f;

        /// <summary>
        /// 턱으로 인정하는 최대 상승(m). 얕은 물 바닥(−2.3)에서 얼음 상면(0)까지 2.3 m 를 덮되,
        /// 열차 갑판(3.566 m)에는 닿지 않는 값이다 — 물에서 갑판으로 곧장 기어오르면
        /// 사다리가 있을 이유가 없어진다.
        /// </summary>
        public const float DefaultMaxRise = 2.6f;

        /// <summary>턱 위에 몸이 들어갈 여유 높이(m).</summary>
        public const float DefaultHeadroom = 1.9f;

        /// <summary>
        /// 벽이 <b>수직에 가까운가</b> — 비스듬한 사면은 걸어 올라가는 것이지 기어오르는 것이 아니다.
        /// <paramref name="normal"/>은 벽 법선이고, 수평 성분이 클수록 수직 벽이다.
        /// </summary>
        public static bool IsClimbableWall(Vector3 normal, float maxSlopeDot = 0.5f)
        {
            return Mathf.Abs(normal.y) <= maxSlopeDot;
        }

        /// <summary>
        /// 발 높이 <paramref name="feetY"/>에서 턱 <paramref name="ledgeY"/>로 오를 수 있는가.
        /// </summary>
        public static bool CanMantle(
            float feetY, float ledgeY, float minRise = DefaultMinRise, float maxRise = DefaultMaxRise)
        {
            float rise = ledgeY - feetY;
            return rise >= minRise && rise <= maxRise;
        }

        /// <summary>
        /// 점프로 넘을 수 있는 턱인가 — 넘을 수 있으면 기어오르기가 <b>끼어들면 안 된다</b>.
        /// 그렇지 않으면 얕은 턱마다 몸이 잠깐 굳는다.
        /// <para>점프 도달 높이는 <c>v² / 2g</c>이고 <see cref="PlayerMotor.GetJumpSpeed"/>의 역이라
        /// 결국 <paramref name="jumpHeight"/> 그대로다 — 여유(<paramref name="margin"/>)만 뺀다.</para>
        /// </summary>
        public static bool ClearsWithJump(float feetY, float ledgeY, float jumpHeight, float margin = 0.15f)
        {
            return ledgeY - feetY <= jumpHeight - margin;
        }

        /// <summary>
        /// 이 프레임에 기어오르기를 시작해야 하는가 — 위 셋을 한 판정으로 묶은 것.
        /// </summary>
        public static bool ShouldMantle(
            bool submerged, bool jumpPressed, bool hitWall, Vector3 wallNormal,
            float feetY, float ledgeY, float jumpHeight)
        {
            return submerged
                && jumpPressed
                && hitWall
                && IsClimbableWall(wallNormal)
                && CanMantle(feetY, ledgeY)
                && !ClearsWithJump(feetY, ledgeY, jumpHeight);
        }
    }
}
