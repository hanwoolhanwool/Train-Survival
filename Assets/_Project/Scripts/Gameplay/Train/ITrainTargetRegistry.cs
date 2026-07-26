using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 공격 가능한 열차 부위(칸·연결부) 표적 등록소 — 몬스터가 열차를 공격 대상으로 삼기 위한 조회면.
    /// 리시버가 스스로 등록/해제하고, 몬스터 AI는 <see cref="TryGetNearest"/>로 가장 가까운 살아있는 부위를 찾는다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainTargetRegistry
    {
        /// <summary>표적을 등록한다(중복 등록은 무시).</summary>
        void Register(Transform target, IDamageable damageable);

        /// <summary>표적 등록을 해제한다.</summary>
        void Unregister(Transform target);

        /// <summary>
        /// <paramref name="from"/>에서 가장 가까운, 살아있는(<see cref="IDamageable.IsAlive"/>) 열차 부위를 찾는다.
        /// 없으면 false.
        /// </summary>
        bool TryGetNearest(Vector3 from, out Transform target, out IDamageable damageable);
    }
}
