using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 리볼버 실린더의 순수 상태 머신 — 장탄·발사 간격·재장전.
    /// 시간은 <see cref="Tick"/> 누적으로만 흐른다 (엔진 무의존, EditMode 테스트 대상).
    /// </summary>
    public sealed class RevolverCylinder
    {
        private readonly int _capacity;
        private readonly float _fireInterval;
        private readonly float _reloadDuration;

        private float _fireCooldown;
        private float _reloadRemaining;

        public RevolverCylinder(int capacity, float fireInterval, float reloadDuration)
        {
            _capacity = Mathf.Max(1, capacity);
            _fireInterval = Mathf.Max(0f, fireInterval);
            _reloadDuration = Mathf.Max(0f, reloadDuration);
            RoundsLoaded = _capacity;
        }

        public int Capacity => _capacity;

        public int RoundsLoaded { get; private set; }

        public bool IsReloading => _reloadRemaining > 0f;

        public void Tick(float deltaTime)
        {
            _fireCooldown = Mathf.Max(0f, _fireCooldown - deltaTime);

            if (_reloadRemaining > 0f)
            {
                _reloadRemaining -= deltaTime;
                if (_reloadRemaining <= 0f)
                {
                    _reloadRemaining = 0f;
                    RoundsLoaded = _capacity;
                }
            }
        }

        /// <summary>발사를 시도한다 — 성공 시 1발 소모 + 발사 간격 쿨다운 시작.</summary>
        public bool TryFire()
        {
            if (IsReloading || RoundsLoaded <= 0 || _fireCooldown > 0f)
            {
                return false;
            }

            RoundsLoaded -= 1;
            _fireCooldown = _fireInterval;
            return true;
        }

        /// <summary>재장전을 시도한다 — 이미 만탄이거나 재장전 중이면 실패.</summary>
        public bool TryStartReload()
        {
            if (IsReloading || RoundsLoaded >= _capacity)
            {
                return false;
            }

            _reloadRemaining = _reloadDuration;
            return true;
        }
    }
}
