using UnityEngine;

namespace Game.Gameplay.Cycle
{
    /// <summary>환경광 1벌 — 3색 그라디언트 + 강도. <see cref="UnityEngine.RenderSettings"/>의 ambient에 그대로 대응한다.</summary>
    public readonly struct AmbientTone
    {
        public readonly Color Sky;
        public readonly Color Equator;
        public readonly Color Ground;
        public readonly float Intensity;

        public AmbientTone(Color sky, Color equator, Color ground, float intensity)
        {
            Sky = sky;
            Equator = equator;
            Ground = ground;
            Intensity = intensity;
        }

        public static AmbientTone Lerp(in AmbientTone from, in AmbientTone to, float t)
        {
            return new AmbientTone(
                Color.Lerp(from.Sky, to.Sky, t),
                Color.Lerp(from.Equator, to.Equator, t),
                Color.Lerp(from.Ground, to.Ground, t),
                Mathf.Lerp(from.Intensity, to.Intensity, t));
        }
    }

    /// <summary>방향광 1벌 — 색 + 강도. 각도는 진행도에서 따로 유도한다(<see cref="DayVisualState.SunElevation"/>).</summary>
    public readonly struct SunTone
    {
        public readonly Color Color;
        public readonly float Intensity;

        public SunTone(Color color, float intensity)
        {
            Color = color;
            Intensity = intensity;
        }

        public static SunTone Lerp(in SunTone from, in SunTone to, float t)
        {
            return new SunTone(
                UnityEngine.Color.Lerp(from.Color, to.Color, t),
                Mathf.Lerp(from.Intensity, to.Intensity, t));
        }
    }

    /// <summary>하늘 1벌 — <c>Skybox/Procedural</c> 셰이더의 보간 대상 4값.</summary>
    public readonly struct SkyTone
    {
        public readonly Color Tint;
        public readonly Color Ground;
        public readonly float AtmosphereThickness;
        public readonly float Exposure;

        public SkyTone(Color tint, Color ground, float atmosphereThickness, float exposure)
        {
            Tint = tint;
            Ground = ground;
            AtmosphereThickness = atmosphereThickness;
            Exposure = exposure;
        }

        public static SkyTone Lerp(in SkyTone from, in SkyTone to, float t)
        {
            return new SkyTone(
                Color.Lerp(from.Tint, to.Tint, t),
                Color.Lerp(from.Ground, to.Ground, t),
                Mathf.Lerp(from.AtmosphereThickness, to.AtmosphereThickness, t),
                Mathf.Lerp(from.Exposure, to.Exposure, t));
        }
    }

    /// <summary>
    /// 낮/밤 연출 수치의 순수 묶음 — <see cref="DayVisualSettings"/>에서 뽑아 <see cref="DayVisualMath"/>에
    /// 넘긴다 (순수 로직이 ScriptableObject를 모르게 하는 경계 — <c>TemperatureCurve</c>와 같은 규약).
    /// </summary>
    public readonly struct DayVisualProfile
    {
        public readonly AmbientTone DayAmbient;
        public readonly AmbientTone NightAmbient;

        /// <summary>정오의 태양.</summary>
        public readonly SunTone DaySun;

        /// <summary>국면 경계(일출·일몰)의 태양 — 낮과 밤이 <b>같은 값에서 만나</b> 전환이 이어진다.</summary>
        public readonly SunTone DuskSun;

        /// <summary>한밤의 달빛.</summary>
        public readonly SunTone NightSun;

        public readonly SkyTone DaySky;
        public readonly SkyTone NightSky;

        /// <summary>A안 크로스페이드 길이 (초).</summary>
        public readonly float FadeSeconds;

        /// <summary>국면 경계(일출·일몰)의 태양 고도 (도).</summary>
        public readonly float RiseElevation;

        /// <summary>정오의 태양 고도 (도).</summary>
        public readonly float NoonElevation;

        /// <summary>한밤의 태양 고도 (도) — 지평선 아래로 완전히 내리지 않는다(밝기 하한 설계).</summary>
        public readonly float NightElevation;

        public readonly float YawStart;
        public readonly float YawEnd;

        /// <summary>실효 환경 밝기의 하한 — 색을 어떻게 잡아도 이 밑으로는 내려가지 않는다.</summary>
        public readonly float MinLuminanceGuard;

        public DayVisualProfile(
            in AmbientTone dayAmbient, in AmbientTone nightAmbient,
            in SunTone daySun, in SunTone duskSun, in SunTone nightSun,
            in SkyTone daySky, in SkyTone nightSky,
            float fadeSeconds,
            float riseElevation, float noonElevation, float nightElevation,
            float yawStart, float yawEnd,
            float minLuminanceGuard)
        {
            DayAmbient = dayAmbient;
            NightAmbient = nightAmbient;
            DaySun = daySun;
            DuskSun = duskSun;
            NightSun = nightSun;
            DaySky = daySky;
            NightSky = nightSky;
            FadeSeconds = fadeSeconds;
            RiseElevation = riseElevation;
            NoonElevation = noonElevation;
            NightElevation = nightElevation;
            YawStart = yawStart;
            YawEnd = yawEnd;
            MinLuminanceGuard = minLuminanceGuard;
        }
    }

