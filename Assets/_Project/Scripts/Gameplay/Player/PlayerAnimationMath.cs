using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>이동 애니메이션 단계 — Speed 파라미터 산출의 이산 상태 (히스테리시스 대상).</summary>
    public enum LocomotionTier
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
    }

    /// <summary>
    /// 점프 감지 누적 상태 — 프레임 간 이어지는 값이라 호출자가 보관한다.
    /// </summary>
    public struct JumpDetectState
    {
        /// <summary>공중에서 상승 문턱을 연속으로 넘긴 프레임 수.</summary>
        public int RisingFrames;

        /// <summary>이번 체공에서 이미 점프로 판정했는가 — 착지 전 재판정을 막는다.</summary>
        public bool Latched;
    }

    /// <summary>
    /// 애니메이션 파라미터 산출 순수 로직 — EditMode 테스트 대상 (플레이어 확장 계획 §2.1~2.2).
    /// 원격 플레이어는 동기화 채널 없이 보간 위치 델타로 속도를 추정하되, 지상(월드 프레임)에
    /// 서 있으면 스크롤로 밀리는 성분을 빼야 "제자리인데 걷는" 오판이 없다.
    /// </summary>
    public static class PlayerAnimationMath
    {
        /// <summary>
        /// 월드 스크롤 성분 제거 — 지상(월드 프레임) 위에서는 스크롤 속도만큼 −Z로 밀리므로
        /// (상시 외력형, <see cref="NetworkPlayerController"/>와 같은 규약) 그만큼 되돌린다.
        /// 열차 위(정지 프레임)에서는 그대로 둔다.
        /// </summary>
        public static Vector3 RemoveWorldScroll(Vector3 worldVelocity, float scrollSpeed, bool onWorldFrame)
        {
            if (!onWorldFrame)
            {
                return worldVelocity;
            }

            return worldVelocity - Vector3.back * scrollSpeed;
        }

        /// <summary>수평(XZ) 속력.</summary>
        public static float HorizontalSpeed(Vector3 velocity)
        {
            velocity.y = 0f;
            return velocity.magnitude;
        }

        /// <summary>
        /// 원격 플레이어 속도 추정 — 보간 위치의 프레임 델타에서 스크롤 성분을 제거한 수평 속력.
        /// 텔레포트(부활·재접속 복원)의 큰 델타는 이동이 아니므로 상한 초과 시 0으로 버린다.
        /// </summary>
        public static float EstimateHorizontalSpeed(
            Vector3 positionDelta, float deltaTime, float scrollSpeed, bool onWorldFrame,
            float teleportSpeedThreshold)
        {
            if (deltaTime <= 0f)
            {
                return 0f;
            }

            Vector3 velocity = RemoveWorldScroll(positionDelta / deltaTime, scrollSpeed, onWorldFrame);
            float speed = HorizontalSpeed(velocity);
            return speed > teleportSpeedThreshold ? 0f : speed;
        }

        /// <summary>
        /// 이동 단계 판정 — 경계 양쪽에 진입/이탈 문턱을 벌려(히스테리시스) 경계 근처에서
        /// 파라미터가 진동하지 않게 한다 (확장 계획 §2.2 — 원격 떨림을 애니 층위에서 흡수).
        /// Walk↔Run 경계는 <paramref name="runBoundarySpeed"/> ± <paramref name="hysteresisBand"/>.
        /// </summary>
        public static LocomotionTier StepTier(
            LocomotionTier previous, float speed,
            float walkEnterSpeed, float idleEnterSpeed,
            float runBoundarySpeed, float hysteresisBand)
        {
            bool running = previous == LocomotionTier.Run
                ? speed >= runBoundarySpeed - hysteresisBand
                : speed > runBoundarySpeed + hysteresisBand;
            if (running)
            {
                return LocomotionTier.Run;
            }

            bool moving = previous == LocomotionTier.Idle
                ? speed > walkEnterSpeed
                : speed > idleEnterSpeed;
            return moving ? LocomotionTier.Walk : LocomotionTier.Idle;
        }

        /// <summary>
        /// 단계별 Speed 파라미터 목표값 — 실측 속력을 따르되, 판정된 단계의 영역(Walk↔Run 경계
        /// 기준) 밖으로는 내보내지 않는다. 블렌드가 경계를 넘나들며 떨리는 것을 막는 마감쇠다.
        /// </summary>
        public static float TierTargetSpeed(LocomotionTier tier, float rawSpeed, float runBoundarySpeed)
        {
            switch (tier)
            {
                case LocomotionTier.Idle:
                    return 0f;
                case LocomotionTier.Walk:
                    return Mathf.Clamp(rawSpeed, 0f, runBoundarySpeed);
                default:
                    return Mathf.Max(rawSpeed, runBoundarySpeed);
            }
        }

        /// <summary>
        /// 지수 스무딩 — 반감기 <paramref name="halfLifeSeconds"/>마다 목표와의 거리가 절반이
        /// 된다. 프레임레이트와 무관하게 같은 속도로 수렴한다 (확장 계획 §2.2 — 반감기 ~0.15 s).
        /// </summary>
        public static float SmoothTowards(float current, float target, float halfLifeSeconds, float deltaTime)
        {
            if (halfLifeSeconds <= 0f || deltaTime <= 0f)
            {
                return target;
            }

            float blend = 1f - Mathf.Pow(2f, -deltaTime / halfLifeSeconds);
            return current + (target - current) * blend;
        }

        /// <summary>
        /// 소유자 점프 감지 — 공중에서 상승 속도가 문턱을 <b>연속 프레임</b>으로 유지할 때만
        /// 점프로 본다. <see cref="CharacterController.velocity"/>는 변위/dt라 칸 모듈 이음새·
        /// StepOffset 스냅에서 1프레임 수직 스파이크가 나오는데, 실제 점프(v₀≈7 m/s, 감쇠 20 m/s²)는
        /// 문턱 위를 수백 ms 유지하므로 연속성으로 갈라진다. 체공당 1회만 판정한다(래치).
        /// </summary>
        public static bool StepJumpDetect(
            ref JumpDetectState state, bool grounded, float verticalSpeed,
            float risingSpeedThreshold, int confirmFrames)
        {
            if (grounded)
            {
                state.RisingFrames = 0;
                state.Latched = false;
                return false;
            }

            if (state.Latched)
            {
                return false;
            }

            state.RisingFrames = verticalSpeed > risingSpeedThreshold ? state.RisingFrames + 1 : 0;
            if (state.RisingFrames < confirmFrames)
            {
                return false;
            }

            state.Latched = true;
            return true;
        }

        /// <summary>오일러 X(0~360)를 부호 있는 피치(−180~180, +가 내려다봄)로 변환한다.</summary>
        public static float SignedPitch(float eulerX)
        {
            return eulerX > 180f ? eulerX - 360f : eulerX;
        }

        /// <summary>
        /// 점프 중계 RPC 홍수 방지 — 서버가 1초 창 단위로 상한을 센다 (확장 계획 §2.2 — 상한 4/s).
        /// 게임 상태를 바꾸지 않는 연출 중계라 재검증은 없고 상한만 건다.
        /// </summary>
        public static bool TryConsumeJumpBudget(
            ref double windowStartTime, ref int usedInWindow, double now, int maxPerSecond)
        {
            if (maxPerSecond <= 0)
            {
                return false;
            }

            if (now - windowStartTime >= 1.0)
            {
                windowStartTime = now;
                usedInWindow = 0;
            }

            if (usedInWindow >= maxPerSecond)
            {
                return false;
            }

            usedInWindow++;
            return true;
        }
    }
}
