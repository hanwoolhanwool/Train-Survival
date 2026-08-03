using Game.Core.Events;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 리볼버 — 사격 파이프라인 (권위 분담표, 개발 가이드 M2):
    /// 소유자 로컬 레이캐스트 판정(지연 0) → 호스트 보고 → 호스트가 데미지·사망 확정 → 권위 이벤트.
    /// 발사음·트레이서는 입력 즉시 로컬 재생, 다른 클라이언트에는 연출 전용 브로드캐스트.
    /// 실린더 상태는 <see cref="RevolverCylinder"/>가 담당한다.
    /// 재장전(M5)은 인벤토리 예비 탄약을 소모한다 — 로컬 선반영 시작 후 호스트가 차감을 확정한다
    /// (재장전 시간 &gt; RTT라 확정 대기가 체감되지 않는다).
    /// </summary>
    public sealed class RevolverController : NetworkBehaviour
    {
        private const float TracerVisibleSeconds = 0.05f;

        [SerializeField] private RevolverSettings _settings;
        [SerializeField] private Transform _aimSource;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private LineRenderer _tracer;

        private RevolverCylinder _cylinder;
        private IResourceInventory _inventory;
        private float _tracerHideTime;
        private int _lastPublishedRounds = -1;
        private bool _lastPublishedReloading;
        private int _lastPublishedReserve = -1;

        /// <summary>무기 슬롯 활성 여부 — <see cref="PlayerWeaponLoadout"/>이 제어한다. 소유자 입력 게이트.</summary>
        public bool InputEnabled { get; set; }

        public override void OnNetworkSpawn()
        {
            if (_settings != null)
            {
                _cylinder = new RevolverCylinder(
                    _settings.CylinderCapacity, _settings.FireInterval, _settings.ReloadDuration);
            }

            _inventory = GetComponent<IResourceInventory>();
            _lastPublishedRounds = -1;
            _lastPublishedReserve = -1;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            UpdateTracer();

            if (!IsOwner || _cylinder == null)
            {
                return;
            }

            _cylinder.Tick(Time.deltaTime);

            if (InputEnabled)
            {
                UpdateOwnerInput();
            }

            PublishAmmoIfChanged();
        }

        // ── 소유자: 입력·로컬 판정 계층 ────────────────────────────────────

        private void UpdateOwnerInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (_cylinder.TryFire())
                {
                    Fire();
                }
                else if (_cylinder.RoundsLoaded <= 0)
                {
                    // 빈 실린더 격발 → 자동 재장전 (기본 화기의 조작 단순화).
                    TryReload();
                }
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                TryReload();
            }
        }

        /// <summary>
        /// 재장전 — 예비 탄약(복제된 인벤토리 값)을 로컬에서 읽어 즉시 선반영으로 시작하고,
        /// 호스트에 차감을 요청한다. 확정 발수는 <see cref="ConfirmReloadOwnerRpc"/>로 돌아온다.
        /// </summary>
        private void TryReload()
        {
            int reserve = GetReserveRounds();
            if (_cylinder.TryStartReload(reserve))
            {
                RequestReloadServerRpc(_cylinder.PendingLoad);
            }
        }

        private int GetReserveRounds()
        {
            return _inventory != null && _settings != null ? _inventory.CountOf(_settings.AmmoType) : 0;
        }

        private void Fire()
        {
            Vector3 aimOrigin = _aimSource != null ? _aimSource.position : transform.position;
            Vector3 aimForward = _aimSource != null ? _aimSource.forward : transform.forward;
            Vector3 firePosition = transform.position;

            // 로컬 레이캐스트 판정 (지연 0) — 자기 몸은 제외한다.
            Vector3 endPoint = aimOrigin + aimForward * _settings.MaxRange;
            IDamageable target = null;
            NetworkObject targetObject = null;
            if (TryRaycastHit(aimOrigin, aimForward, out RaycastHit hit))
            {
                endPoint = hit.point;
                targetObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (targetObject != null)
                {
                    IDamageable candidate = targetObject.GetComponent<IDamageable>();
                    if (candidate != null && candidate.IsAlive)
                    {
                        target = candidate;
                    }
                }
            }

            // 발사 연출은 입력 즉시 로컬 발행 (지연 0).
            EventBus<RevolverFiredLocalEvent>.Publish(new RevolverFiredLocalEvent(target != null));
            ShowTracer(endPoint);

            if (target != null)
            {
                ReportHitServerRpc(targetObject, firePosition, hit.point);
            }

            // 다른 클라이언트에게 발사 모습을 보여준다 (연출 전용, 판정에는 영향 없음).
            ReportFireServerRpc(endPoint);
        }

        private bool TryRaycastHit(Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            var ray = new Ray(origin, direction);
            RaycastHit[] hits = Physics.RaycastAll(ray, _settings.MaxRange, ~0, QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            hit = default;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].distance < bestDistance && hits[i].transform.root != transform.root)
                {
                    bestDistance = hits[i].distance;
                    hit = hits[i];
                    found = true;
                }
            }

            return found;
        }

        private void PublishAmmoIfChanged()
        {
            int reserve = GetReserveRounds();
            if (_cylinder.RoundsLoaded == _lastPublishedRounds &&
                _cylinder.IsReloading == _lastPublishedReloading &&
                reserve == _lastPublishedReserve)
            {
                return;
            }

            _lastPublishedRounds = _cylinder.RoundsLoaded;
            _lastPublishedReloading = _cylinder.IsReloading;
            _lastPublishedReserve = reserve;
            EventBus<RevolverAmmoChangedLocalEvent>.Publish(new RevolverAmmoChangedLocalEvent(
                _cylinder.RoundsLoaded, _cylinder.Capacity, _cylinder.IsReloading, reserve));
        }

        // ── 호스트: 재장전 차감 확정 ───────────────────────────────────────

        [Rpc(SendTo.Server)]
        private void RequestReloadServerRpc(int requestedRounds, RpcParams rpcParams = default)
        {
            IResourceInventory inventory = GetComponent<IResourceInventory>();
            if (_settings == null || inventory == null || requestedRounds <= 0)
            {
                ConfirmReloadOwnerRpc(0);
                return;
            }

            // 실보유 기준으로 요청을 깎아 차감한다 — 소유자 선반영과 서버 상태가 어긋나도 초과 지급이 없다.
            int granted = Mathf.Min(Mathf.Min(requestedRounds, _settings.CylinderCapacity),
                inventory.CountOf(_settings.AmmoType));
            if (granted <= 0 || !inventory.ServerTryRemove(_settings.AmmoType, granted))
            {
                ConfirmReloadOwnerRpc(0);
                return;
            }

            ConfirmReloadOwnerRpc(granted);
        }

        [Rpc(SendTo.Owner)]
        private void ConfirmReloadOwnerRpc(int grantedRounds)
        {
            _cylinder?.ConfirmPendingLoad(grantedRounds);
        }

        // ── 호스트: 권위 계층 (명중 검증·데미지 확정) ──────────────────────

        [Rpc(SendTo.Server)]
        private void ReportHitServerRpc(
            NetworkObjectReference targetRef, Vector3 firePosition, Vector3 hitPoint,
            RpcParams rpcParams = default)
        {
            if (!targetRef.TryGet(out NetworkObject targetObject))
            {
                return;
            }

            IDamageable damageable = targetObject.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                return;
            }

            // 거리 검증 — 보고된 발사 지점 기준 사거리 초과 보고는 기각한다 (호스트 검증 원칙).
            float maxDistance = _settings.MaxRange + _settings.RangeTolerance;
            if ((hitPoint - firePosition).sqrMagnitude > maxDistance * maxDistance)
            {
                Debug.Log($"[RevolverController] 명중 보고 기각(사거리 초과): client={rpcParams.Receive.SenderClientId}");
                return;
            }

            damageable.ApplyDamage(_settings.Damage, rpcParams.Receive.SenderClientId);
        }

        // ── 비소유 클라이언트: 발사 연출 브로드캐스트 (판정에 영향 없음) ────

        [Rpc(SendTo.Server)]
        private void ReportFireServerRpc(Vector3 endPoint)
        {
            PlayRemoteFireRpc(endPoint);
        }

        [Rpc(SendTo.NotOwner)]
        private void PlayRemoteFireRpc(Vector3 endPoint)
        {
            ShowTracer(endPoint);
        }

        private void ShowTracer(Vector3 endPoint)
        {
            if (_tracer == null)
            {
                return;
            }

            Vector3 start = _muzzle != null ? _muzzle.position : transform.position;
            _tracer.positionCount = 2;
            _tracer.SetPosition(0, start);
            _tracer.SetPosition(1, endPoint);
            _tracer.enabled = true;
            _tracerHideTime = Time.time + TracerVisibleSeconds;
        }

        private void UpdateTracer()
        {
            if (_tracer != null && _tracer.enabled && Time.time >= _tracerHideTime)
            {
                _tracer.enabled = false;
            }
        }
    }
}
