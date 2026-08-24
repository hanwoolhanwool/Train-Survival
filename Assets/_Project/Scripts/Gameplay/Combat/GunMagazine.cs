using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 총기 장탄부의 순수 상태 머신 — 장탄·발사 간격·재장전 (리볼버 실린더·샷건 튜브·볼트액션 탄창 공통).
    /// 시간은 <see cref="Tick"/> 누적으로만 흐른다 (엔진 무의존, EditMode 테스트 대상).
    /// 재장전은 예비 탄약(인벤토리 스택)을 소모한다: 로컬 선반영으로 즉시 시작하되,
    /// 장전 완료는 시간 경과 <b>그리고</b> 호스트 차감 확정(<see cref="ConfirmPendingLoad"/>) 양쪽을 요구한다.
    /// </summary>
    public sealed class GunMagazine
    {
        private readonly int _capacity;
        private readonly float _fireInterval;
        private readonly float _reloadDuration;

        private float _fireCooldown;
        private float _reloadRemaining;
        private int _pendingLoad;
        private bool _hasConfirm;
        private int _grantedRounds;

        public GunMagazine(int capacity, float fireInterval, float reloadDuration)
        {
            _capacity = Mathf.Max(1, capacity);
            _fireInterval = Mathf.Max(0f, fireInterval);
            _reloadDuration = Mathf.Max(0f, reloadDuration);
            RoundsLoaded = _capacity;
        }

        public int Capacity => _capacity;

        public int RoundsLoaded { get; private set; }

        public bool IsReloading => _pendingLoad > 0;

        /// <summary>진행 중 재장전이 요청한 발수 — 호스트 차감 요청 RPC의 페이로드.</summary>
        public int PendingLoad => _pendingLoad;

        public void Tick(float deltaTime)
        {
            _fireCooldown = Mathf.Max(0f, _fireCooldown - deltaTime);

            if (_pendingLoad > 0 && _reloadRemaining > 0f)
            {
                _reloadRemaining = Mathf.Max(0f, _reloadRemaining - deltaTime);
            }

            TryCompleteReload();
        }

        /// <summary>
        /// 장탄을 서버 확정 값으로 맞춘다 (M7 4차 §2.5) — 거치 무기는 장탄 권위가 서버에 있어
        /// 붙는 순간과 서버가 발사를 기각했을 때 이 값으로 되돌린다. 개인 화기는 쓰지 않는다
        /// (자기 탄창의 진실이 로컬에 있다).
        /// </summary>
        public void SetRounds(int rounds)
        {
            RoundsLoaded = Mathf.Clamp(rounds, 0, _capacity);
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

        /// <summary>
        /// 재장전을 시도한다 — 이미 만탄·재장전 중·예비 탄약 0이면 실패.
        /// 성공 시 로컬 선반영으로 재장전이 시작되며, 채울 발수는 min(빈 약실, 예비량)이다.
        /// </summary>
        public bool TryStartReload(int roundsAvailable)
        {
            if (IsReloading || RoundsLoaded >= _capacity)
            {
                return false;
            }

            int pending = Mathf.Min(_capacity - RoundsLoaded, roundsAvailable);
            if (pending <= 0)
            {
                return false;
            }

            _pendingLoad = pending;
            _hasConfirm = false;
            _grantedRounds = 0;
            _reloadRemaining = _reloadDuration;
            return true;
        }

        /// <summary>
        /// 호스트 차감 확정 반영 — granted가 요청보다 작으면 그만큼만 장전되고, 0 이하면 재장전이 취소된다.
        /// 시간이 이미 지났으면 즉시 완료된다.
        /// </summary>
        public void ConfirmPendingLoad(int granted)
        {
            if (_pendingLoad <= 0)
            {
                return;
            }

            if (granted <= 0)
            {
                // 호스트가 차감을 거부(예비 소진) — 재장전 취소.
                _pendingLoad = 0;
                _hasConfirm = false;
                _reloadRemaining = 0f;
                return;
            }

            _grantedRounds = granted;
            _hasConfirm = true;
            TryCompleteReload();
        }

        private void TryCompleteReload()
        {
            if (_pendingLoad <= 0 || _reloadRemaining > 0f || !_hasConfirm)
            {
                return;
            }

            RoundsLoaded = Mathf.Min(_capacity, RoundsLoaded + Mathf.Min(_grantedRounds, _pendingLoad));
            _pendingLoad = 0;
            _hasConfirm = false;
            _grantedRounds = 0;
        }
    }
}