    /// <summary>B안 평가 결과 — 이 프레임에 적용할 환경광·태양·하늘 전체.</summary>
    public readonly struct DayVisualState
    {
        public readonly AmbientTone Ambient;
        public readonly SunTone Sun;

        /// <summary>태양 고도 (도) — 양수가 지평선 위.</summary>
        public readonly float SunElevation;

        /// <summary>태양 방위 (도) — 하루 동안 그림자가 도는 축.</summary>
        public readonly float SunYaw;

        public readonly SkyTone Sky;

        public DayVisualState(in AmbientTone ambient, in SunTone sun, float sunElevation, float sunYaw, in SkyTone sky)
        {
            Ambient = ambient;
            Sun = sun;
            SunElevation = sunElevation;
            SunYaw = sunYaw;
            Sky = sky;
        }

        /// <summary>방향광 트랜스폼에 그대로 대입할 회전.</summary>
        public Quaternion SunRotation => Quaternion.Euler(SunElevation, SunYaw, 0f);
    }

    /// <summary>
    /// 낮/밤 시각 연출의 순수 계산 로직 (M8 2차). <see cref="IDayCycleService"/>가 주는 국면 진행도
    /// 하나에서 환경광·태양·하늘을 유도한다 — <see cref="DayTimelineMath"/>와 같은 규약이며,
    /// 상태를 보관하지 않으므로 후발 접속·디버그 국면 점프·새벽 보류에서 표시가 저절로 맞는다.
    /// </summary>
    public static class DayVisualMath
    {
        /// <summary>하늘색 휘도가 이 값 이하면 강도로는 밝기를 살릴 수 없다고 보고 색 자체를 보정한다.</summary>
        private const float DegenerateLuminance = 0.001f;

