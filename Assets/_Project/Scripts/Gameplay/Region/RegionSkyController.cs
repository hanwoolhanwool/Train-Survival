using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.World;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역 하늘의 소유자 (레벨 3차 · 미결 ② <b>B안 — 슬롯은 지역, 프로퍼티는 낮/밤 연출</b>).
    /// 지역의 스카이박스 복제본을 <see cref="RenderSettings.skybox"/>에 건다.
    ///
    /// <para>
    /// <b>전환은 시점이 아니라 구간이다.</b> 종전에는 <see cref="RegionChangedEvent"/>를 받는
    /// 즉시 하늘을 갈아 끼웠다. 그런데 지형은 <c>tilesAhead + 1</c>장 <b>너머</b>에서야 바뀌므로
    /// (현행 6타일 · 240 m · 전속 40초), 숲을 달리는 동안 하늘만 먼저 사막이 되고
    /// <b>뒤를 돌아보면 지나온 숲 끝이 노랬다.</b> 지금은 발밑 경계를 중심으로
    /// <see cref="_blendRange"/> m에 걸쳐 <b>지역 프로퍼티를 섞는다</b>
    /// (<see cref="RegionBlendQuery"/>) — 지형이 바뀌는 그 자리에서, 튀지 않고 넘어간다.
    /// </para>
    ///
    /// <para>
    /// <b>복제본에만 쓴다.</b> 원본 에셋을 그대로 걸면 낮/밤 연출이 그 위에 색을 써서
    /// 에디터 세션 내내 값이 남고, 같은 머티리얼을 쓰는 다른 씬까지 물든다.
    /// (풀링 규약의 대상은 GameObject 스폰이며, 머티리얼 인스턴스는 여기서 직접 관리한다.)
    /// </para>
    ///
    /// <para>
    /// <b>복제본은 셰이더가 같은 한 다시 만들지 않는다.</b> 지역 하늘이 전부
    /// <c>Train Survival/Region Sky</c> 한 셰이더라, 전환은 <b>같은 복제본 위에서 값만 섞는</b>
    /// 것으로 끝난다 — 복제본을 새로 만들면 그 위에 낮/밤 연출이 써 둔 노출·틴트가 초기화된다.
    /// </para>
    ///
    /// <para>
    /// <b>순수 로컬 표현이다</b> — <c>NetworkBehaviour</c>가 아니고 네트워크 상태를 만들지 않는다.
    /// 지역 인덱스도 경계 목록도 이미 복제된 값이라 전 피어가 같은 하늘을 본다.
    /// </para>
    ///
    /// <para>
    /// <b>fog는 건드리지 않는다</b> — <see cref="WeatherVisualController"/>·
    /// <see cref="RegionFogController"/>의 몫이다(M8 결정 ② ㉮). 이 컴포넌트가 쓰는 것은
    /// 하늘 <b>슬롯 하나</b>뿐이다.
    /// </para>
    ///
    /// Game 씬에 1개 배치한다. 지역에 하늘이 지정돼 있지 않으면 아무것도 하지 않는다 — 회귀 방어선.
    /// </summary>
    public sealed class RegionSkyController : MonoBehaviour, IRegionSkyProvider
    {
        [Tooltip("지역에 하늘이 지정돼 있지 않을 때 쓸 하늘. 비우면 그 지역에서는 슬롯을 건드리지 않는다.")]
        [SerializeField] private Material _fallbackSkybox;

        [Tooltip("지역 경계 앞뒤로 하늘을 섞는 거리(m). 60 m = 전속 6 m/s 기준 앞뒤 10초씩. " +
                 "0 이면 경계를 밟는 한 프레임에 통째로 바뀐다(종전 동작의 발밑 판정판).")]
        [SerializeField, Min(0f)] private float _blendRange = 60f;

        /// <summary>
        /// <b>낮/밤 연출이 소유한</b> 셰이더 프로퍼티 — 지역 전환이 건드리면 안 되는 것들.
        /// 목록의 출처는 <c>S_RegionSky.shader</c> 머리말이다(소유자별로 갈아 둔 이유가 거기 있다).
        /// 나머지 프로퍼티는 전부 지역 정체성이라 섞는다.
        /// </summary>
        private static readonly HashSet<string> DayCycleOwnedProperties = new HashSet<string>
        {
            "_SkyTint", "_GroundColor", "_AtmosphereThickness", "_Exposure",
        };

        private static readonly List<int> ColorIdBuffer = new List<int>(16);
        private static readonly List<int> FloatIdBuffer = new List<int>(16);
        private static readonly List<int> VectorIdBuffer = new List<int>(8);

        private Material _instance;
        private Material _sceneSkybox;
        private bool _hasBackup;

        // 섞기 캐시 — 같은 (from, to, blend)를 다시 쓰지 않는다.
        private Material _appliedFrom;
        private Material _appliedTo;
        private float _appliedBlend = -1f;

        // 셰이더별 프로퍼티 목록 캐시 (매 프레임 셰이더를 훑지 않기 위해).
        private Shader _cachedShader;
        private int[] _colorIds;
        private int[] _floatIds;
        private int[] _vectorIds;

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

            // 첫 프레임부터 슬롯을 쥐고 있어야 낮/밤 연출이 "지역 하늘이 없다"고 보고
            // 자기 복제본을 걸어 버리지 않는다 (SkySlotOwnership 규약).
            Refresh();
        }

        private void OnDisable()
        {
            if (ServiceLocator.TryGet(out IRegionSkyProvider provider) && ReferenceEquals(provider, this))
            {
                ServiceLocator.Unregister<IRegionSkyProvider>();
            }

            Release();
        }

        // 지역 전환이 이벤트가 아니라 <b>거리</b>로 진행하므로 매 프레임 조회한다 —
        // 조회 두 번이라 비용이 없고, 늦게 켜진 경우·이벤트를 놓친 경우가 함께 해결된다
        // (RegionFogController·SeaSurfaceView 와 같은 규약).
        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!RegionBlendQuery.TryResolve(_blendRange, out RegionDefinition from, out RegionDefinition to,
                    out float blend))
            {
                Release();
                return;
            }

            Material fromSky = ResolveSkybox(from);
            Material toSky = ResolveSkybox(to);

            if (fromSky == null && toSky == null)
            {
                // 두 지역 다 하늘이 없다 — 슬롯을 비우지 말고 놓아 준다(씬 기본값·낮밤 연출 복제본 유지).
                Release();
                return;
            }

            // 한쪽만 배선돼 있으면 섞을 상대가 없다 — 있는 쪽을 그대로 쓴다(배선 전 지역의 회귀 방어선).
            if (fromSky == null)
            {
                fromSky = toSky;
            }
            else if (toSky == null)
            {
                toSky = fromSky;
            }

            if (fromSky.shader != toSky.shader)
            {
                // 셰이더가 다르면 값을 섞을 수 없다 — 경계 정중앙에서 갈아 끼운다.
                Material picked = blend >= 0.5f ? toSky : fromSky;
                fromSky = picked;
                toSky = picked;
                blend = 0f;
            }

            Apply(fromSky, toSky, blend);
        }

        /// <summary>지역의 하늘 원본 — 지정이 없으면 폴백, 폴백도 없으면 null.</summary>
        private Material ResolveSkybox(RegionDefinition region)
        {
            if (region != null && region.SkyboxMaterial != null)
            {
                return region.SkyboxMaterial;
            }

            return _fallbackSkybox;
        }

        private void Apply(Material fromSky, Material toSky, float blend)
        {
            EnsureInstance(fromSky);

            if (RenderSettings.skybox != _instance)
            {
                RenderSettings.skybox = _instance;
            }

            bool sameTargets = ReferenceEquals(_appliedFrom, fromSky) && ReferenceEquals(_appliedTo, toSky);
            if (sameTargets && Mathf.Approximately(_appliedBlend, blend))
            {
                return;
            }

            if (!sameTargets && !ReferenceEquals(_appliedTo, toSky))
            {
                GameLog.Info(LogCategory.Cycle, ReferenceEquals(fromSky, toSky)
                    ? $"지역 하늘 적용 — {toSky.name}"
                    : $"지역 하늘 전환 — {fromSky.name} → {toSky.name}");
            }

            _appliedFrom = fromSky;
            _appliedTo = toSky;
            _appliedBlend = blend;

            BlendRegionProperties(fromSky, toSky, blend);
        }

        /// <summary>
        /// 지역 프로퍼티만 섞어 복제본에 쓴다 — 낮/밤 프로퍼티는 손대지 않는다.
        /// <see cref="Material.Lerp"/>를 쓰지 않는 이유가 이것이다(그쪽은 전부를 덮어쓴다).
        /// </summary>
        private void BlendRegionProperties(Material fromSky, Material toSky, float blend)
        {
            CachePropertyIds(fromSky.shader);

            for (int i = 0; i < _colorIds.Length; i++)
            {
                int id = _colorIds[i];
                _instance.SetColor(id, Color.Lerp(fromSky.GetColor(id), toSky.GetColor(id), blend));
            }

            for (int i = 0; i < _floatIds.Length; i++)
            {
                int id = _floatIds[i];
                _instance.SetFloat(id, Mathf.Lerp(fromSky.GetFloat(id), toSky.GetFloat(id), blend));
            }

            for (int i = 0; i < _vectorIds.Length; i++)
            {
                int id = _vectorIds[i];
                _instance.SetVector(id, Vector4.Lerp(fromSky.GetVector(id), toSky.GetVector(id), blend));
            }
        }

        /// <summary>
        /// 셰이더에서 <b>섞을 프로퍼티</b>의 ID를 한 번만 훑어 캐시한다.
        /// 텍스처·정수는 섞을 수 없어 뺀다 — 지역 하늘은 둘 다 쓰지 않는다.
        /// </summary>
        private void CachePropertyIds(Shader shader)
        {
            if (ReferenceEquals(_cachedShader, shader) && _colorIds != null)
            {
                return;
            }

            ColorIdBuffer.Clear();
            FloatIdBuffer.Clear();
            VectorIdBuffer.Clear();

            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (DayCycleOwnedProperties.Contains(shader.GetPropertyName(i)))
                {
                    continue;
                }

                int id = shader.GetPropertyNameId(i);
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        ColorIdBuffer.Add(id);
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        FloatIdBuffer.Add(id);
                        break;
                    case ShaderPropertyType.Vector:
                        VectorIdBuffer.Add(id);
                        break;
                }
            }

            _cachedShader = shader;
            _colorIds = ColorIdBuffer.ToArray();
            _floatIds = FloatIdBuffer.ToArray();
            _vectorIds = VectorIdBuffer.ToArray();
        }

        /// <summary>
        /// 복제본을 확보한다 — <b>셰이더가 같으면 다시 만들지 않는다.</b> 새로 만들면 그 위에
        /// 낮/밤 연출이 써 둔 노출·틴트가 초기화돼, 지역이 바뀔 때마다 하늘 밝기가 한 번 튄다.
        /// </summary>
        private void EnsureInstance(Material source)
        {
            if (_instance != null && _instance.shader == source.shader)
            {
                return;
            }

            DestroyInstance();
            _instance = new Material(source);
            _appliedFrom = null;
            _appliedTo = null;
            _appliedBlend = -1f;
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
            _appliedFrom = null;
            _appliedTo = null;
            _appliedBlend = -1f;
        }
    }
}
