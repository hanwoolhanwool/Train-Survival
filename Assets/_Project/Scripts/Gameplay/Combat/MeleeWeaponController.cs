using Game.Core.Pooling;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 근접 무기(마체테) — 총기와 같은 권위 파이프라인의 최소형 (기획서 §6.2, M5 2차):
    /// 소유자 로컬 스피어캐스트 판정(지연 0) → 호스트 보고 → 호스트가 거리 재검증 후 데미지 확정.
    /// 탄약·재장전·트레이서가 없다 — 무한 사용이되 리치가 짧아 위험이 곧 비용이다.
    /// 스윙 표현(호 궤적 트윈)·타격 이펙트는 입력 즉시 로컬 재생하고, 원격에는 총기의
    /// ReportFire → PlayRemoteFire 규약과 같은 형태로 중계한다 (M5 8차 — 표시 전용).
    /// </summary>
    public sealed class MeleeWeaponController : NetworkBehaviour
    {
        [SerializeField] private MeleeSettings _settings;
        [SerializeField] private Transform _aimSource;

        [Tooltip("스윙 표현 피벗 (M5 8차) — 비면 스윙을 그리지 않는다.")]
        [SerializeField] private MeleeSwingView _swingView;

        private float _nextSwingTime;

        /// <summary>무기 슬롯 활성 여부 — <see cref="HotbarController"/>가 선택 슬롯 기준으로 제어한다.</summary>
        public bool InputEnabled { get; set; }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || !InputEnabled || _settings == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Time.time < _nextSwingTime)
            {
                return;
            }

            _nextSwingTime = Time.time + _settings.SwingInterval;
            Swing();
        }

        // ── 소유자: 입력·로컬 판정 계층 ────────────────────────────────────

        private void Swing()
        {
            Vector3 aimOrigin = _aimSource != null ? _aimSource.position : transform.position;
            Vector3 aimForward = _aimSource != null ? _aimSource.forward : transform.forward;
            Vector3 swingPosition = transform.position;

            // 로컬 판정 (지연 0) — 근접 스윙은 스피어캐스트로 관대하게 잡는다.
            IDamageable target = null;
            NetworkObject targetObject = null;
            Vector3 hitPoint = default;
            Vector3 hitNormal = Vector3.up;
            bool surfaceHit = WeaponRaycast.TryGetClosestSphereHit(
                aimOrigin, _settings.HitRadius, aimForward, _settings.MaxRange, transform.root,
                out RaycastHit hit);
            if (surfaceHit)
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
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

            // 스윙 표현은 입력 즉시 로컬 재생 (지연 0) — 원격에는 타격점을 실어 중계한다.
            PlaySwingCosmetics(surfaceHit, hitPoint, hitNormal);

            if (target != null)
            {
                ReportHitServerRpc(targetObject, swingPosition, hitPoint);
            }

            // 다른 클라이언트에게 스윙 모습을 보여준다 (연출 전용, 판정에는 영향 없음).
            ReportSwingServerRpc(surfaceHit, hitPoint, hitNormal);
        }

        /// <summary>스윙 코스메틱 (판정 무변) — 호 궤적 트윈 + 표면에 닿았으면 타격 이펙트.</summary>
        private void PlaySwingCosmetics(bool surfaceHit, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_swingView != null)
            {
                _swingView.PlaySwing();
            }

            if (surfaceHit && _settings != null && _settings.ImpactEffectPrefab != null)
            {
                ImpactEffectView impact = PoolManager.Spawn(
                    _settings.ImpactEffectPrefab, hitPoint, Quaternion.identity);
                impact.Play(hitPoint, hitNormal);
            }
        }

        // ── 호스트: 권위 계층 (명중 검증·데미지 확정) ──────────────────────

        [Rpc(SendTo.Server)]
        private void ReportHitServerRpc(
            NetworkObjectReference targetRef, Vector3 swingPosition, Vector3 hitPoint,
            RpcParams rpcParams = default)
        {
            if (_settings == null || !targetRef.TryGet(out NetworkObject targetObject))
            {
                return;
            }

            IDamageable damageable = targetObject.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                return;
            }

            // 거리 검증 — 근접 무기의 리치를 벗어난 보고는 기각한다 (호스트 검증 원칙).
            float maxDistance = _settings.MaxRange + _settings.RangeTolerance;
            if ((hitPoint - swingPosition).sqrMagnitude > maxDistance * maxDistance)
            {
                Debug.Log($"[MeleeWeaponController] 명중 보고 기각(리치 초과): client={rpcParams.Receive.SenderClientId}");
                return;
            }

            damageable.ApplyDamage(_settings.Damage, rpcParams.Receive.SenderClientId);
        }

        // ── 비소유 클라이언트: 스윙 연출 브로드캐스트 (판정에 영향 없음 — 총기 규약과 동일 형태) ────

        [Rpc(SendTo.Server)]
        private void ReportSwingServerRpc(bool surfaceHit, Vector3 hitPoint, Vector3 hitNormal)
        {
            PlayRemoteSwingRpc(surfaceHit, hitPoint, hitNormal);
        }

        [Rpc(SendTo.NotOwner)]
        private void PlayRemoteSwingRpc(bool surfaceHit, Vector3 hitPoint, Vector3 hitNormal)
        {
            PlaySwingCosmetics(surfaceHit, hitPoint, hitNormal);
        }
    }
}
