using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 역 소품의 내용물 추첨 — 전부 순수 함수
    /// ([기차역 이벤트 구현 계획](docs/plans/features/기차역-이벤트-구현-계획.md) §4.3).
    ///
    /// <para><b>결정론이 필수는 아니다.</b> 내용물은 호스트가 정해 <c>NetworkList</c>로 복제하므로
    /// 지형 추첨(<see cref="SegmentPickLogic"/>)처럼 전 피어가 같은 답을 낼 필요가 없다.
    /// 그런데도 순수 함수로 두는 이유는 <b>테스트</b>다 — 롤 값을 주입해 경계를 고정할 수 있다.</para>
    /// </summary>
    public static class StationLootLogic
    {
        /// <summary>
        /// 소품 종류가 요구하는 집게 등급 — <b>데이터가 아니라 규칙</b>이다.
        /// 금고가 3단계인 것이 이 기능의 성장 축이라 저작 실수로 흔들리면 안 된다.
        /// </summary>
        public static int RequiredTierFor(StationPropKind kind)
        {
            switch (kind)
            {
                case StationPropKind.Safe:
                    return 3;
                case StationPropKind.Vending:
                    return 2;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// 정수 구간 [min, max]에서 하나 — <paramref name="roll"/>은 0~1.
        /// 뒤집힌 구간(min &gt; max)도 받아 준다. 저작 실수로 스폰이 멈추는 편이 더 나쁘다.
        /// </summary>
        public static int RollRange(int min, int max, float roll)
        {
            if (max < min)
            {
                int swap = min;
                min = max;
                max = swap;
            }

            int span = max - min + 1;
            if (span <= 1)
            {
                return min;
            }

            int offset = Mathf.FloorToInt(roll * span);

            // roll이 1.0이거나 음수로 새어 들어와도 구간 밖으로 나가지 않는다.
            if (offset >= span)
            {
                offset = span - 1;
            }
            else if (offset < 0)
            {
                offset = 0;
            }

            return min + offset;
        }

        /// <summary>
        /// 가중 추첨 — 규칙 자체는 <see cref="SegmentPickLogic.WeightedPick"/>이 소유한다.
        /// 여기서 다시 쓰면 두 벌이 조용히 갈린다.
        /// </summary>
        public static int RollEntry(float[] weights, float roll)
        {
            return SegmentPickLogic.WeightedPick(weights, roll, -1);
        }

        /// <summary>이 자리를 비워 둘 것인가 — 역마다 조금씩 달라 보이게 하는 장치.</summary>
        public static bool RollEmpty(float emptyChance, float roll)
        {
            if (emptyChance <= 0f)
            {
                return false;
            }

            return roll < emptyChance;
        }
    }
}
