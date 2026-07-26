using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸과 칸 사이 연결부 하나를 표현·피격받는 부위 (기획서 §9 — 연결부 = 밤 방어전 핵심 방어 목표).
    /// 몬스터의 공격 표적(<see cref="IDamageable"/>)이자, 끊김·인접 칸 이탈 시 연결 표현을 끄는 뷰다.
    /// 데미지 확정·후방 연쇄 이탈은 호스트(<see cref="ITrainDamageSink"/>)가 담당한다.
    /// 칸 c와 칸 c+1 사이 지점에 배치하고 <see cref="_couplingIndex"/>를 c로 지정한다.
    /// </summary>
    public sealed class CouplingPart : MonoBehaviour, IDamageable
    {
        [Tooltip("연결부 인덱스 c — 칸 c(전방)와 칸 c+1(후방)을 잇는다. TrainState의 연결부 배열과 1:1 대응.")]
        [SerializeField, Min(0)] private int _couplingIndex;

        private Renderer[] _renderers;
        private bool _registeredAsTarget;

        // ── IDamageable — 몬스터가 공격하는 표적면 (데미지 확정은 호스트) ──────────

        public bool IsAlive => IsCouplingLive();

        public void ApplyDamage(float amount, ulong instigatorClientId)
        {
            if (ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                sink.ApplyCouplingDamage(_couplingIndex, amount);
            }
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        private void Start()
        {
            UpdateTargetRegistration();
        }

        private void OnEnable()
        {
            EventBus<TrainInitializedEvent>.Subscribe(OnTrainInitialized);
            EventBus<CouplingStateChangedEvent>.Subscribe(OnCouplingStateChanged);
            EventBus<CarStateChangedEvent>.Subscribe(OnCarStateChanged);
            SyncFromState();
        }

        private void OnDisable()
        {
            EventBus<TrainInitializedEvent>.Unsubscribe(OnTrainInitialized);
            EventBus<CouplingStateChangedEvent>.Unsubscribe(OnCouplingStateChanged);
            EventBus<CarStateChangedEvent>.Unsubscribe(OnCarStateChanged);
        }

        private void OnDestroy()
        {
            if (_registeredAsTarget && ServiceLocator.TryGet(out ITrainTargetRegistry registry))
            {
                registry.Unregister(transform);
                _registeredAsTarget = false;
            }
        }

        /// <summary>
        /// 공격 대상 등록을 현재 상태에 맞춘다 — 끊겼거나 잇는 칸이 이탈하면 즉시 표적에서 빠진다
        /// (Feature 1: 연결 해제된 연결부는 더 이상 몬스터 공격 대상이 아니다).
        /// </summary>
        private void UpdateTargetRegistration()
        {
            if (!ServiceLocator.TryGet(out ITrainTargetRegistry registry))
            {
                return;
            }

            bool eligible = IsCouplingLive();
            if (eligible && !_registeredAsTarget)
            {
                registry.Register(transform, this);
                _registeredAsTarget = true;
            }
            else if (!eligible && _registeredAsTarget)
            {
                registry.Unregister(transform);
                _registeredAsTarget = false;
            }
        }

        private void OnTrainInitialized(TrainInitializedEvent _)
        {
            SyncFromState();
        }

        private void OnCouplingStateChanged(CouplingStateChangedEvent evt)
        {
            if (evt.Index == _couplingIndex)
            {
                SyncFromState();
            }
        }

        private void OnCarStateChanged(CarStateChangedEvent evt)
        {
            // 잇는 두 칸(c, c+1) 중 하나라도 이탈·파괴되면 연결 표현이 사라져야 한다.
            if (evt.Index == _couplingIndex || evt.Index == _couplingIndex + 1)
            {
                SyncFromState();
            }
        }

        private void SyncFromState()
        {
            bool live = IsCouplingLive();
            UpdateTargetRegistration();

            if (_renderers == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = live;
            }
        }

        /// <summary>연결부가 끊기지 않았고 잇는 두 칸이 모두 편성에 살아 붙어 있는지.</summary>
        private bool IsCouplingLive()
        {
            if (!ServiceLocator.TryGet(out ITrainState train))
            {
                return false;
            }

            if (!train.TryGetCoupling(_couplingIndex, out CouplingState coupling) || coupling.Broken)
            {
                return false;
            }

            return train.TryGetCar(_couplingIndex, out CarState front) && TrainStateLogic.IsCarPresent(front)
                && train.TryGetCar(_couplingIndex + 1, out CarState rear) && TrainStateLogic.IsCarPresent(rear);
        }
    }
}
