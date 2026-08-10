using Game.Core.Pooling;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 타격점 이펙트 (M5 8차 — 전투 연출) — 풀링 비네트워크 파티클 버스트.
    /// 탄착·근접 타격·사망·바퀴 분쇄가 같은 컴포넌트를 프리팹 설정만 바꿔 공유한다.
    /// 파티클 모듈은 Awake에서 코드로 구성한다 — 프리팹에는 빈 ParticleSystem만 두면 된다.
    /// 수명이 다하면 스스로 풀로 돌아간다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ImpactEffectView : MonoBehaviour, IPoolable
    {
        [Tooltip("버스트 파티클 수 — 분쇄 강조 프리팹은 크게 잡는다.")]
        [SerializeField, Min(1)] private int _burstCount = 8;

        [Tooltip("연출 총 수명 (초) — 지나면 풀로 돌아간다.")]
        [SerializeField, Min(0.05f)] private float _lifetimeSeconds = 0.6f;

        [SerializeField] private Color _color = new Color(1f, 0.85f, 0.4f, 1f);

        [SerializeField, Min(0.01f)] private float _particleSize = 0.08f;

        [SerializeField, Min(0f)] private float _particleSpeed = 3f;

        private ParticleSystem _particles;
        private float _despawnTime;
        private bool _active;

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = _lifetimeSeconds * 0.8f;
            main.startSpeed = _particleSpeed;
            main.startSize = _particleSize;
            main.startColor = _color;

            // 주기 방출 없음 — Play()의 Emit 버스트만 쓴다.
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;
        }

        /// <summary>지정 위치에 버스트를 재생한다 — normal은 튀는 방향 힌트 (0이면 무방향).</summary>
        public void Play(Vector3 position, Vector3 normal)
        {
            transform.position = position;
            transform.rotation = normal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            _particles.Emit(_burstCount);
            _despawnTime = Time.time + _lifetimeSeconds;
            _active = true;
        }

        private void Update()
        {
            if (_active && Time.time >= _despawnTime)
            {
                _active = false;
                PoolManager.Despawn(gameObject);
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _active = false;
            if (_particles != null)
            {
                _particles.Clear();
            }
        }
    }
}
