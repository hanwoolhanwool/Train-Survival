using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>사다리에서 떨어진 이유 — 처리가 사유마다 다르다 (사다리 계획 §3.7).</summary>
    public enum LadderDetachReason
    {
        /// <summary>계속 매달려 있다.</summary>
        None = 0,

        /// <summary>점프로 밀어냈다 — 법선 방향으로 튕겨내지 않으면 곧바로 다시 붙는다.</summary>
        Jump = 1,

        /// <summary>꼭대기에 닿았다 — 갑판 위로 올려놓아야 한다. 그냥 놓으면 갑판 <b>옆</b> 허공이다.</summary>
        TopReached = 2,

        /// <summary>발치에 닿았다 — 지상 접지로 돌아간다.</summary>
        BottomReached = 3,

        /// <summary>볼륨을 벗어났다 — 칸 이탈·파괴로 사다리가 사라지면 여기로 들어온다.</summary>
        LeftVolume = 4,
    }

    /// <summary>
    /// 사다리 오르기 판정 (사다리 승하차 계획 §3.3) — 전부 순수 함수, EditMode 테스트 대상.
    ///
    /// <para><b>이 기능의 진짜 문제는 좌표계다.</b> 지상에 접지한 플레이어는 스크롤 속도만큼 뒤로
    /// 밀리는데(네트워크 문서 §4.2 상시 외력형) 사다리는 열차 소속이라 붙는 순간 그 밀림이 꺼져야 한다.
    /// 이 로직은 그 전환의 <b>시점</b>만 정하고, 실제로 끄는 것은 호출부다.</para>
    ///
    /// <para><b>상태를 갖지 않는다.</b> "지금 오르는 중인가"는 호출부가 들고 있고 여기에는 인자로 들어온다 —
    /// 소유자 로컬 판정이라 복제 대상이 아니기 때문이다 (계획 §3.2).</para>
    /// </summary>
    public static class LadderClimbLogic
    {
        /// <summary>붙는 데 필요한 최소 정렬 — 약 72°. 옆걸음으로 스치기만 해도 붙으면 성가시다.</summary>
        public const float DefaultApproachDot = 0.3f;

        /// <summary>
        /// 사다리에 붙을 것인가. 세 조건이 <b>동시에</b> 참일 때만 붙는다 (계획 §3.5) —
        /// 볼륨 안 · 이동 입력이 사다리 쪽 · 아직 안 붙음.
        /// </summary>
        /// <param name="wishDirection">이동 입력의 월드 방향. 길이는 보지 않는다(정규화해서 쓴다).</param>
        /// <param name="ladderApproachDirection">오르는 사람이 <b>바라보는</b> 방향 = 사다리를 향하는 수평 방향.</param>
        public static bool ShouldAttach(bool insideVolume, Vector3 wishDirection,
            Vector3 ladderApproachDirection, bool alreadyClimbing, float dotThreshold)
        {
            if (!insideVolume || alreadyClimbing)
            {
                return false;
            }

            // 입력이 없으면 붙지 않는다 — 볼륨 안에 가만히 서 있는 것만으로 매달리면
            // 사다리 옆을 지나가려던 사람이 붙잡힌다.
            Vector3 wish = Flatten(wishDirection);
            Vector3 approach = Flatten(ladderApproachDirection);
            if (wish.sqrMagnitude < 0.0001f || approach.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            return Vector3.Dot(wish.normalized, approach.normalized) >= dotThreshold;
        }

        /// <summary>이번 프레임의 오르내림 이동량 — 사다리 축은 언제나 월드 +Y다.</summary>
        public static Vector3 ComputeClimbMotion(float verticalInput, float climbSpeed, float deltaTime)
        {
            float input = Mathf.Clamp(verticalInput, -1f, 1f);
            return Vector3.up * (input * Mathf.Max(0f, climbSpeed) * Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// 떨어질 것인가, 어떤 이유로인가 (계획 §3.7).
        ///
        /// <para><b>순서가 규칙이다.</b> 점프가 가장 앞선다 — 꼭대기에서 점프하면 "올라서기"가 아니라
        /// "뛰어내리기"여야 한다. 볼륨 이탈이 그다음인 이유는 사다리가 사라진 상황에서 상단·하단
        /// 좌표를 믿을 수 없기 때문이다.</para>
        /// </summary>
        public static LadderDetachReason ResolveDetach(float feetY, float bottomY, float topY,
            bool jumpPressed, bool insideVolume)
        {
            if (jumpPressed)
            {
                return LadderDetachReason.Jump;
            }

            if (!insideVolume)
            {
                return LadderDetachReason.LeftVolume;
            }

            if (feetY >= topY)
            {
                return LadderDetachReason.TopReached;
            }

            if (feetY <= bottomY)
            {
                return LadderDetachReason.BottomReached;
            }

            return LadderDetachReason.None;
        }

        /// <summary>
        /// 몸을 사다리 평면으로 되당기는 보정 이동량. 이것이 없으면 오르는 동안 옆으로 새고,
        /// 새다가 볼륨을 벗어나 <see cref="LadderDetachReason.LeftVolume"/>이 오작동한다.
        /// <b>수평만</b> 건드린다 — 높이는 오르내림이 소유한다.
        /// </summary>
        /// <param name="ladderNormal">사다리 앞면 법선 = 오르는 사람이 서는 쪽.</param>
        /// <param name="holdDistance">사다리 중심선에서 몸 중심까지 유지할 거리.</param>
        public static Vector3 ResolvePlaneCorrection(Vector3 position, Vector3 ladderOrigin,
            Vector3 ladderNormal, float holdDistance)
        {
            Vector3 normal = Flatten(ladderNormal);
            if (normal.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 target = Flatten(ladderOrigin) + normal.normalized * holdDistance;
            Vector3 current = Flatten(position);
            return target - current;
        }

        /// <summary>
        /// 평면 보정이 한 프레임에 감당할 범위를 넘었는가 — 넘었다면 사다리가 <b>통째로 옮겨간</b> 것이다
        /// (후미 칸 이탈·파괴로 재배치, 계획 §6). 그대로 따라가면 사람이 순간이동하므로 떨어뜨린다.
        ///
        /// <para>볼륨 이탈(<see cref="LadderDetachReason.LeftVolume"/>)에만 맡길 수 없다 —
        /// 트리거 콜백은 물리 갱신 시점에 와서 한 프레임 늦고, 그 사이에 보정이 먼저 순간이동시킨다.</para>
        /// </summary>
        public static bool IsPlaneCorrectionTooFar(Vector3 correction, float maxDistance)
        {
            float limit = Mathf.Max(0f, maxDistance);
            return correction.sqrMagnitude > limit * limit;
        }

        /// <summary>
        /// 점프로 떨어져 나갈 때의 속도. 법선 쪽으로 밀어내지 않으면 다음 프레임에 다시 붙어
        /// 제자리에서 튀기만 한다.
        /// </summary>
        public static Vector3 ComputeJumpOffVelocity(Vector3 ladderNormal, float pushSpeed, float upSpeed)
        {
            Vector3 normal = Flatten(ladderNormal);
            Vector3 push = normal.sqrMagnitude < 0.0001f
                ? Vector3.zero
                : normal.normalized * Mathf.Max(0f, pushSpeed);

            return push + Vector3.up * Mathf.Max(0f, upSpeed);
        }

        /// <summary>
        /// 꼭대기에서 갑판으로 올려놓는 이동량 (계획 §3.8) — 사다리 반대쪽(갑판 안)으로 밀고
        /// 발을 갑판면에 올린다. 이 한 번의 이동이 없으면 갑판 옆 허공에서 그대로 떨어진다.
        /// </summary>
        /// <param name="feetY">지금 발 높이.</param>
        /// <param name="deckY">갑판 상면 y.</param>
        /// <param name="inwardDistance">갑판 안쪽으로 밀 거리 — 캡슐 반경의 2배가 기준이다.</param>
        /// <param name="clearance">갑판면에서 띄울 여유.</param>
        public static Vector3 ComputeMantleMotion(Vector3 ladderNormal, float feetY, float deckY,
            float inwardDistance, float clearance)
        {
            Vector3 normal = Flatten(ladderNormal);
            Vector3 inward = normal.sqrMagnitude < 0.0001f
                ? Vector3.zero
                : -normal.normalized * Mathf.Max(0f, inwardDistance);

            // 이미 갑판보다 높으면 끌어내리지 않는다 — 올려놓기이지 맞추기가 아니다.
            float lift = Mathf.Max(0f, deckY + clearance - feetY);
            return inward + Vector3.up * lift;
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
