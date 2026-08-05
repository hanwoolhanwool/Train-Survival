using Game.Core.Events;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 근접 무기(마체테) — 총기와 같은 권위 파이프라인의 최소형 (기획서 §6.2, M5 2차):
    /// 소유자 로컬 스피어캐스트 판정(지연 0) → 호스트 보고 → 호스트가 거리 재검증 후 데미지 확정.
    /// 탄약·재장전·트레이서가 없다 — 무한 사용이되 리치가 짧아 위험이 곧 비용이다.
    /// </summary>
    public sealed class MeleeWeaponController : NetworkBehaviour
    {
        [SerializeField] private MeleeSettings _settings;
        [SerializeField] private Transform _aimSource;

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
            if (WeaponRaycast.TryGetClosestSphereHit(
                    aimOrigin, _settings.HitRadius, aimForward, _settings.MaxRange, transform.root,
                    out RaycastHit hit))
            {
                targetObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (targetObject != null)
                {
                    IDamageable candidate = targetObject.GetComponent<IDamageable>();
                    if (candidate != null && candidate.IsAlive)
                    {
                        target = candidate;
                        hitPoint = hit.point;
                    }
                }
            }

            // 스윙 연출은 입력 즉시 로컬 발행 (지연 0).
            EventBus<WeaponFiredLocalEvent>.Publish(
                new WeaponFiredLocalEvent(HotbarItemType.Melee, target != null));

            if (target != null)
            {
                ReportHitServerRpc(targetObject, swingPosition, hitPoint);
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
    }
}
