using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역 하늘의 소유자 (레벨 3차 · 미결 ② <b>B안 — 슬롯은 지역, 프로퍼티는 낮/밤 연출</b>).
    /// <see cref="RegionChangedEvent"/>를 받아 그 지역의 스카이박스 복제본을
    /// <see cref="RenderSettings.skybox"/>에 건다.
    ///
    /// <para>
    /// <b>복제본에만 쓴다.</b> 원본 에셋을 그대로 걸면 낮/밤 연출이 그 위에 색을 써서
    /// 에디터 세션 내내 값이 남고, 같은 머티리얼을 쓰는 다른 씬까지 물든다.
    /// (풀링 규약의 대상은 GameObject 스폰이며, 머티리얼 인스턴스는 여기서 직접 관리한다.)
    /// </para>
    ///
    /// <para>
    /// <b>순수 로컬 표현이다</b> — <c>NetworkBehaviour</c>가 아니고 네트워크 상태를 만들지 않는다.
    /// 지역 인덱스는 이미 복제된 값이라 전 피어가 같은 하늘을 본다.
    /// </para>
    ///
    /// <para>
    /// <b>fog는 건드리지 않는다</b> — <see cref="WeatherVisualController"/> 단독 소유(M8 결정 ② ㉮).
    /// 이 컴포넌트가 쓰는 것은 하늘 <b>슬롯 하나</b>뿐이다.
    /// </para>
    ///
    /// Game 씬에 1개 배치한다. 지역에 하늘이 지정돼 있지 않으면 아무것도 하지 않는다 — 회귀 방어선.
    /// </summary>
    public sealed class RegionSkyController : MonoBehaviour, IRegionSkyProvider
    {
        [Tooltip("지역에 하늘이 지정돼 있지 않을 때 쓸 하늘. 비우면 그 지역에서는 슬롯을 건드리지 않는다.")]
        [SerializeField] private Material _fallbackSkybox;

        private Material _instance;
        private Material _instanceSource;
        private Material _sceneSkybox;
        private bool _hasBackup;

        /// <inheritdoc />
        public Material CurrentSky => _instance;

        private void OnEnable()
        {
            if (!_hasBackup)
            {
                _sceneSkybox = RenderSettings.skybox;
                _hasBackup = true;
            }

            if (!ServiceLocator.IsRegistered<IRegionSkyProvider>())
            {
                ServiceLocator.Register<IRegionSkyProvider>(this);
            }

            EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);

            // 늦게 켜졌어도 진행 중인 지역을 반영한다 (날씨 연출과 같은 규약).
            if (ServiceLocator.TryGet(out IRegionService region))
            {
                Apply(region.CurrentRegion);
            }
        }

        private void OnDisable()
        {
            EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);

            if (ServiceLocator.TryGet(out IRegionSkyProvider provider) && ReferenceEquals(provider, this))
            {
                ServiceLocator.Unregister<IRegionSkyProvider>();
            }

            Release();
        }

        private void OnRegionChanged(RegionChangedEvent evt)
        {
            Apply(evt.Region);
        }

        private void Apply(RegionDefinition region)
        {
            Material source = region != null && region.SkyboxMaterial != null
                ? region.SkyboxMaterial
                : _fallbackSkybox;

            if (source == null)
            {
                // 이 지역엔 하늘이 없다 — 슬롯을 비우지 말고 놓아 준다(씬 기본값·낮밤 연출 복제본 유지).
                Release();
                return;
            }

            if (_instance != null && _instanceSource == source)
            {
                // 같은 하늘이다. 복제본을 다시 만들면 낮/밤 연출이 써 둔 색이 초기화된다.
                if (RenderSettings.skybox != _instance)
                {
                    RenderSettings.skybox = _instance;
                }

                return;
            }

            DestroyInstance();

            _instance = new Material(source);
            _instanceSource = source;
            RenderSettings.skybox = _instance;

            GameLog.Info(LogCategory.Cycle, $"지역 하늘 적용 — {source.name}");
        }

        /// <summary>지역 하늘을 놓는다. <b>내가 건 복제본이 아직 슬롯에 있을 때만</b> 씬 기본값으로 되돌린다.</summary>
        private void Release()
        {
            if (_instance == null)
            {
                return;
            }

            if (_hasBackup && RenderSettings.skybox == _instance)
            {
                RenderSettings.skybox = _sceneSkybox;
            }

            DestroyInstance();
        }

        private void DestroyInstance()
        {
            if (_instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_instance);
            }
            else
            {
                DestroyImmediate(_instance);
            }

            _instance = null;
            _instanceSource = null;
        }
    }
}
