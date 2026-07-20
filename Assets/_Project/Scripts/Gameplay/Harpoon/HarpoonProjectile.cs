using System;
using Game.Core.Pooling;
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

        private HarpoonHookMotion _motion;
        private Vector3 _direction;
        private float _speed;
        private float _radius;
        private float _maxRange;
        private float _retractSpeed;
        private float _traveled;
        private bool _collisionEnabled;
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

        /// <summary>
        /// 권위 발사 (소유자 전용) — 실제 충돌 판정을 수행하고 명중/미스 콜백을 정확히 한 번 호출한다.
        /// returnAnchor는 실패 시 되돌아갈 총구 — 살아있는 동안 매 프레임 위치를 따라간다.
        /// </summary>
        public void Launch(
            Vector3 origin, Vector3 direction, float speed, float radius, float maxRange,
            Transform returnAnchor, float retractSpeed, float impactPauseDuration, float waitingForServerTimeout,
            Action<IGrabbable, Vector3> onHit, Action onMiss)
        {
            SetupCommon(origin, direction, speed, maxRange, returnAnchor, retractSpeed, impactPauseDuration, waitingForServerTimeout);
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
            Transform returnAnchor, float retractSpeed, float impactPauseDuration, float waitingForServerTimeout)
        {
            SetupCommon(origin, direction, speed, maxRange, returnAnchor, retractSpeed, impactPauseDuration, waitingForServerTimeout);
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
            Transform returnAnchor, float retractSpeed, float impactPauseDuration, float waitingForServerTimeout)
        {
            _motion = new HarpoonHookMotion(impactPauseDuration, waitingForServerTimeout);
            _direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            _speed = speed;
            _maxRange = maxRange;
            _returnAnchor = returnAnchor;
            _retractSpeed = Mathf.Max(0.1f, retractSpeed);
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

                case HookPhase.Attached:
                    UpdateAttached();
                    break;

                case HookPhase.Retracting:
                    UpdateRetracting();
                    break;

                // WaitingForServer / ImpactPause: 제자리 대기 (Tick의 타이머가 자동 전이시킨다).
            }
        }

        private void UpdateFlying()
        {
            float step = _speed * Time.deltaTime;
            Vector3 current = transform.position;

            if (_collisionEnabled &&
                Physics.SphereCast(current, _radius, _direction, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                var grabbable = hit.collider.GetComponentInParent<IGrabbable>();
                if (grabbable != null)
                {
                    _motion.NotifyGrabbableHit();
                    _onHit?.Invoke(grabbable, hit.point);
                }
                else
                {
                    // 그랩 불가 지형·구조물 명중 = 빗나감 — 사라지지 않고 되감기로 전이한다.
                    _motion.NotifyMiss();
                    _onMiss?.Invoke();
                }

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
