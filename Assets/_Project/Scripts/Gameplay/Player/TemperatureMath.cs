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

        public TemperatureCurve(
            float normalBody, float minBody, float maxBody,
            float comfortMin, float comfortMax,
            float driftRatePerDegree, float recoveryRate,
            float heatWarnThreshold, float heatDamageThreshold,
            float coldWarnThreshold, float coldDamageThreshold,
            float damagePerDegreePerSecond, float shelterFactor)
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
        /// 차폐를 반영한 실효 환경 온도 — 건축물 아래는 <b>그늘</b>이므로 <b>더위만 막는다</b>.
        /// 환경 온도가 쾌적 상한을 넘을 때만 쾌적대 중심으로 당겨지며, 밤 급랭·혹한은 완화하지 못한다
        /// (지붕이 햇빛은 가려도 난방은 되지 않는다 — 추위 대응은 난방 건축물·방한 장비의 몫, M5).
        /// </summary>
        public static float ResolveAmbient(float regionAmbient, bool sheltered, in TemperatureCurve curve)
        {
            if (!sheltered || regionAmbient <= curve.ComfortMax)
            {
                return regionAmbient;
            }

            return Mathf.Lerp(regionAmbient, curve.ComfortCenter, Mathf.Clamp01(curve.ShelterFactor));
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
