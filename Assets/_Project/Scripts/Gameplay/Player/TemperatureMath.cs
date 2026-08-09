using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>체온 압박 단계 — HUD 경고와 피해 판정의 공통 표현.</summary>
    public enum TemperatureStress : byte
    {
        None = 0,

        /// <summary>고온 쪽으로 벗어남 (열사병, 기획서 §4.2 사막 낮).</summary>
        Heat = 1,

        /// <summary>저온 쪽으로 벗어남 (급랭·동상, 기획서 §4.2 사막 밤 / §4.4 북극).</summary>
        Cold = 2,
    }

    /// <summary>
    /// 체온 곡선의 순수 수치 묶음 — <see cref="TemperatureSettings"/>에서 뽑아
    /// <see cref="TemperatureMath"/>에 넘긴다 (순수 로직이 ScriptableObject를 모르게 하는 경계).
    /// </summary>
    public readonly struct TemperatureCurve
    {
        public readonly float NormalBody;
        public readonly float MinBody;
        public readonly float MaxBody;

        public readonly float ComfortMin;
        public readonly float ComfortMax;

        public readonly float DriftRatePerDegree;
        public readonly float RecoveryRate;

        public readonly float HeatWarnThreshold;
        public readonly float HeatDamageThreshold;
        public readonly float ColdWarnThreshold;
        public readonly float ColdDamageThreshold;
        public readonly float DamagePerDegreePerSecond;

        public readonly float ShelterFactor;

        public readonly float HeaterFactor;

        public TemperatureCurve(
            float normalBody, float minBody, float maxBody,
            float comfortMin, float comfortMax,
            float driftRatePerDegree, float recoveryRate,
            float heatWarnThreshold, float heatDamageThreshold,
            float coldWarnThreshold, float coldDamageThreshold,
            float damagePerDegreePerSecond, float shelterFactor, float heaterFactor)
        {
            NormalBody = normalBody;
            MinBody = minBody;
            MaxBody = maxBody;
            ComfortMin = comfortMin;
            ComfortMax = comfortMax;
            DriftRatePerDegree = driftRatePerDegree;
            RecoveryRate = recoveryRate;
            HeatWarnThreshold = heatWarnThreshold;
            HeatDamageThreshold = heatDamageThreshold;
            ColdWarnThreshold = coldWarnThreshold;
            ColdDamageThreshold = coldDamageThreshold;
            DamagePerDegreePerSecond = damagePerDegreePerSecond;
            ShelterFactor = shelterFactor;
            HeaterFactor = heaterFactor;
        }

        /// <summary>쾌적대의 중심 — 차폐(건축물 아래)가 환경 온도를 끌어당기는 목표점.</summary>
        public float ComfortCenter => (ComfortMin + ComfortMax) * 0.5f;
    }

    /// <summary>
    /// 체온의 순수 계산 로직 (기획서 §4.2 — 사막 낮 고온/밤 급랭의 온도 관리).
    /// 환경 온도가 쾌적대를 벗어난 만큼 체온이 그 방향으로 표류하고, 쾌적대 안에서는 정상 체온으로 회복한다.
    /// M5 장비(사막 로브·방한 세트)가 들어오면 여기 배율을 얹는 것으로 확장된다.
    /// </summary>
    public static class TemperatureMath
    {
        /// <summary>
        /// 건축물 효과를 반영한 실효 환경 온도 (M5 3차 — 건축물 종류화).
        /// 그늘(돔)은 환경 온도가 쾌적 상한을 넘을 때만, 난방(난방기)은 쾌적 하한을 밑돌 때만
        /// 쾌적대 중심으로 당긴다 — 지붕이 햇빛은 가려도 난방은 되지 않고, 화로가 그늘을 만들지도 않는다.
        /// </summary>
        public static float ResolveAmbient(
            float regionAmbient, bool hasShade, bool hasHeat, in TemperatureCurve curve)
        {
            if (hasShade && regionAmbient > curve.ComfortMax)
            {
                return Mathf.Lerp(regionAmbient, curve.ComfortCenter, Mathf.Clamp01(curve.ShelterFactor));
            }

            if (hasHeat && regionAmbient < curve.ComfortMin)
            {
                return Mathf.Lerp(regionAmbient, curve.ComfortCenter, Mathf.Clamp01(curve.HeaterFactor));
            }

            return regionAmbient;
        }

        /// <summary>
        /// 장비 단열을 반영한 실효 환경 온도 (기획서 §6.3, M5 3차) — 적용 순서는
        /// 지역 온도 → 건축물(<see cref="ResolveAmbient"/>) → 장비 → <see cref="Step"/>.
        /// 추우면 쾌적 하한으로 cold만큼, 더우면 쾌적 상한으로 heat만큼 당긴다.
        /// <b>음수 계수 = 이탈 확대</b> — 보온복의 사막 낮 역효과가 같은 수식으로 성립한다.
        /// </summary>
        public static float ApplyInsulation(float ambient, float cold, float heat, in TemperatureCurve curve)
        {
            if (ambient < curve.ComfortMin)
            {
                return Mathf.LerpUnclamped(ambient, curve.ComfortMin, EquipmentClamp(cold));
            }

            if (ambient > curve.ComfortMax)
            {
                return Mathf.LerpUnclamped(ambient, curve.ComfortMax, EquipmentClamp(heat));
            }

            return ambient;
        }

        private static float EquipmentClamp(float factor)
        {
            return Mathf.Clamp(factor, -1f, 0.9f);
        }

        /// <summary>
        /// 즉시 체온 상승 상한의 국면화 (M5 6차 — 5차 G3 "사막의 낮에서는 스튜 상한이 좀 더 높으면").
        /// 환경이 쾌적 상한을 넘는 <b>더위 국면이면 hotCeiling</b>(고온 피해 임계 직전까지 허용),
        /// 아니면 baseCeiling. "끌어내리지 않는다"(5차 G4)는 호출부의 몫이다 — 여기는 상한만 고른다.
        /// </summary>
        public static float ResolveWarmthCeiling(
            float ambient, float baseCeiling, float hotCeiling, in TemperatureCurve curve)
        {
            return ambient > curve.ComfortMax ? hotCeiling : baseCeiling;
        }

        /// <summary>한 스텝 뒤의 체온. 표류 속도는 쾌적대를 벗어난 정도에 비례한다.</summary>
        public static float Step(float current, float ambient, in TemperatureCurve curve, float deltaTime)
        {
            float target;
            float rate;

            if (ambient > curve.ComfortMax)
            {
                target = curve.MaxBody;
                rate = (ambient - curve.ComfortMax) * curve.DriftRatePerDegree;
            }
            else if (ambient < curve.ComfortMin)
            {
                target = curve.MinBody;
                rate = (curve.ComfortMin - ambient) * curve.DriftRatePerDegree;
            }
            else
            {
                target = curve.NormalBody;
                rate = curve.RecoveryRate;
            }

            float next = Mathf.MoveTowards(current, target, Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime));
            return Mathf.Clamp(next, curve.MinBody, curve.MaxBody);
        }

        /// <summary>피해 임계를 벗어난 만큼의 초당 피해량. 임계 안이면 0.</summary>
        public static float GetDamagePerSecond(float temperature, in TemperatureCurve curve)
        {
            if (temperature > curve.HeatDamageThreshold)
            {
                return (temperature - curve.HeatDamageThreshold) * curve.DamagePerDegreePerSecond;
            }

            if (temperature < curve.ColdDamageThreshold)
            {
                return (curve.ColdDamageThreshold - temperature) * curve.DamagePerDegreePerSecond;
            }

            return 0f;
        }

        /// <summary>HUD 경고용 압박 단계 — 피해가 시작되기 전 경고 임계에서 이미 반응한다.</summary>
        public static TemperatureStress GetStress(float temperature, in TemperatureCurve curve)
        {
            if (temperature >= curve.HeatWarnThreshold)
            {
                return TemperatureStress.Heat;
            }

            if (temperature <= curve.ColdWarnThreshold)
            {
                return TemperatureStress.Cold;
            }

            return TemperatureStress.None;
        }
    }
}
