using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 낮/밤 연출이 건드리는 환경 상태의 스냅샷 (M8 2차). <b>떠 두고 그대로 되돌리는 것만</b> 담당한다 —
    /// 언제 소유하고 언제 놓을지는 <see cref="DayCycleVisualController"/>가 정한다.
    /// <para>
    /// <b>fog는 대상이 아니다</b> (M8 착수 준비 결정 ② ㉮) — <c>RenderSettings.fog*</c>는
    /// <c>WeatherVisualController</c> 단독 소유이므로 여기서 읽지도 쓰지도 않는다.
    /// </para>
    /// </summary>
    public readonly struct RenderEnvironmentSnapshot
    {
        private readonly AmbientMode _ambientMode;
        private readonly Color _ambientSky;
        private readonly Color _ambientEquator;
        private readonly Color _ambientGround;
        private readonly float _ambientIntensity;

        private readonly Material _skybox;
        private readonly Light _renderSun;

        /// <summary>연출이 회전·착색하는 광원. null이면 광원 항목은 복원 대상에서 빠진다.</summary>
        private readonly Light _light;

        private readonly Color _lightColor;
        private readonly float _lightIntensity;
        private readonly Quaternion _lightRotation;

        private RenderEnvironmentSnapshot(
            AmbientMode ambientMode, Color ambientSky, Color ambientEquator, Color ambientGround,
            float ambientIntensity, Material skybox, Light renderSun,
            Light light, Color lightColor, float lightIntensity, Quaternion lightRotation)
        {
            _ambientMode = ambientMode;
            _ambientSky = ambientSky;
            _ambientEquator = ambientEquator;
            _ambientGround = ambientGround;
            _ambientIntensity = ambientIntensity;
            _skybox = skybox;
            _renderSun = renderSun;
            _light = light;
            _lightColor = lightColor;
            _lightIntensity = lightIntensity;
            _lightRotation = lightRotation;
        }

        /// <summary>지금의 환경 상태를 그대로 떠 둔다.</summary>
        /// <param name="light">연출이 건드릴 방향광. null이면 광원 항목 없이 환경만 뜬다.</param>
        public static RenderEnvironmentSnapshot Capture(Light light)
        {
            bool hasLight = light != null;

            return new RenderEnvironmentSnapshot(
                RenderSettings.ambientMode,
                RenderSettings.ambientSkyColor,
                RenderSettings.ambientEquatorColor,
                RenderSettings.ambientGroundColor,
                RenderSettings.ambientIntensity,
                RenderSettings.skybox,
                RenderSettings.sun,
                light,
                hasLight ? light.color : Color.white,
                hasLight ? light.intensity : 1f,
                hasLight ? light.transform.rotation : Quaternion.identity);
        }

        /// <summary>환경광을 원래대로 (모드 포함 — 연출은 <see cref="AmbientMode.Trilight"/>로 바꿔 쓴다).</summary>
        public void RestoreAmbient()
        {
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            RenderSettings.ambientIntensity = _ambientIntensity;
        }

        /// <summary>하늘·태양 지정과 광원의 색·강도·각도를 원래대로.</summary>
        public void RestoreSkyAndSun()
        {
            RenderSettings.skybox = _skybox;
            RenderSettings.sun = _renderSun;

            if (_light == null)
            {
                return;
            }

            _light.color = _lightColor;
            _light.intensity = _lightIntensity;
            _light.transform.rotation = _lightRotation;
        }
    }
}