        /// <summary>국면 내 진행도 (0 → 1). 길이가 0 이하면 0 — 0 나눗셈 방어.</summary>
        public static float PhaseProgress(float phaseElapsed, float phaseDuration)
        {
            if (phaseDuration <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(phaseElapsed / phaseDuration);
        }

        /// <summary>
        /// 하루 전체 진행도 (0 → 1) — 낮이 0 ~ 0.5, 밤이 0.5 ~ 1.
        /// 낮·밤 길이가 달라도 각 국면에 절반씩 배분하는 <b>국면 균등 매핑</b>이다(실시간 비례가 아니다).
        /// </summary>
        public static float CycleProgress(DayPhase phase, float phaseT)
        {
            float t = Mathf.Clamp01(phaseT);

            return phase == DayPhase.Night ? 0.5f + t * 0.5f : t * 0.5f;
        }

        /// <summary>A안 크로스페이드 진행 (0 → 1). 길이가 0 이하면 즉시 1 — 전환 없음과 같다.</summary>
        public static float FadeBlend(float phaseElapsed, float fadeSeconds)
        {
            if (fadeSeconds <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(phaseElapsed / fadeSeconds);
        }

        /// <summary>
        /// 국면 안에서 "밤에 가까운 정도" (0 = 정오, 1 = 한밤). 국면 경계에서 양쪽이 <b>0.5로 만나</b>
        /// 낮 → 밤이 이어진다 — B안 상시 보간의 연속성이 여기서 나온다.
        /// </summary>
        public static float Nightness(DayPhase phase, float phaseT)
        {
            float t = Mathf.Clamp01(phaseT);

            // |2t − 1| : 국면 중앙에서 0, 양 경계에서 1.
            float edge = Mathf.Abs(2f * t - 1f);

            return phase == DayPhase.Night ? 1f - edge * 0.5f : edge * 0.5f;
        }

        /// <summary>
        /// A안 — 국면 전환 크로스페이드된 환경광. 직전 국면 색을 <b>상태로 보관하지 않고</b>
        /// "반대 국면 색"으로 정의해 무상태를 유지한다.
        /// </summary>
        public static AmbientTone EvaluateAmbientCrossfade(
            DayPhase phase, float phaseElapsed, in DayVisualProfile profile)
        {
            float blend = FadeBlend(Mathf.Max(0f, phaseElapsed), profile.FadeSeconds);

            AmbientTone from = phase == DayPhase.Day ? profile.NightAmbient : profile.DayAmbient;
            AmbientTone to = phase == DayPhase.Day ? profile.DayAmbient : profile.NightAmbient;

            return ApplyLuminanceGuard(AmbientTone.Lerp(from, to, blend), profile.MinLuminanceGuard);
        }

        /// <summary>B안 — 진행도에서 환경광·태양·하늘을 상시 유도한다.</summary>
        public static DayVisualState EvaluateContinuous(
            DayPhase phase, float phaseElapsed, float phaseDuration, in DayVisualProfile profile)
        {
            float phaseT = PhaseProgress(Mathf.Max(0f, phaseElapsed), phaseDuration);
            float nightness = Nightness(phase, phaseT);

            AmbientTone ambient = ApplyLuminanceGuard(
                AmbientTone.Lerp(profile.DayAmbient, profile.NightAmbient, nightness),
                profile.MinLuminanceGuard);

            SunTone sun = EvaluateSunTone(phase, phaseT, profile);
            float elevation = EvaluateSunElevation(phase, phaseT, profile);
            float yaw = Mathf.Lerp(profile.YawStart, profile.YawEnd, CycleProgress(phase, phaseT));
            SkyTone sky = SkyTone.Lerp(profile.DaySky, profile.NightSky, nightness);

            return new DayVisualState(ambient, sun, elevation, yaw, sky);
        }

        /// <summary>
        /// 태양 색·강도 — 낮은 정오↔경계, 밤은 경계↔한밤을 오간다.
        /// 양쪽 모두 국면 경계에서 <see cref="DayVisualProfile.DuskSun"/>이 되므로 전환이 이어진다.
        /// </summary>
        public static SunTone EvaluateSunTone(DayPhase phase, float phaseT, in DayVisualProfile profile)
        {
            float edge = Mathf.Abs(2f * Mathf.Clamp01(phaseT) - 1f);

            SunTone tone = phase == DayPhase.Night
                ? SunTone.Lerp(profile.NightSun, profile.DuskSun, edge)
                : SunTone.Lerp(profile.DaySun, profile.DuskSun, edge);

            float guarded = Mathf.Max(tone.Intensity, profile.MinLuminanceGuard);

            return new SunTone(tone.Color, guarded);
        }

        /// <summary>
        /// 태양 고도 (도) — 낮은 일출각 → 정오각 → 일몰각, 밤은 일몰각 → 야간 최저각 → 일출각.
        /// <para>
        /// 밤에도 <see cref="DayVisualProfile.NightElevation"/> 밑으로 내리지 않는다. 점광원이 M8
        /// 비범위라 정직하게 어둡게 하면 아무것도 보이지 않는 것이 첫 이유이고(착수 준비 리스크 2),
        /// <b>고도가 0 이하가 되면 빛이 아래에서 위로 향해 지면 그림자가 통째로 사라지는 것</b>이
        /// 둘째 이유다 — 밤에 방향광의 존재 의의가 없어진다.
        /// </para>
        /// </summary>
        public static float EvaluateSunElevation(DayPhase phase, float phaseT, in DayVisualProfile profile)
        {
            // 국면 중앙에서 1, 경계에서 0 — 중앙값(정오/한밤)으로 얼마나 다가갔는가.
            float center = 1f - Mathf.Abs(2f * Mathf.Clamp01(phaseT) - 1f);

            float peak = phase == DayPhase.Night ? profile.NightElevation : profile.NoonElevation;

            return Mathf.Lerp(profile.RiseElevation, peak, center);
        }

        /// <summary>
        /// 실효 환경 밝기(하늘색 휘도 × 강도)가 하한 밑이면 <b>색은 두고 강도만</b> 끌어올린다 —
        /// 색조가 밤의 표현이므로 색을 흐리지 않는 쪽을 택한다. 하늘색이 사실상 검정이면
        /// 강도로 살릴 수 없으므로 그때만 회색으로 최소 보정한다.
        /// </summary>
        public static AmbientTone ApplyLuminanceGuard(in AmbientTone tone, float guard)
        {
            if (guard <= 0f)
            {
                return tone;
            }

            float luminance = tone.Sky.grayscale;
            if (luminance * tone.Intensity >= guard)
            {
                return tone;
            }

            if (luminance <= DegenerateLuminance)
            {
                Color floor = new Color(guard, guard, guard, 1f);

                return new AmbientTone(floor, floor, floor, 1f);
            }

            return new AmbientTone(tone.Sky, tone.Equator, tone.Ground, guard / luminance);
        }
    }
}
