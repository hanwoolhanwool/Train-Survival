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

        /// <summary>살아 있는 연결부 중 가장 후미만 공격 대상이다(후미 순차 파괴 — 앞쪽 연결부는 뒤가 끊겨야 표적이 된다).</summary>
        public bool IsAlive => IsCouplingTargetable();

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
        /// 공격 대상 등록을 현재 상태에 맞춘다 — 끊겼거나 잇는 칸이 이탈하면 즉시 표적에서 빠지고
        /// (Feature 1), 뒤쪽에 살아 있는 연결부가 남아 있는 동안에도 표적이 아니다(후미 순차 파괴).
        /// </summary>
        private void UpdateTargetRegistration()
        {
            if (!ServiceLocator.TryGet(out ITrainTargetRegistry registry))
            {
                return;
            }

            bool eligible = IsCouplingTargetable();
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
            // 자기 연결부는 물론, 뒤쪽 연결부가 끊기면 이 연결부가 새 후미 표적이 되므로 함께 재동기화한다.
            if (evt.Index >= _couplingIndex)
            {
                SyncFromState();
            }
        }

        private void OnCarStateChanged(CarStateChangedEvent evt)
        {
            // 잇는 두 칸(c, c+1)의 이탈·파괴는 연결 표현을 끄고, 그보다 뒤 칸의 변화는
            // 뒤쪽 연결부의 생사를 바꿔 이 연결부의 표적 여부(후미 순차 파괴)를 바꿀 수 있다.
            if (evt.Index >= _couplingIndex)
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

        /// <summary>공격 대상인지 — 살아 있는 연결부 중 가장 후미(복제 데이터 기반, 전 피어 동일 판정).</summary>
        private bool IsCouplingTargetable()
        {
            return ServiceLocator.TryGet(out ITrainState train) && train.IsCouplingTargetable(_couplingIndex);
        }

        /// <summary>연결부가 끊기지 않았고 잇는 두 칸이 모두 편성에 살아 붙어 있는지(연결 표현용).</summary>
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
