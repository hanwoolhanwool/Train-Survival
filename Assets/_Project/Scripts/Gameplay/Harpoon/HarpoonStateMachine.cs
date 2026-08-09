using System;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게 조작 상태 머신 순수 로직 (슬라이스 스펙 §2.1~§2.3).
    /// 네트워크·연출 무의존 — 소유자 클라이언트에서 입력을 게이트하며 EditMode 테스트로 검증한다.
    /// 전이 규칙:
    /// Ready --발사--> Firing --로컬 명중--> PendingGrab --승인--> Reeling --도착/취소--> Cooldown --> Ready
    /// Firing --빗나감--> MissRecovery --> Ready,  PendingGrab --거부--> MissRecovery --> Ready
    /// Reeling --도착(파지)--> Holding --놓기(취소·강제 해제)--> Cooldown (M5 6차 — 몬스터 파지)
    /// </summary>
    public sealed class HarpoonStateMachine
    {
        private readonly float _missRecoveryDuration;
        private readonly float _cooldownDuration;
        private float _timer;

        public HarpoonState State { get; private set; } = HarpoonState.Ready;

        /// <summary>MissRecovery/Cooldown 상태의 남은 잠금 시간 (초). 그 외 상태에서는 0.</summary>
        public float RemainingLockTime => _timer;

        public HarpoonStateMachine(float missRecoveryDuration, float cooldownDuration)
        {
            if (missRecoveryDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(missRecoveryDuration));
            }

            if (cooldownDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownDuration));
            }

            _missRecoveryDuration = missRecoveryDuration;
            _cooldownDuration = cooldownDuration;
        }

        /// <summary>발사 시도. Ready에서만 성립한다.</summary>
        public bool TryFire()
        {
            if (State != HarpoonState.Ready)
            {
                return false;
            }

            State = HarpoonState.Firing;
            return true;
        }

        /// <summary>
        /// 그랩 취소 시도. 릴 감기·파지 중에만 유효 — 투사체 비행 중 취소는 무효 (§2.1).
        /// 취소 성공 시 미스 페널티 없이 발사 쿨다운만 적용된다 (§2.2).
        /// 파지 중 취소 = 놓기 (M5 6차) — 같은 경로를 그대로 쓴다.
        /// </summary>
        public bool TryCancel()
        {
            if (State != HarpoonState.Reeling && State != HarpoonState.Holding)
            {
                return false;
            }

            EnterCooldown();
            return true;
        }

        /// <summary>투사체 로컬 명중 판정 → 호스트 승인 대기로 전환.</summary>
        public void NotifyLocalHit()
        {
            if (State == HarpoonState.Firing)
            {
                State = HarpoonState.PendingGrab;
            }
        }

        /// <summary>투사체 빗나감 → 미스 페널티 시작 (§2.3).</summary>
        public void NotifyMiss()
        {
            if (State == HarpoonState.Firing)
            {
                EnterMissRecovery();
            }
        }

        /// <summary>호스트 그랩 승인 → 릴 감기 시작.</summary>
        public void NotifyGrabApproved()
        {
            if (State == HarpoonState.PendingGrab)
            {
                State = HarpoonState.Reeling;
            }
        }

        /// <summary>호스트 그랩 거부 → 미끄러짐 연출 후 미스 페널티 전환 (§2.4).</summary>
        public void NotifyGrabRejected()
        {
            if (State == HarpoonState.PendingGrab)
            {
                EnterMissRecovery();
            }
        }

        /// <summary>대상 도착(획득 완료) → 발사 쿨다운.</summary>
        public void NotifyTargetArrived()
        {
            if (State == HarpoonState.Reeling)
            {
                EnterCooldown();
            }
        }

        /// <summary>
        /// 대상 도착 — 파지 유지 (M5 6차). 릴이 끝나도 놓지 않고 붙잡은 채 든다.
        /// Holding 중 좌클릭(재발사)은 Ready가 아니므로 자연 차단된다.
        /// </summary>
        public void NotifyHeld()
        {
            if (State == HarpoonState.Reeling)
            {
                State = HarpoonState.Holding;
            }
        }

        /// <summary>서버 사정에 의한 강제 해제 (대상 소멸 등). 감기·파지 중이었다면 쿨다운으로 전환.</summary>
        public void NotifyForcedRelease()
        {
            if (State == HarpoonState.Reeling || State == HarpoonState.PendingGrab
                || State == HarpoonState.Holding)
            {
                EnterCooldown();
            }
        }

        public void Tick(float deltaTime)
        {
            if (State != HarpoonState.MissRecovery && State != HarpoonState.Cooldown)
            {
                return;
            }

            _timer -= deltaTime;
            if (_timer <= 0f)
            {
                _timer = 0f;
                State = HarpoonState.Ready;
            }
        }

        private void EnterMissRecovery()
        {
            State = HarpoonState.MissRecovery;
            _timer = _missRecoveryDuration;
        }

        private void EnterCooldown()
        {
            State = HarpoonState.Cooldown;
            _timer = _cooldownDuration;
        }
    }
}
