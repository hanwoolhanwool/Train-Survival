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

        /// <summary>
        /// 보정을 <b>진동하지 않게</b> 다듬는다.
        ///
        /// <para><b>왜 필요한가.</b> 매 프레임 오차 전부를 한 번에 없애면 조금만 넘겨도(충돌 해결·
        /// skin width·부동소수 오차) 반대편으로 넘어가고, 다음 프레임에 되돌아오며 <b>떨린다</b>.
        /// 그래서 ① 아주 작은 오차는 <b>그냥 둔다</b>(데드존) ② 나머지는 <b>일부만</b> 좁힌다.</para>
        ///
        /// <para>일부만 좁혀도 매 프레임 반복되므로 몇 프레임이면 붙는다 — 오버슈트 없이.</para>
        /// </summary>
        /// <param name="deadZone">이 거리 안이면 보정하지 않는다 (m).</param>
        /// <param name="damping">남은 오차를 한 프레임에 좁히는 비율 (0~1).</param>
        public static Vector3 SmoothCorrection(Vector3 rawCorrection, float deadZone, float damping)
        {
            float distance = rawCorrection.magnitude;
            if (distance <= Mathf.Max(0f, deadZone))
            {
                return Vector3.zero;
            }

            return rawCorrection * Mathf.Clamp01(damping);
        }

        /// <summary>
        /// 사다리가 <b>다른 것으로 바뀌었는가</b> — 이동량 추종을 초기화해야 하는 순간이다.
        /// 흘러오는 다음 사다리로 참조가 옮겨간 프레임에 이전 위치와 비교하면 <b>큰 점프</b>가 나온다.
        /// </summary>
        public static bool IsFollowJump(Vector3 delta, float maxStep)
        {
            return delta.sqrMagnitude > maxStep * maxStep;
        }

        /// <summary>오르내리는 수직 속도 (m/s). 입력이 없으면 <b>그 자리에 매달려 있는다</b>.</summary>
        public static float ClimbVelocity(float verticalInput, float climbSpeed)
        {
            return Mathf.Clamp(verticalInput, -1f, 1f) * Mathf.Max(0f, climbSpeed);
        }

        /// <summary>
        /// 점프로 놓을 때 <b>뛰어내릴 방향</b> — 기본은 <b>바라보는 쪽</b>이다.
        ///
        /// <para><b>단, 사다리 쪽은 갈 수 없다.</b> 오르는 사람은 대개 사다리를 마주보고 있고,
        /// 그 방향으로 밀면 사다리·상판에 부딪혀 제자리에서 튀기만 한다. 그래서 시선이 사다리를
        /// 향하면 앞면을 거울 삼아 <b>반사</b>한다 — 정면으로 보고 있었다면 정확히 <b>뒤로</b>,
        /// 비스듬히 보고 있었다면 <b>비스듬히 뒤로</b> 나간다.</para>
        ///
        /// <para><b>왜 투영이 아니라 반사인가.</b> 앞면에 투영하면 정면을 보고 뛸 때 방향이
        /// <b>0</b>이 되어 제자리 낙하가 된다. 반사는 접선 성분을 그대로 두고 법선 성분만
        /// 뒤집으므로 어느 각도에서도 크기가 유지되고, 옆을 볼 때(법선 성분 0) 반사해도 같은
        /// 방향이라 <b>경계에서 튀지 않는다</b>.</para>
        /// </summary>
        /// <param name="lookDirection">바라보는 방향. 수평 성분만 쓴다.</param>
        /// <param name="outward">사다리 앞면 법선(물 쪽).</param>
        public static Vector3 ResolveJumpOffDirection(Vector3 lookDirection, Vector3 outward)
        {
            Vector3 normal = new Vector3(outward.x, 0f, outward.z);
            if (normal.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            normal.Normalize();

            Vector3 look = new Vector3(lookDirection.x, 0f, lookDirection.z);
            if (look.sqrMagnitude < 0.0001f)
            {
                // 시선이 수직이라 수평 방향이 없다 — 앞면 밖으로 내보낸다.
                return normal;
            }

            look.Normalize();
            return Vector3.Dot(look, normal) >= 0f ? look : Vector3.Reflect(look, normal).normalized;
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
        /// 지금 붙을 수 있는 높이인가 — <b>사다리 밑을 지나지 않았어야</b> 한다.
        ///
        /// <para><b>왜 붙기에도 높이를 보는가.</b> 이 검사가 없으면 밑으로 빠져 놓아 준
        /// <b>바로 다음 프레임</b>에 <b>같은 입력</b>(계속 누르고 있는 S)으로 다시 붙는다.
        /// 놓기와 붙기가 매 프레임 번갈아 일어나며 사다리가 끝난 뒤에도 물속으로 끝없이
        /// 끌려 내려간다 — <see cref="HasFallenBelow"/>가 놓아 준 것을 붙기가 곧바로 취소하는
        /// 구도다.</para>
        ///
        /// <para><b>꼭대기는 보지 않는다.</b> 위쪽 재부착은 참조를 끊고 잠깐 차단하는 쪽이
        /// 이미 막고 있고, 여기서 상한을 걸면 <b>상판에서 사다리를 타고 내려가는</b> 경로까지
        /// 함께 막힌다 — 상판 위에 선 발은 <see cref="HasReachedTop"/>의 경계에 걸쳐 있다.</para>
        /// </summary>
        public static bool CanAttach(float footY, float bottomY)
        {
            return !HasFallenBelow(footY, bottomY);
        }

        /// <summary>
        /// 상판 위에서 사다리를 잡지 않게 하는 여유 (m). <see cref="HasReachedTop"/>의 기준선에서
        /// 이만큼 <b>아래</b>부터 잡힌다.
        ///
        /// <para>기준선 자체는 상판 상면보다 살짝 위(<c>SeaLadder._topY</c> = 0.1)라, 상면에 선 발은
        /// 그 아래에 놓여 <b>기준선만으로는 걸러지지 않는다</b>. <c>CharacterController</c>가
        /// <c>skinWidth</c>(0.08)만큼 떠 있을 수 있는 것까지 감안해 상면보다 확실히 낮은 곳부터
        /// 잡도록 여유를 둔다 — 물에서 올라오는 경로는 물면이 −4라 전혀 영향받지 않는다.</para>
        /// </summary>
        public const float AttachMarginBelowTop = 0.2f;

        /// <summary>
        /// 지금 붙을 수 있는가 — <b>사다리 구간 안</b>이어야 한다. 위아래 양쪽을 본다.
        ///
        /// <para><b>아래.</b> 밑으로 빠져 놓아 준 <b>바로 다음 프레임</b>에 <b>같은 입력</b>(계속
        /// 누르고 있는 S)으로 다시 붙으면, 놓기와 붙기가 매 프레임 번갈아 일어나며 사다리가 끝난
        /// 뒤에도 물속으로 끝없이 끌려 내려간다 (<see cref="CanAttach(float,float)"/>).</para>
        ///
        /// <para><b>위 — 상판에 선 사람은 사다리를 잡지 않는다.</b> 붙기 조건이 "볼륨 안 + 세로
        /// 입력"뿐이면 <b>상판을 달리다</b> 볼륨을 스치는 순간 잡히고, 상판 위의 발은 이미
        /// <see cref="HasReachedTop"/>의 경계 위라 <b>다음 프레임에 곧바로 꼭대기로 판정</b>되어
        /// 올라서기 자리로 순간이동한다 — 달리기만 했는데 몸이 옆으로 튄다 (11회차 결함 ②).</para>
        ///
        /// <para>상판에서 물로 내려가는 길은 <b>가장자리로 걸어 나가는 것</b>이다. 낙하 피해가 없고
        /// 물면이 −4라 그대로 수영으로 이어진다 — 사다리를 내려갈 이유가 애초에 없다.</para>
        /// </summary>
        public static bool CanAttach(float footY, float bottomY, float topY)
        {
            return CanAttach(footY, bottomY) && footY < topY - AttachMarginBelowTop;
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
