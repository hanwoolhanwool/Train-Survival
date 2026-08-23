using Game.Core.Events;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 화구 화염 표현 (화구 연료구 교체 계획 §3.4) — 연료 잔량에 따라 상시 타오르고,
    /// 투입이 성사되면 순간적으로 치솟았다 잦아든다.
    /// <para>
    /// 표현 전용이다 — 게임 상태를 읽지도 바꾸지도 않고 권위 이벤트만 구독한다. 세기 계산은
    /// <see cref="FireboxFlameMath"/>가 소유하고 이 클래스는 그 값을 파티클·조명에 옮기기만 한다.
    /// </para>
    /// 화구 캐비티 바닥 중앙에 배치한다. 씬에 하나뿐이고 상시 살아 있으므로 풀링하지 않는다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FireboxFlameView : MonoBehaviour
    {
        [Header("세기")]
        [Tooltip("연료가 바닥났을 때 남는 잉걸불 세기 — 0이면 완전히 꺼진다.")]
        [SerializeField, Range(0f, 1f)] private float _minIntensity = 0.15f;

        [Header("투입 버스트")]
        [Tooltip("치솟았다 잦아들기까지의 시간(초).")]
        [SerializeField, Min(0.05f)] private float _burstDuration = 0.9f;

        [Tooltip("버스트가 기본 세기에 얹는 최대치.")]
        [SerializeField, Range(0f, 1f)] private float _burstMaxPeak = 0.85f;

        [Tooltip("이 발열량이면 버스트가 최대치가 된다 — 그보다 작은 자원은 비례해 작게 탄다.")]
        [SerializeField, Min(0f)] private float _burstReferenceFuelValue = 20f;

        [Tooltip("투입 순간 한꺼번에 터뜨릴 불티 수.")]
        [SerializeField, Min(0)] private int _burstParticleCount = 24;

        [Header("파티클")]
        [Tooltip("불꽃이 솟는 바닥 면의 크기(가로 × 세로) — 화구 캐비티 안에 들어가야 한다.")]
        [SerializeField] private Vector2 _emitArea = new Vector2(0.55f, 0.6f);

        [SerializeField, Min(0f)] private float _minEmissionRate = 8f;

        [SerializeField, Min(0f)] private float _maxEmissionRate = 85f;

        [SerializeField, Min(0.01f)] private float _minParticleSize = 0.09f;

        [SerializeField, Min(0.01f)] private float _maxParticleSize = 0.3f;

        [Tooltip("불꽃이 사는 시간(초) — 속도 × 이 값이 캐비티 높이를 넘으면 화구 밖으로 샌다.")]
        [SerializeField, Min(0.05f)] private float _particleLifetime = 1.15f;

        [SerializeField, Min(0f)] private float _particleSpeed = 0.8f;

        [Tooltip("잉걸불 색 — 세기가 낮을 때.")]
        [SerializeField] private Color _emberColor = new Color(1f, 0.35f, 0.08f, 1f);

        [Tooltip("화염 색 — 세기가 높을 때.")]
        [SerializeField] private Color _flameColor = new Color(1f, 0.78f, 0.3f, 1f);

        [Header("조명 (선택)")]
        [Tooltip("화구가 주변을 밝히는 빛. 비워 두면 조명 없이 파티클만 연출한다.")]
        [SerializeField] private Light _light;

        [SerializeField, Min(0f)] private float _minLightIntensity = 3f;

        [SerializeField, Min(0f)] private float _maxLightIntensity = 14f;

        [Tooltip("불빛 흔들림 폭 (0 = 흔들리지 않음).")]
        [SerializeField, Range(0f, 0.5f)] private float _flickerAmount = 0.18f;

        [SerializeField, Min(0f)] private float _flickerSpeed = 7f;

        private ParticleSystem _particles;
        private ParticleSystem.EmissionModule _emission;
        private ParticleSystem.MainModule _main;

        private float _baseIntensity;
        private float _burstPeak;
        private float _burstElapsed;
        private float _flickerSeed;

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            _baseIntensity = _minIntensity;
            _flickerSeed = Random.value * 100f;

            ConfigureParticles();
        }

        /// <summary>
        /// 파티클 모듈을 코드로 구성한다 — 프리팹·씬에는 빈 <see cref="ParticleSystem"/>만 두면 된다
        /// (<see cref="Combat.ImpactEffectView"/>와 같은 규약).
        /// </summary>
        private void ConfigureParticles()
        {
            _main = _particles.main;
            _main.loop = true;
            _main.playOnAwake = false;
            // 수명·속도·회전을 흩뜨린다 — 같은 값으로 뿜으면 불꽃이 아니라 떠오르는 구슬로 보인다.
            _main.startLifetime = new ParticleSystem.MinMaxCurve(_particleLifetime * 0.65f, _particleLifetime);
            _main.startSpeed = new ParticleSystem.MinMaxCurve(_particleSpeed * 0.55f, _particleSpeed);
            _main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            _main.startSize = _minParticleSize;
            _main.startColor = _emberColor;
            _main.maxParticles = 400;

            // 열차 위의 것은 좌표계를 먼저 의심한다 — 이 화구에는 TrainElevationFollower가 붙어 y를 흔든다.
            // World 시뮬레이션이면 화구가 오르내릴 때 불기둥이 제자리에 남아 밖으로 새어 나온다.
            _main.simulationSpace = ParticleSystemSimulationSpace.Local;

            _emission = _particles.emission;
            _emission.enabled = true;
            _emission.rateOverTime = _minEmissionRate;

            // 캐비티 바닥 면에서 위로 솟는다 — Box는 +Z로 방출하므로 -90° 눕혀 +Y를 향하게 한다.
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_emitArea.x, _emitArea.y, 0.02f);
            shape.rotation = new Vector3(-90f, 0f, 0f);

            // 위로 갈수록 식으며 옅어진다 — 끝에서 뚝 끊기거나 색이 그대로면 불로 안 보인다.
            ParticleSystem.ColorOverLifetimeModule color = _particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.62f, 0.22f), 0.45f),
                    new GradientColorKey(new Color(0.72f, 0.18f, 0.05f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.85f, 0.7f),
                    new GradientAlphaKey(0f, 1f),
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0.4f)));

            // 불꽃은 솟는 방향으로 늘어나야 혀처럼 보인다 — 원형 빌보드로는 잉걸불에서 멈춘다.
            ParticleSystemRenderer renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2.6f;
                renderer.velocityScale = 0f;
                renderer.cameraVelocityScale = 0f;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingFudge = -5f;
            }

            _particles.Play();
        }

        private void OnEnable()
        {
            EventBus<FuelChangedEvent>.Subscribe(OnFuelChanged);
            EventBus<EngineFuelDepositedEvent>.Subscribe(OnFuelDeposited);
        }

        private void OnDisable()
        {
            EventBus<FuelChangedEvent>.Unsubscribe(OnFuelChanged);
            EventBus<EngineFuelDepositedEvent>.Unsubscribe(OnFuelDeposited);
        }

        private void OnFuelChanged(FuelChangedEvent evt)
        {
            _baseIntensity = FireboxFlameMath.ComputeBaseIntensity(evt.Fuel, evt.Capacity, _minIntensity);
        }

        private void OnFuelDeposited(EngineFuelDepositedEvent evt)
        {
            _burstPeak = FireboxFlameMath.ComputeBurstPeak(evt.FuelValue, _burstReferenceFuelValue, _burstMaxPeak);
            _burstElapsed = 0f;

            if (_burstParticleCount > 0)
            {
                _particles.Emit(_burstParticleCount);
            }
        }

        private void Update()
        {
            _burstElapsed += Time.deltaTime;

            float burst = FireboxFlameMath.ComputeBurstFactor(_burstElapsed, _burstDuration, _burstPeak);
            float intensity = FireboxFlameMath.ComposeIntensity(_baseIntensity, burst);

            _emission.rateOverTime = Mathf.Lerp(_minEmissionRate, _maxEmissionRate, intensity);

            float size = Mathf.Lerp(_minParticleSize, _maxParticleSize, intensity);
            _main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);

            // 잉걸불 색과 화염 색 사이를 오가되, 한 벌 안에서도 두 색이 섞이게 둔다.
            Color hot = Color.Lerp(_emberColor, _flameColor, intensity);
            _main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(_emberColor, hot, 0.35f), hot);

            if (_light != null)
            {
                float flicker = 1f + (Mathf.PerlinNoise(_flickerSeed + Time.time * _flickerSpeed, 0f) - 0.5f)
                    * 2f * _flickerAmount;
                _light.intensity = Mathf.Lerp(_minLightIntensity, _maxLightIntensity, intensity) * flicker;
            }
        }
    }
}
