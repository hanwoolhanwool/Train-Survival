using System;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게 훅 — 발사·명중 판정을 로컬로 수행하는 권위체(소유자) 겸, 다른 클라이언트에게는
    /// 순수 연출용 사본(<see cref="LaunchCosmetic"/>)으로도 쓰이는 공용 프리팹 (네트워크 오브젝트 아님).
    /// 실제 이동·충돌은 이 컴포넌트가, 단계 전이 규칙은 <see cref="HarpoonHookMotion"/>이 담당한다.
    /// 명중 판정은 쏜 클라이언트가 로컬로 수행한다 (권위 분담표 — 사격과 동일 원칙).
    /// 빗나가거나 호스트가 거부해도 즉시 사라지지 않고 총구로 되돌아가는 실패 연출을 재생한다.
    /// </summary>
    public sealed class HarpoonProjectile : MonoBehaviour, IPoolable
    {
        private const float ArriveEpsilon = 0.1f;

        private static readonly Collider[] OverlapBuffer = new Collider[8];

        private HarpoonHookMotion _motion;
        private Vector3 _direction;
        private float _speed;
        private float _radius;
        private float _maxRange;
        private float _retractSpeed;
        private float _traveled;
        private bool _collisionEnabled;
        private bool _scrollWithWorld;
        private Transform _ignoreRoot;
        private Transform _returnAnchor;
        private Transform _attachTarget;
        private Action<IGrabbable, Vector3> _onHit;
        private Action _onMiss;

        /// <summary>훅이 살아 있는(풀에 반환되지 않은) 상태인지.</summary>
        public bool IsAlive => _motion != null && _motion.Phase != HookPhase.Idle;

        /// <summary>호스트 확정을 기다리는 중인지 — 탄성(늘어남) 연출 구간.</summary>
        public bool IsWaitingForServer => _motion != null && _motion.Phase == HookPhase.WaitingForServer;

        /// <summary>실패 연출(피격 정지·되감기) 구간인지 — 로프 실패 색상 표시에 사용.</summary>
        public bool IsFailing => _motion != null &&
            (_motion.Phase == HookPhase.ImpactPause || _motion.Phase == HookPhase.Retracting);

        /// <summary>대상에 부착되어 견인을 따라가는 중인지 — Q3 견인 샘플 계측 구간 판별에 사용.</summary>
        public bool IsAttached => _motion != null && _motion.Phase == HookPhase.Attached;

        /// <summary>부착 중인 대상 (없으면 null) — 파지 중 투척 선반영이 대상을 찾을 때 쓴다 (M5 6차 2차).</summary>
        public Transform AttachTarget => _attachTarget;

        /// <summary>
        /// 권위 발사 (소유자 전용) — 실제 충돌 판정을 수행하고 명중/미스 콜백을 정확히 한 번 호출한다.
        /// returnAnchor는 실패 시 되돌아갈 총구 — 살아있는 동안 매 프레임 위치를 따라간다.
        /// ignoreRoot는 사수 자신의 루트 — 겹침 검사에서 자기 콜라이더를 제외한다.
        /// scrollWithWorld가 true면 발사 시점 사수가 지상(월드 프레임)에 있었다는 뜻 — 훅도 컨베이어를
        /// 따라 함께 밀려 지면 위 대상과 같은 프레임에서 움직인다 (열차 위 발사면 false).
        /// </summary>
        public void Launch(
            Vector3 origin, Vector3 direction, float speed, float radius, float maxRange,
            Transform ignoreRoot, Transform returnAnchor,
            float retractSpeed, float impactPauseDuration, float waitingForServerTimeout, bool scrollWithWorld,
            Action<IGrabbable, Vector3> onHit, Action onMiss)
        {
            SetupCommon(origin, direction, speed, maxRange, ignoreRoot, returnAnchor, retractSpeed, impactPauseDuration, waitingForServerTimeout, scrollWithWorld);
            _radius = radius;
            _collisionEnabled = true;
            _onHit = onHit;
            _onMiss = onMiss;
        }

        /// <summary>
        /// 연출 전용 발사 (비소유 클라이언트) — 다른 플레이어의 발사를 눈으로 볼 수 있게 하는 사본.
        /// 동일한 충돌 판정을 로컬로 재현하되, 결과가 게임 상태에 영향을 주지 않는다.
        /// </summary>
        public void LaunchCosmetic(
            Vector3 origin, Vector3 direction, float speed, float radius, float maxRange,
            Transform ignoreRoot, Transform returnAnchor,
            float retractSpeed, float impactPauseDuration, float waitingForServerTimeout, bool scrollWithWorld)
        {
            SetupCommon(origin, direction, speed, maxRange, ignoreRoot, returnAnchor, retractSpeed, impactPauseDuration, waitingForServerTimeout, scrollWithWorld);
            _radius = radius;
            // 실제 발사와 동일하게 충돌 판정을 로컬 재현한다 — 벽에 막히는 타이밍이 사수가 보는 것과 일치한다.
            // 그랩 가능 대상 명중 시에는 콜백 없이 WaitingForServer로만 전이하고, 실제 결과는 뒤따르는
            // GrabApproved/RejectedNotOwnerRpc가 덮어써 확정한다 (판정 자체는 항상 서버 권위).
            _collisionEnabled = true;
            _onHit = null;
            _onMiss = null;
        }

        private void SetupCommon(
            Vector3 origin, Vector3 direction, float speed, float maxRange,
            Transform ignoreRoot, Transform returnAnchor,
            float retractSpeed, float impactPauseDuration, float waitingForServerTimeout, bool scrollWithWorld)
        {
            _motion = new HarpoonHookMotion(impactPauseDuration, waitingForServerTimeout);
            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            _speed = speed;
            _maxRange = maxRange;
            _ignoreRoot = ignoreRoot;
            _returnAnchor = returnAnchor;
            _retractSpeed = Mathf.Max(0.1f, retractSpeed);
            _scrollWithWorld = scrollWithWorld;
            _traveled = 0f;
            _attachTarget = null;
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(_direction));
            _motion.StartFlying();
        }

        /// <summary>호스트 승인 — 대상에 부착되어 견인을 시각적으로 따라간다 (§2.4 "집게손").</summary>
        public void AttachTo(Transform target)
        {
            if (_motion == null)
            {
                return;
            }

            _attachTarget = target;
            _motion.Attach();
        }

        /// <summary>즉시 되감기 시작 — 호스트 거부·대상 강제 해제 시 사용 (피격 정지 없이 바로 시작).</summary>
        public void BeginRetract()
        {
            _motion?.BeginRetract();
        }

        /// <summary>즉시 소멸 — 획득 완료·취소·정리 시 사용 (되감기 연출 없이 즉시 회수).</summary>
        public void Cancel()
        {
            if (_motion == null || _motion.Phase == HookPhase.Idle)
            {
                return;
            }

            _motion.Cancel();
            ClearCallbacks();
            PoolManager.Despawn(gameObject);
        }

        private void Update()
        {
            if (_motion == null || _motion.Phase == HookPhase.Idle)
            {
                return;
            }

            _motion.Tick(Time.deltaTime);

            switch (_motion.Phase)
            {
                case HookPhase.Flying:
                    UpdateFlying();
                    break;

                case HookPhase.Retracting:
                    UpdateRetracting();
                    break;

                // Attached: LateUpdate에서 추종한다 — 파지 부착(몬스터 LateUpdate, 실행 순서 −10)이
                // 먼저 대상을 옮긴 뒤 따라가야 훅이 한 프레임 늦지 않는다.
                // WaitingForServer / ImpactPause: 제자리 대기 (Tick의 타이머가 자동 전이시킨다).
            }

            // 지상 발사분(scrollWithWorld)은 컨베이어 프레임에 실려 함께 밀린다 — 지면·대상과 같은
            // 프레임에서 움직여야 발사 후 목표에서 어긋나지 않는다 (열차 위 발사분은 밀림 없음).
            if (_scrollWithWorld)
            {
                ApplyWorldScroll();
            }
        }

        private void ApplyWorldScroll()
        {
            // 부착(대상 추종)·되감기(총구 추종)는 이미 컨베이어에 실린 대상을 따라가므로 이중 적용을 피한다.
            // 스스로 위치를 관리하는 Flying / WaitingForServer / ImpactPause 단계에서만 밀어준다.
            HookPhase phase = _motion.Phase;
            if (phase == HookPhase.Attached || phase == HookPhase.Retracting || phase == HookPhase.Idle)
            {
                return;
            }

            if (ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                transform.position += Vector3.back * (scroll.ScrollSpeed * Time.deltaTime);
            }
        }

        private void UpdateFlying()
        {
            float step = _speed * Time.deltaTime;
            Vector3 current = transform.position;

            // SphereCast는 시작 시점에 이미 겹친 콜라이더를 무시하므로, 컨베이어로 물체가
            // 프레임 사이에 훅 위치까지 이동해 겹친 경우를 캐스트 전에 잡는다 (§11 관통 해소).
            if (_collisionEnabled && TryGetOverlapHit(current, out Collider overlapped, out Vector3 overlapPoint))
            {
                ResolveHit(overlapped, overlapPoint);
                return;
            }

            if (_collisionEnabled &&
                Physics.SphereCast(current, _radius, _direction, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
            {
                ResolveHit(hit.collider, hit.point);
                return;
            }

            _traveled += step;
            if (_traveled >= _maxRange)
            {
                _motion.NotifyMiss();
                _onMiss?.Invoke();
                return;
            }

            transform.position = current + _direction * step;
        }

        private bool TryGetOverlapHit(Vector3 position, out Collider nearest, out Vector3 nearestPoint)
        {
            nearest = null;
            nearestPoint = position;

            int count = Physics.OverlapSphereNonAlloc(position, _radius, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = OverlapBuffer[i];
                if (_ignoreRoot != null && candidate.transform.root == _ignoreRoot)
                {
                    continue;
                }

                Vector3 point = GetClosestPointSafe(candidate, position);
                float sqr = (point - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = candidate;
                    nearestPoint = point;
                }
            }

            return nearest != null;
        }

        private static Vector3 GetClosestPointSafe(Collider collider, Vector3 position)
        {
            // 비볼록 MeshCollider는 ClosestPoint를 지원하지 않는다 — AABB 근사로 대체.
            if (collider is MeshCollider mesh && !mesh.convex)
            {
                return collider.bounds.ClosestPoint(position);
            }

            return collider.ClosestPoint(position);
        }

        private void ResolveHit(Collider collider, Vector3 hitPoint)
        {
            transform.position = hitPoint;
            var grabbable = collider.GetComponentInParent<IGrabbable>();
            if (grabbable != null)
            {
                _motion.NotifyGrabbableHit();
                _onHit?.Invoke(grabbable, hitPoint);
            }
            else
            {
                // 그랩 불가 지형·구조물 명중 = 빗나감 — 사라지지 않고 되감기로 전이한다.
                _motion.NotifyMiss();
                _onMiss?.Invoke();
            }
        }

        /// <summary>
        /// 부착 추종은 LateUpdate — 대상을 움직이는 쪽(서버 견인·파지 로컬 부착)이 이번 프레임 값을
        /// 확정한 뒤에 따라가, 훅·로프가 대상보다 한 프레임 늦게 끌리지 않게 한다 (M5 6차 2차).
        /// </summary>
        private void LateUpdate()
        {
            if (_motion == null || _motion.Phase != HookPhase.Attached)
            {
                return;
            }

            UpdateAttached();
        }

        private void UpdateAttached()
        {
            if (_attachTarget == null)
            {
                // 대상이 사라졌는데도 부착 통지를 못 받은 경우의 안전장치.
                _motion.BeginRetract();
                return;
            }

            transform.position = _attachTarget.position;
        }

        private void UpdateRetracting()
        {
            if (_returnAnchor == null)
            {
                Cancel();
                return;
            }

            Vector3 current = transform.position;
            Vector3 target = _returnAnchor.position;
            Vector3 next = Vector3.MoveTowards(current, target, _retractSpeed * Time.deltaTime);
            transform.position = next;

            Vector3 toTarget = target - next;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget);
            }

            if ((target - next).sqrMagnitude <= ArriveEpsilon * ArriveEpsilon)
            {
                // NotifyRetractArrived가 Phase를 Idle로 만들면 Cancel()의 Idle 가드에 막혀
                // 풀 반환이 누락되므로, 여기서는 직접 Despawn한다 (정리는 OnDespawned가 수행).
                _motion.NotifyRetractArrived();
                PoolManager.Despawn(gameObject);
            }
        }

        private void ClearCallbacks()
        {
            _onHit = null;
            _onMiss = null;
            _attachTarget = null;
            _returnAnchor = null;
            _ignoreRoot = null;
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _motion?.Cancel();
            ClearCallbacks();
        }
    }
}
