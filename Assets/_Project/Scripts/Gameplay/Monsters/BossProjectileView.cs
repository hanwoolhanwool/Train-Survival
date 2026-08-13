using Game.Core.Pooling;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 보스 투사체의 표시 전용 재생기 (M7 2차 결정 ②-b) — <b>비네트워크·풀링</b> 오브젝트다.
    /// 탄체를 복제하지 않는 것이 설계의 핵심이라(대역폭 0 증분), 호스트가 보낸 발사 파라미터로
    /// 각 피어가 <see cref="BossProjectileMath"/>의 같은 궤적을 로컬 재생한다. 판정은 호스트가
    /// 낙하 시점에 한 번만 하며 이 컴포넌트는 판정에 관여하지 않는다.
    /// 낙점 예고 링은 비행 내내 지면에 떠 있어 "어디로 떨어지는지"를 보여 준다.
    /// </summary>
    public sealed class BossProjectileView : MonoBehaviour, IPoolable
    {
        [Tooltip("탄체 표현 — 궤적을 따라 움직인다. 비면 루트가 직접 움직인다.")]
        [SerializeField] private Transform _body;

        [Tooltip("낙점 예고 링 — 낙점 지면에 놓이고 반경에 맞춰 스케일된다. 비면 예고 없음.")]
        [SerializeField] private Transform _impactRing;

        [Tooltip("낙점 링이 지면에서 떠 있는 높이 (z-fighting 방지).")]
        [SerializeField, Min(0f)] private float _ringGroundOffset = 0.05f;

        [Tooltip("낙하 후 링이 남아 있는 시간 (초) — 착탄 위치를 눈으로 확인할 여유.")]
        [SerializeField, Min(0f)] private float _lingerSeconds = 0.25f;

        private Vector3 _origin;
        private Vector3 _launchVelocity;
        private float _flightSeconds;
        private float _elapsed;
        private bool _active;

        /// <summary>발사 파라미터로 궤적 재생을 시작한다 — 각 피어가 같은 입력에서 같은 궤적을 그린다.</summary>
        public void Play(Vector3 origin, Vector3 impact, float flightSeconds, float impactRadius)
        {
            _origin = origin;
            _flightSeconds = Mathf.Max(0.01f, flightSeconds);
            _launchVelocity = BossProjectileMath.ComputeLaunchVelocity(
                origin, impact, _flightSeconds, BossProjectileMath.Gravity);
            _elapsed = 0f;
            _active = true;

            transform.position = origin;
            if (_body != null)
            {
                _body.position = origin;
            }

            if (_impactRing != null)
            {
                _impactRing.gameObject.SetActive(true);
                _impactRing.position = new Vector3(impact.x, impact.y + _ringGroundOffset, impact.z);
                _impactRing.rotation = Quaternion.identity;

                float diameter = Mathf.Max(0.1f, impactRadius) * 2f;
                _impactRing.localScale = new Vector3(diameter, _impactRing.localScale.y, diameter);
            }
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            _elapsed += Time.deltaTime;

            if (_elapsed < _flightSeconds)
            {
                Vector3 position = BossProjectileMath.EvaluatePosition(
                    _origin, _launchVelocity, _elapsed, BossProjectileMath.Gravity);

                if (_body != null)
                {
                    _body.position = position;
                }
                else
                {
                    transform.position = position;
                }

                return;
            }

            // 낙하 완료 — 탄체를 감추고 링만 잠깐 남긴 뒤 풀로 돌아간다.
            if (_body != null && _body.gameObject.activeSelf)
            {
                _body.gameObject.SetActive(false);
            }

            if (_elapsed >= _flightSeconds + _lingerSeconds)
            {
                _active = false;
                PoolManager.Despawn(gameObject);
            }
        }

        public void OnSpawned()
        {
            if (_body != null)
            {
                _body.gameObject.SetActive(true);
            }
        }

        public void OnDespawned()
        {
            _active = false;
            _elapsed = 0f;

            if (_impactRing != null)
            {
                _impactRing.gameObject.SetActive(false);
            }
        }
    }
}
