using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>보스 돌진 패턴의 국면 (M7 2차 — 공통 패턴).</summary>
    public enum BossChargeState : byte
    {
        /// <summary>대기 — 쿨다운을 채우는 중. 평소처럼 추격한다.</summary>
        Ready = 0,

        /// <summary>예고 — 방향이 고정되고 전 피어 연출이 뜬다. 회피 여유.</summary>
        Telegraph = 1,

        /// <summary>돌진 — 고정 방향으로 직선 가속. 경로상 대상이 피해를 받는다.</summary>
        Charging = 2,

        /// <summary>경직 — 벽 충돌·돌진 종료 후의 반격 틈.</summary>
        Recover = 3,
    }

    /// <summary>돌진 상태 기계의 한 스텝 결과 — 갱신된 국면·타이머와 이번 스텝의 전이 여부.</summary>
    public readonly struct BossChargeStep
    {
        public readonly BossChargeState State;

        /// <summary>새 국면 안에서의 경과 시간 (초).</summary>
        public readonly float Timer;

        /// <summary>이번 스텝에 예고가 시작됐는가 (연출 RPC 발신 지점).</summary>
        public readonly bool EnteredTelegraph;

        /// <summary>이번 스텝에 돌진이 개시됐는가 (방향 고정 지점).</summary>
        public readonly bool EnteredCharge;

        /// <summary>이번 스텝에 경직이 시작됐는가.</summary>
        public readonly bool EnteredRecover;

        public BossChargeStep(
            BossChargeState state, float timer,
            bool enteredTelegraph, bool enteredCharge, bool enteredRecover)
        {
            State = state;
            Timer = timer;
            EnteredTelegraph = enteredTelegraph;
            EnteredCharge = enteredCharge;
            EnteredRecover = enteredRecover;
        }
    }

    /// <summary>
    /// 보스 돌진 패턴의 순수 상태 기계 (M7 2차 — 공통 패턴).
    /// 시간을 주입받아 전이만 계산하므로 EditMode에서 결정론적으로 검증된다 —
    /// 위치·피해 적용은 호출부(<see cref="BossAgent"/>, 호스트 전용)가 맡는다.
    /// </summary>
    public static class BossChargeMath
    {
        /// <summary>
        /// 돌진 상태를 한 스텝 진행한다.
        /// </summary>
        /// <param name="state">현재 국면.</param>
        /// <param name="timer">현재 국면 안에서의 경과 시간 (초).</param>
        /// <param name="deltaTime">이번 스텝의 경과 시간 (초).</param>
        /// <param name="cooldownSeconds">대기 국면의 길이 — 페이즈 배율이 이미 반영된 값을 넘긴다.</param>
        /// <param name="telegraphSeconds">예고 길이.</param>
        /// <param name="chargeSeconds">돌진 지속 길이.</param>
        /// <param name="recoverSeconds">경직 길이.</param>
        /// <param name="canStart">돌진을 시작해도 되는 상황인가 (표적 존재 등).</param>
        /// <param name="blocked">돌진 중 벽에 부딪혔는가 — 즉시 경직으로 전이한다.</param>
        public static BossChargeStep Step(
            BossChargeState state, float timer, float deltaTime,
            float cooldownSeconds, float telegraphSeconds, float chargeSeconds, float recoverSeconds,
            bool canStart, bool blocked)
        {
            float next = timer + Mathf.Max(0f, deltaTime);

            switch (state)
            {
                case BossChargeState.Telegraph:
                    return next >= Mathf.Max(0f, telegraphSeconds)
                        ? new BossChargeStep(BossChargeState.Charging, 0f, false, true, false)
                        : new BossChargeStep(BossChargeState.Telegraph, next, false, false, false);

                case BossChargeState.Charging:
                    // 벽 충돌은 남은 돌진 시간을 잘라내고 즉시 경직으로 보낸다 (반격 틈).
                    return blocked || next >= Mathf.Max(0f, chargeSeconds)
                        ? new BossChargeStep(BossChargeState.Recover, 0f, false, false, true)
                        : new BossChargeStep(BossChargeState.Charging, next, false, false, false);

                case BossChargeState.Recover:
                    return next >= Mathf.Max(0f, recoverSeconds)
                        ? new BossChargeStep(BossChargeState.Ready, 0f, false, false, false)
                        : new BossChargeStep(BossChargeState.Recover, next, false, false, false);

                default:
                    // 대기: 쿨다운이 찬 뒤에도 시작 조건이 갖춰질 때까지 타이머를 유지한다
                    // (표적이 없어 흘려보낸 쿨다운이 표적 복귀 즉시 돌진으로 이어진다).
                    if (canStart && next >= Mathf.Max(0f, cooldownSeconds))
                    {
                        return new BossChargeStep(BossChargeState.Telegraph, 0f, true, false, false);
                    }

                    return new BossChargeStep(BossChargeState.Ready, next, false, false, false);
            }
        }

        /// <summary>돌진 속도 벡터 — 고정된 수평 방향으로의 직선 주행 (조향 없음).</summary>
        public static Vector3 ComputeChargeVelocity(Vector3 lockedDirection, float chargeSpeed)
        {
            Vector3 flat = lockedDirection;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            return flat.normalized * Mathf.Max(0f, chargeSpeed);
        }
    }
}
