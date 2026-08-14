using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 부위별 동상 단계 (M7 3차 결정 ⑥) — 연속 진행도를 <b>3단으로 이산화</b>한 복제 표현.
    /// 부위 4개 × 2비트가 <see cref="System.Byte"/> 하나에 들어간다.
    /// </summary>
    public enum FrostbiteStage : byte
    {
        None = 0,

        /// <summary>경증 — 이동속도 소폭 저하 + 화면 가장자리 서리.</summary>
        Mild = 1,

        /// <summary>중증 — 저하 누적. 부위를 비운 채 오래 버틴 결과다.</summary>
        Severe = 2,
    }

    /// <summary>
    /// 동상 곡선의 순수 수치 묶음 — <see cref="TemperatureSettings"/>에서 뽑아 <see cref="FrostbiteMath"/>에
    /// 넘긴다 (<see cref="TemperatureCurve"/>와 같은 경계 — 순수 로직이 ScriptableObject를 모르게 한다).
    /// </summary>
    public readonly struct FrostbiteCurve
    {
        /// <summary>이 체온 미만에서 동상이 진행한다 (= 체온 곡선의 저온 경고 임계).</summary>
        public readonly float ColdThreshold;

        /// <summary>맨 부위·완화 없음일 때의 초당 진행도.</summary>
        public readonly float ProgressPerSecond;

        /// <summary>체온이 임계 위로 돌아왔을 때의 초당 회복량.</summary>
        public readonly float RecoveryPerSecond;

        /// <summary>부위 단열 1당 진행 저항 가산 — 클수록 옷 한 벌의 가치가 크다.</summary>
        public readonly float InsulationWeight;

        /// <summary>전신 완화(요리 보온 + 난방칸) 1당 진행 저항 가산.</summary>
        public readonly float MitigationWeight;

        /// <summary>경증 진입 진행도.</summary>
        public readonly float MildThreshold;

        /// <summary>중증 진입 진행도 (= 진행도 상한).</summary>
        public readonly float SevereThreshold;

        /// <summary>단계 합계 1당 이동속도 배율 감소량.</summary>
        public readonly float MoveSpeedPenaltyPerStage;

        /// <summary>이동속도 배율 하한 — 네 부위 전부 중증이어도 이 밑으로는 내려가지 않는다.</summary>
        public readonly float MinMoveSpeedMultiplier;

        public FrostbiteCurve(
            float coldThreshold, float progressPerSecond, float recoveryPerSecond,
            float insulationWeight, float mitigationWeight,
            float mildThreshold, float severeThreshold,
            float moveSpeedPenaltyPerStage, float minMoveSpeedMultiplier)
        {
            ColdThreshold = coldThreshold;
            ProgressPerSecond = progressPerSecond;
            RecoveryPerSecond = recoveryPerSecond;
            InsulationWeight = insulationWeight;
            MitigationWeight = mitigationWeight;
            MildThreshold = mildThreshold;
            SevereThreshold = severeThreshold;
            MoveSpeedPenaltyPerStage = moveSpeedPenaltyPerStage;
            MinMoveSpeedMultiplier = minMoveSpeedMultiplier;
        }
    }

    /// <summary>
    /// 부위별 동상의 순수 계산 (기획서 §4.4 — 북극의 "부위별 동상", M7 3차 결정 ② · ⑥).
    ///
    /// <para><b>부위가 가르는 것은 속도다.</b> 게임플레이 증상은 이동속도 저하 1축이고(결정 ②),
    /// 네 부위 단계의 <b>합계</b>가 그 배율과 화면 결빙 강도를 함께 정한다. 어느 부위를 비웠는가는
    /// "얼마나 빨리 심해지는가"로 나타난다 — 그 부위 장비의 단열이 진행 속도를 나누기 때문이다.</para>
    ///
    /// <para><b>완화는 몸 전체에 적용된다</b>(결정 ⑥) — 요리 보온 버프와 난방칸은 네 부위 모두의
    /// 진행을 늦춘다. 부위를 가르는 것은 <b>옷</b>뿐이다.</para>
    /// </summary>
    public static class FrostbiteMath
    {
        /// <summary>부위 수 — <see cref="Inventory.EquipSlot"/> 4부위와 같은 인덱스 규약.</summary>
        public const int PartCount = 4;

        /// <summary>단계 합계의 최대 (부위 4 × 중증 2).</summary>
        public const int MaxStageSum = PartCount * (int)FrostbiteStage.Severe;

        /// <summary>
        /// 한 부위의 진행도 한 스텝. 체온이 임계 미만이면 진행하고, 임계 위로 돌아오면 회복한다.
        /// 진행 속도는 <b>그 부위의 단열</b>과 <b>전신 완화</b>가 함께 나눈다 (완화는 네 부위 공통).
        /// 결과는 [0, SevereThreshold]로 잘린다 — 중증 위로 더 쌓이면 회복만 한없이 길어진다.
        /// </summary>
        public static float StepProgress(
            float progress, float temperature, float partInsulation, float bodyMitigation,
            in FrostbiteCurve curve, float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);

            float next = temperature < curve.ColdThreshold
                ? progress + ResolveProgressRate(partInsulation, bodyMitigation, curve) * step
                : progress - Mathf.Max(0f, curve.RecoveryPerSecond) * step;

            return Mathf.Clamp(next, 0f, curve.SevereThreshold);
        }

        /// <summary>
        /// 초당 진행도 — 부위 단열과 전신 완화가 저항으로 쌓여 기본 속도를 나눈다.
        /// <b>음수 계수는 저항을 늘리지 않는다</b>(0으로 본다) — 역효과 장비가 동상을 가속하는 축은
        /// 두지 않는다. 사막 로브를 입고 북극에 오는 것은 "맨몸과 같다"까지가 벌이다.
        /// </summary>
        public static float ResolveProgressRate(
            float partInsulation, float bodyMitigation, in FrostbiteCurve curve)
        {
            float resistance = 1f
                + Mathf.Max(0f, partInsulation) * Mathf.Max(0f, curve.InsulationWeight)
                + Mathf.Max(0f, bodyMitigation) * Mathf.Max(0f, curve.MitigationWeight);

            return Mathf.Max(0f, curve.ProgressPerSecond) / resistance;
        }

        /// <summary>진행도 → 단계. 경계값은 <b>이상</b>이면 그 단계다.</summary>
        public static FrostbiteStage GetStage(float progress, in FrostbiteCurve curve)
        {
            if (progress >= curve.SevereThreshold)
            {
                return FrostbiteStage.Severe;
            }

            return progress >= curve.MildThreshold ? FrostbiteStage.Mild : FrostbiteStage.None;
        }

        /// <summary>
        /// 단계 합계 → 이동속도 배율 (결정 ② — 개입점 1축). 하한 아래로는 내려가지 않는다.
        /// </summary>
        public static float GetMoveSpeedMultiplier(int stageSum, in FrostbiteCurve curve)
        {
            if (stageSum <= 0)
            {
                return 1f;
            }

            float multiplier = 1f - stageSum * Mathf.Max(0f, curve.MoveSpeedPenaltyPerStage);
            return Mathf.Max(curve.MinMoveSpeedMultiplier, multiplier);
        }

        /// <summary>단계 합계 → 화면 결빙 강도 (0~1) — 로컬 표현 전용이라 복제하지 않는다.</summary>
        public static float GetFreezeIntensity(int stageSum)
        {
            return Mathf.Clamp01(stageSum / (float)MaxStageSum);
        }

        /// <summary>
        /// 부위 4개의 단계를 <b>2비트씩 1바이트</b>로 묶는다 — 복제 페이로드 (진행도 자체는 서버 전용,
        /// 단계만 복제한다. <see cref="PlayerBuffs"/>가 세기를 복제하지 않는 것과 같은 규약).
        /// </summary>
        public static byte Pack(FrostbiteStage head, FrostbiteStage body, FrostbiteStage legs, FrostbiteStage feet)
        {
            return (byte)(((int)head & 0x3)
                | (((int)body & 0x3) << 2)
                | (((int)legs & 0x3) << 4)
                | (((int)feet & 0x3) << 6));
        }

        /// <summary>비트팩에서 한 부위의 단계를 꺼낸다. 범위 밖 인덱스는 <see cref="FrostbiteStage.None"/>.</summary>
        public static FrostbiteStage Unpack(byte packed, int partIndex)
        {
            if (partIndex < 0 || partIndex >= PartCount)
            {
                return FrostbiteStage.None;
            }

            return (FrostbiteStage)((packed >> (partIndex * 2)) & 0x3);
        }

        /// <summary>비트팩의 네 부위 단계 합계 (0~<see cref="MaxStageSum"/>).</summary>
        public static int SumStages(byte packed)
        {
            int sum = 0;
            for (int i = 0; i < PartCount; i++)
            {
                sum += (int)Unpack(packed, i);
            }

            return sum;
        }
    }
}
