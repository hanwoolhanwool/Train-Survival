using System.Collections.Generic;
using Game.Core.Services;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// <see cref="ITrainTargetRegistry"/> 구현 — 공격 가능한 열차 부위 목록을 보관하고 최근접 조회를 제공한다.
    /// Awake에서 서비스로 등록해(리시버의 Start 등록보다 먼저) 등록 순서를 보장한다.
    /// Train 루트에 1개 배치한다(씬 상주). 조회는 호스트의 몬스터 AI가 사용한다.
    /// </summary>
    public sealed class TrainTargetRegistry : MonoBehaviour, ITrainTargetRegistry
    {
        private readonly List<Transform> _transforms = new List<Transform>();
        private readonly List<IDamageable> _damageables = new List<IDamageable>();

        private void Awake()
        {
            if (!ServiceLocator.IsRegistered<ITrainTargetRegistry>())
            {
                ServiceLocator.Register<ITrainTargetRegistry>(this);
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet(out ITrainTargetRegistry service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<ITrainTargetRegistry>();
            }
        }

        public void Register(Transform target, IDamageable damageable)
        {
            if (target == null || damageable == null || _transforms.Contains(target))
            {
                return;
            }

            _transforms.Add(target);
            _damageables.Add(damageable);
        }

        public void Unregister(Transform target)
        {
            int index = _transforms.IndexOf(target);
            if (index >= 0)
            {
                _transforms.RemoveAt(index);
                _damageables.RemoveAt(index);
            }
        }

        public bool TryGetNearest(Vector3 from, out Transform target, out IDamageable damageable)
        {
            target = null;
            damageable = null;

            float bestSqr = float.MaxValue;
            for (int i = 0; i < _transforms.Count; i++)
            {
                IDamageable candidate = _damageables[i];
                Transform t = _transforms[i];
                if (t == null || candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                float sqr = (t.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    target = t;
                    damageable = candidate;
                }
            }

            return target != null;
        }
    }
}
