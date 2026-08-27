using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 바다 교각 사다리 계산 순수 로직 — EditMode 테스트 대상 (바다 지역 구현 계획 §6.3 ③).
    ///
    /// <para><b>왜 열차 사다리를 안 쓰나.</b> <c>BoardingLadder</c>는 <b>정지 프레임 + 넓은 갑판</b>을
    /// 전제로 만들어졌고 바다는 둘 다 깬다. 재사용을 시도해 <b>일곱 번</b> 고쳤고 원인이 매번 달랐다 —
    /// 붙기·오르기·올라서기 <b>세 경로에 각각</b> 컨베이어를 실어야 했고, 좁은 통로 탓에 올라선 자리가
    /// 볼륨과 겹쳐 재부착됐다.</para>
    ///
    /// <para><b>그래서 구조를 바꾼다.</b> 스크롤 속도를 읽어 컨베이어를 <b>계산하지</b> 않는다.
    /// 대신 <b>사다리가 실제로 움직인 양</b>을 그대로 따라간다 — 속도를 몰라도 정확히 붙어 있고,
    /// 경로가 몇 개든 한 군데서 끝난다. 이탈 칸 위에서 사람이 칸을 따라가는 것과 같은 수법이다.</para>
    /// </summary>
    public static class SeaLadderMotion
    {
        /// <summary>
        /// 사다리 앞면에 매달릴 <b>수평</b> 목표 위치. 높이는 손대지 않는다 —
        /// 오르내리는 것은 <see cref="ClimbVelocity"/>가 따로 한다.
        /// </summary>
        /// <param name="outward">사다리 앞면 법선(물 쪽). 수평 성분만 쓴다.</param>
        public static Vector3 HoldTarget(Vector3 ladderOrigin, Vector3 outward, float holdDistance)
        {
            Vector3 flat = new Vector3(outward.x, 0f, outward.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return new Vector3(ladderOrigin.x, 0f, ladderOrigin.z);
            }

            Vector3 offset = flat.normalized * holdDistance;
            return new Vector3(ladderOrigin.x + offset.x, 0f, ladderOrigin.z + offset.z);
        }

        /// <summary>매달린 자리로 가는 <b>수평 보정</b> 벡터.</summary>
        public static Vector3 HoldCorrection(
            Vector3 currentPosition, Vector3 ladderOrigin, Vector3 outward, float holdDistance)
        {
            Vector3 target = HoldTarget(ladderOrigin, outward, holdDistance);
            return new Vector3(target.x - currentPosition.x, 0f, target.z - currentPosition.z);
        }

        /// <summary>오르내리는 수직 속도 (m/s). 입력이 없으면 <b>그 자리에 매달려 있는다</b>.</summary>
        public static float ClimbVelocity(float verticalInput, float climbSpeed)
        {
            return Mathf.Clamp(verticalInput, -1f, 1f) * Mathf.Max(0f, climbSpeed);
        }

        /// <summary>발이 꼭대기에 닿았는가 — 여기 닿으면 올라선다.</summary>
        public static bool HasReachedTop(float footY, float topY)
        {
            return footY >= topY;
        }

        /// <summary>
        /// 아래로 빠져나갔는가 — 사다리 밑을 지나면 놓아 준다.
        /// 물속에서 계속 붙잡고 있으면 잠수가 막힌다.
        /// </summary>
        public static bool HasFallenBelow(float footY, float bottomY)
        {
            return footY < bottomY;
        }

        /// <summary>
        /// 올라선 뒤 설 자리 — 사다리에서 <b>안쪽</b>(물 반대쪽)으로 밀어 넣는다.
        /// <para>이 거리가 부족하면 캡슐 절반이 상판 밖으로 나가 <b>미끄러져 떨어진다</b>.
        /// 바다 통로는 1.15 m뿐이라 열차 기본값(0.7)이 맞지 않는다.</para>
        /// </summary>
        public static Vector3 ExitPosition(
            Vector3 ladderOrigin, Vector3 outward, float holdDistance, float exitInward, float topY)
        {
            Vector3 hold = HoldTarget(ladderOrigin, outward, holdDistance);
            Vector3 flat = new Vector3(outward.x, 0f, outward.z);
            Vector3 inward = flat.sqrMagnitude < 0.0001f ? Vector3.zero : -flat.normalized * exitInward;
            return new Vector3(hold.x + inward.x, topY, hold.z + inward.z);
        }

        /// <summary>
        /// 올라선 자리가 상판 위에 <b>온전히</b> 있는가 — 규격 검증용.
        /// 캡슐이 상판 끝을 넘거나 열차 오버행과 겹치면 안 된다.
        /// </summary>
        public static bool IsExitOnDeck(
            float exitAbsX, float capsuleRadius, float deckHalfWidth, float trainOverhang)
        {
            return exitAbsX + capsuleRadius <= deckHalfWidth
                && exitAbsX - capsuleRadius >= trainOverhang;
        }
    }
}
