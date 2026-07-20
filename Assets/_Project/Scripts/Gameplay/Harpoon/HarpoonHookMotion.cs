namespace Game.Gameplay.Harpoon
{
    /// <summary>집게 훅(투사체) 비행 단계.</summary>
    public enum HookPhase
    {
        /// <summary>미사용 — 풀에 반환된 상태.</summary>
        Idle,

        /// <summary>전방으로 비행 중 (충돌 판정 대상).</summary>
        Flying,

        /// <summary>그랩 가능 대상에 명중 → 호스트 확정 대기 (탄성 연출 구간).</summary>
        WaitingForServer,

        /// <summary>호스트 승인 — 대상에 부착되어 견인을 시각적으로 따라간다.</summary>
        Attached,

        /// <summary>빗나감 직후 짧은 정지(피격 연출) — 이후 자동으로 되감기로 전이한다.</summary>
        ImpactPause,

        /// <summary>총구를 향해 되돌아가는 중 (실패 연출).</summary>
        Retracting
    }

    /// <summary>
    /// 집게 훅의 단계 전이 순수 로직. <see cref="HarpoonProjectile"/>(MonoBehaviour)은 이 상태만 참조해
    /// 실제 Transform 이동·충돌 판정을 수행한다. 엔진·네트워크 무의존 — EditMode 테스트 대상.
    /// </summary>
    public sealed class HarpoonHookMotion
    {
        private readonly float _impactPauseDuration;
        private readonly float _waitingForServerTimeout;
        private float _timer;

        public HookPhase Phase { get; private set; } = HookPhase.Idle;

        public HarpoonHookMotion(float impactPauseDuration, float waitingForServerTimeout)
        {
            _impactPauseDuration = impactPauseDuration;
            _waitingForServerTimeout = waitingForServerTimeout;
        }

        /// <summary>발사 — Flying으로 전이한다. 어떤 상태에서든 호출 가능 (재사용 시 초기화).</summary>
        public void StartFlying()
        {
            Phase = HookPhase.Flying;
            _timer = 0f;
        }

        /// <summary>Flying 중 그랩 가능 대상에 명중 — 호스트 확정 대기로 전이한다.</summary>
        public void NotifyGrabbableHit()
        {
            if (Phase == HookPhase.Flying)
            {
                Phase = HookPhase.WaitingForServer;
                _timer = _waitingForServerTimeout;
            }
        }

        /// <summary>Flying 중 빗나감(비그랩 지형 명중 또는 사거리 도달) — 피격 정지 후 되감기로 전이한다.</summary>
        public void NotifyMiss()
        {
            if (Phase == HookPhase.Flying)
            {
                Phase = HookPhase.ImpactPause;
                _timer = _impactPauseDuration;
            }
        }

        /// <summary>호스트 승인 — 대상 부착으로 전이한다. Idle이 아니면 언제든 허용(늦게 도착한 승인도 처리).</summary>
        public void Attach()
        {
            if (Phase != HookPhase.Idle)
            {
                Phase = HookPhase.Attached;
            }
        }

        /// <summary>즉시 되감기 시작 (호스트 거부·강제 해제). 피격 정지 단계를 건너뛴다.</summary>
        public void BeginRetract()
        {
            if (Phase != HookPhase.Idle)
            {
                Phase = HookPhase.Retracting;
            }
        }

        /// <summary>되감기 도착 통지 — Idle로 전이한다.</summary>
        public void NotifyRetractArrived()
        {
            if (Phase == HookPhase.Retracting)
            {
                Phase = HookPhase.Idle;
            }
        }

        /// <summary>즉시 초기화 (취소·정리). 어떤 상태에서든 Idle로 되돌린다.</summary>
        public void Cancel()
        {
            Phase = HookPhase.Idle;
            _timer = 0f;
        }

        /// <summary>
        /// 타이머 기반 자동 전이 (ImpactPause 만료 → Retracting, WaitingForServer 타임아웃 → Retracting 안전장치).
        /// true를 반환하면 이번 프레임에 Retracting으로 전이했다.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (Phase != HookPhase.ImpactPause && Phase != HookPhase.WaitingForServer)
            {
                return false;
            }

            _timer -= deltaTime;
            if (_timer > 0f)
            {
                return false;
            }

            Phase = HookPhase.Retracting;
            return true;
        }
    }
}
