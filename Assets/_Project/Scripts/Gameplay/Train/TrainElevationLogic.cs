using System.Collections.Generic;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차·궤도 높이 단계의 순수 계산 — 씬·물리·네트워크 없이 EditMode로 검증 가능하게 분리한다.
    /// <para>
    /// 단계는 "현재(0) → 아래(1) → 더 아래(2) → 다시 현재" 순환이고, 각 단계 값은
    /// <b>기준 배치에서 더할 오프셋</b>이다 (0 = 씬·에셋에 굳어 있는 높이, 음수 = 내려간다).
    /// 갑판·레일·손잡이·설비가 전부 <b>같은 오프셋 하나</b>를 더하므로, 어느 단계에서든
    /// 서로의 상대 높이(바퀴가 레일에 얹히는 관계, 갑판까지의 거리)가 그대로 보존된다 —
    /// 이것이 높이를 바꿔도 건설·콜라이더가 깨지지 않는 근거다.
    /// </para>
    /// </summary>
    public static class TrainElevationLogic
    {
        /// <summary>단계 인덱스를 유효 범위로 가둔다 — 범위 밖 값은 순환시키지 않고 잘라 낸다(잘못된 상태 전파 방지).</summary>
        public static int NormalizeStep(int step, int stepCount)
        {
            if (stepCount <= 0 || step < 0)
            {
                return 0;
            }

            return step >= stepCount ? stepCount - 1 : step;
        }

        /// <summary>다음 단계 — 마지막 단계 다음은 처음(현재 높이)으로 돌아온다.</summary>
        public static int NextStep(int step, int stepCount)
        {
            if (stepCount <= 0)
            {
                return 0;
            }

            return (NormalizeStep(step, stepCount) + 1) % stepCount;
        }

        /// <summary>단계의 높이 오프셋 — 목록이 비었거나 인덱스가 범위 밖이면 0(기준 높이)이다.</summary>
        public static float ResolveOffset(IReadOnlyList<float> stepOffsets, int step)
        {
            if (stepOffsets == null || stepOffsets.Count == 0)
            {
                return 0f;
            }

            return stepOffsets[NormalizeStep(step, stepOffsets.Count)];
        }

        /// <summary>
        /// 기준 높이에 오프셋을 얹은 실제 y. 열차 루트·궤도·갑판 기준선이 전부 이 한 식을 쓴다 —
        /// 대상마다 다른 식을 쓰면 단계 전환에서 어긋난다.
        /// </summary>
        public static float ResolveElevatedY(float baseY, float offset)
        {
            return baseY + offset;
        }
    }
}
