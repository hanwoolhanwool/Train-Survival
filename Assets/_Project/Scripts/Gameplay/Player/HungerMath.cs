using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>허기 압박 단계 — HUD 경고와 피해 판정의 공통 표현 (체온의 <see cref="TemperatureStress"/> 대응).</summary>
    public enum HungerStress : byte
    {
        None = 0,

        /// <summary>경고 임계 밑 — 음식 섭취 행동 지시 (기획서 §3.4 상태 창, M5 4차).</summary>
        Hungry = 1,

        /// <summary>0 도달 — 기아 지속 피해 진행 중.</summary>
        Starving = 2,
    }

    /// <summary>
    /// 허기 곡선의 순수 수치 묶음 — <see cref="HungerSettings"/>에서 뽑아
    /// <see cref="HungerMath"/>에 넘긴다 (순수 로직이 ScriptableObject를 모르게 하는 경계 —
    /// 체온의 <see cref="TemperatureCurve"/>와 같은 규약).
    /// </summary>
    public readonly struct HungerCurve
    {
        public readonly float MaxHunger;

        public readonly float DecayPerSecond;

        public readonly float WarnThreshold;

        public readonly float StarveDamagePerSecond;

        public HungerCurve(
            float maxHunger, float decayPerSecond, float warnThreshold, float starveDamagePerSecond)
        {
            MaxHunger = maxHunger;
            DecayPerSecond = decayPerSecond;
            WarnThreshold = warnThreshold;
            StarveDamagePerSecond = starveDamagePerSecond;
        }
    }

    /// <summary>
    /// 허기의 순수 계산 로직 (기획서 §3.4 상태 창의 예약 자리, M5 4차 — 요리·허기).
    /// 체온(<see cref="TemperatureMath"/>)과 같은 3층 구조지만 환경 완충(건축물·장비 층)이 없어
    /// 자연 감소·섭취 회복·기아 피해만 다룬다. 회복 수단은 음식 섭취뿐이다.
    /// </summary>
    public static class HungerMath
    {
        /// <summary>한 스텝 뒤의 허기 — 자연 감소. 0 밑으로 내려가지 않는다.</summary>
        public static float Step(float current, in HungerCurve curve, float deltaTime)
        {
            float next = current - Mathf.Max(0f, curve.DecayPerSecond) * Mathf.Max(0f, deltaTime);
            return Mathf.Clamp(next, 0f, curve.MaxHunger);
        }

        /// <summary>음식 섭취 회복 — 최대치에서 잘린다.</summary>
        public static float Restore(float current, float amount, in HungerCurve curve)
        {
            return Mathf.Clamp(current + Mathf.Max(0f, amount), 0f, curve.MaxHunger);
        }

        /// <summary>기아(0 도달) 상태의 초당 피해량. 허기가 남아 있으면 0.</summary>
        public static float GetDamagePerSecond(float current, in HungerCurve curve)
        {
            return current <= 0f ? Mathf.Max(0f, curve.StarveDamagePerSecond) : 0f;
        }

        /// <summary>HUD 경고용 압박 단계 — 피해가 시작되기 전 경고 임계에서 이미 반응한다.</summary>
        public static HungerStress GetStress(float current, in HungerCurve curve)
        {
            if (current <= 0f)
            {
                return HungerStress.Starving;
            }

            if (current <= curve.WarnThreshold)
            {
                return HungerStress.Hungry;
            }

            return HungerStress.None;
        }
    }
}
