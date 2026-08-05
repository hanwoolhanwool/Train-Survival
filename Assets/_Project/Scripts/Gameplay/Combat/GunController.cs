using System.Collections.Generic;
using Game.Core.Events;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 총기 공통 컨트롤러 — 사격 파이프라인 (권위 분담표, 개발 가이드 M2):
    /// 소유자 로컬 레이캐스트 판정(지연 0) → 호스트 보고 → 호스트가 데미지·사망 확정 → 권위 이벤트.
    /// 발사음·트레이서는 입력 즉시 로컬 재생, 다른 클라이언트에는 연출 전용 브로드캐스트.
    /// 리볼버·샷건·볼트액션이 이 한 컴포넌트를 <see cref="GunSettings"/> 에셋만 바꿔 공유한다 (M5 2차).
    /// 샷건(다펠릿)은 펠릿별 판정을 대상별로 집계해 보고한다 — RPC 수는 맞은 대상 수만큼.
    /// 재장전은 인벤토리 예비 탄약을 소모한다 — 로컬 선반영 시작 후 호스트가 차감을 확정한다
    /// (재장전 시간 &gt; RTT라 확정 대기가 체감되지 않는다). 장탄 상태는 <see cref="GunMagazine"/>이 담당한다.
    /// </summary>
    public sealed class GunController : NetworkBehaviour
    {
        private const float TracerVisibleSeconds = 0.05f;

        [SerializeField] private GunSettings _settings;
        [SerializeField] private Transform _aimSource;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private LineRenderer _tracer;

        // 한 발사의 펠릿 명중을 대상별로 모은다 — 발사 시에만 쓰는 작업 버퍼.
        private struct PelletGroup
        {
            public NetworkObject Target;
            public Vector3 HitPoint;
            public int Count;
        }

        private readonly List<PelletGroup> _pelletGroups = new List<PelletGroup>(4);

        private GunMagazine _magazine;
        private IResourceInventory _inventory;
        private float _tracerHideTime;
        private int _lastPublishedRounds = -1;
        private bool _lastPublishedReloading;
        private int _lastPublishedReserve = -1;

        /// <summary>무기 슬롯 활성 여부 — <see cref="HotbarController"/>가 선택 슬롯 기준으로 제어한다.</summary>
        public bool InputEnabled { get; set; }

        /// <summary>이 총이 차지하는 핫바 아이템 종류 — 핫바 게이트가 조회한다.</summary>
        public HotbarItemType WeaponItem => _settings != null ? _settings.WeaponItem : HotbarItemType.None;

        public override void OnNetworkSpawn()
        {
            if (_settings != null)
            {
                _magazine = new GunMagazine(
                    _settings.MagazineCapacity, _settings.FireInterval, _settings.ReloadDuration);
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

            if (!IsOwner || _magazine == null)
            {
                return;
            }

            _magazine.Tick(Time.deltaTime);

            if (InputEnabled)
            {
                UpdateOwnerInput();
                PublishAmmoIfChanged();
            }
            else
            {
                // 비활성 총은 HUD에 발행하지 않는다 — 다시 들 때 강제 재발행되도록 캐시를 비운다.
                _lastPublishedRounds = -1;
                _lastPublishedReserve = -1;
            }
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
                if (_magazine.TryFire())
                {
                    Fire();
                }
                else if (_magazine.RoundsLoaded <= 0)
                {
                    // 빈 장탄 격발 → 자동 재장전 (기본 화기의 조작 단순화).
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
            if (_magazine.TryStartReload(reserve))
            {
                RequestReloadServerRpc(_magazine.PendingLoad);
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
            int pellets = Mathf.Max(1, _settings.PelletCount);

            // 펠릿별 로컬 레이캐스트 판정 (지연 0) — 자기 몸은 제외, 명중은 대상별로 집계한다.
            _pelletGroups.Clear();
            bool hitDamageable = false;
            Vector3 tracerEnd = aimOrigin + aimForward * _settings.MaxRange;
            float tracerBestDistance = float.MaxValue;

            for (int p = 0; p < pellets; p++)
            {
                Vector3 direction = WeaponSpreadMath.ApplySpread(
                    aimForward, _settings.SpreadAngle, Random.value, Random.value);
                if (!WeaponRaycast.TryGetClosestHit(
                        aimOrigin, direction, _settings.MaxRange, transform.root, out RaycastHit hit))
                {
                    continue;
                }

                if (hit.distance < tracerBestDistance)
                {
                    tracerBestDistance = hit.distance;
                    tracerEnd = hit.point;
                }

                NetworkObject targetObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (targetObject == null)
                {
                    continue;
                }

                IDamageable candidate = targetObject.GetComponent<IDamageable>();
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                hitDamageable = true;
                AccumulatePellet(targetObject, hit.point);
            }

            // 발사 연출은 입력 즉시 로컬 발행 (지연 0).
            EventBus<WeaponFiredLocalEvent>.Publish(
                new WeaponFiredLocalEvent(WeaponItem, hitDamageable));
            ShowTracer(tracerEnd);

            for (int i = 0; i < _pelletGroups.Count; i++)
            {
                ReportHitServerRpc(
                    _pelletGroups[i].Target, firePosition, _pelletGroups[i].HitPoint, _pelletGroups[i].Count);
            }

            // 다른 클라이언트에게 발사 모습을 보여준다 (연출 전용, 판정에는 영향 없음).
            ReportFireServerRpc(tracerEnd);
        }

        private void AccumulatePellet(NetworkObject target, Vector3 hitPoint)
        {
            for (int i = 0; i < _pelletGroups.Count; i++)
            {
                if (_pelletGroups[i].Target == target)
                {
                    PelletGroup group = _pelletGroups[i];
                    group.Count += 1;
                    _pelletGroups[i] = group;
                    return;
                }
            }

            _pelletGroups.Add(new PelletGroup { Target = target, HitPoint = hitPoint, Count = 1 });
        }

        private void PublishAmmoIfChanged()
        {
            int reserve = GetReserveRounds();
            if (_magazine.RoundsLoaded == _lastPublishedRounds &&
                _magazine.IsReloading == _lastPublishedReloading &&
                reserve == _lastPublishedReserve)
            {
                return;
            }

            _lastPublishedRounds = _magazine.RoundsLoaded;
            _lastPublishedReloading = _magazine.IsReloading;
            _lastPublishedReserve = reserve;
            EventBus<WeaponAmmoChangedLocalEvent>.Publish(new WeaponAmmoChangedLocalEvent(
                WeaponItem, _settings.DisplayName,
                _magazine.RoundsLoaded, _magazine.Capacity, _magazine.IsReloading, reserve));
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
            int granted = Mathf.Min(Mathf.Min(requestedRounds, _settings.MagazineCapacity),
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
            _magazine?.ConfirmPendingLoad(grantedRounds);
        }

        // ── 호스트: 권위 계층 (명중 검증·데미지 확정) ──────────────────────

        [Rpc(SendTo.Server)]
        private void ReportHitServerRpc(
            NetworkObjectReference targetRef, Vector3 firePosition, Vector3 hitPoint, int pelletHits,
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
                Debug.Log($"[GunController] 명중 보고 기각(사거리 초과): client={rpcParams.Receive.SenderClientId}");
                return;
            }

            // 펠릿 수 검증 — 세팅의 펠릿 상한을 넘는 보고는 상한으로 깎는다 (과대 데미지 방어).
            int pellets = Mathf.Clamp(pelletHits, 1, Mathf.Max(1, _settings.PelletCount));
            damageable.ApplyDamage(_settings.Damage * pellets, rpcParams.Receive.SenderClientId);
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
