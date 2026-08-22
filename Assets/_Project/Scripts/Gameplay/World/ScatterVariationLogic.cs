using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 스캐터 슬롯의 변주 계산 (레벨 디자인 가이드 §4.5) — 전부 순수 함수.
    ///
    /// <para><b>왜 이것이 팔레트보다 먼저인가.</b> 936장을 흘려보내는데 팔레트는 10종이라
    /// 같은 타일이 평균 133초마다 재등장한다(가이드 §2.2). 프리팹을 늘리는 것은 가장 비싼 해법이고,
    /// 슬롯 하나를 끄고 켜는 것은 거의 공짜다 — 반복 인지는 <b>변주 장치</b>로 먼저 벌어야 한다.</para>
    ///
    /// <para><b>결정론 주의</b>: 이 변주는 각 피어 로컬이라 피어마다 달라 보인다.
    /// 게임플레이에 영향을 주는 것(콜라이더·앵커)에 적용하면 <b>없는 벽을 도는 몬스터</b>가 생긴다
    /// — 순수 장식에만 쓴다(§5.3).</para>
    /// </summary>
    public static class ScatterVariationLogic
    {
        /// <summary>회전 지터 기본값 — 반복 실루엣을 깨는 데는 전방위 회전이 가장 싸다.</summary>
        public const float DefaultYawJitterDegrees = 360f;

        /// <summary>
        /// 이번 활성에서 이 슬롯을 보여줄 것인가. <paramref name="roll"/>은 [0, 1] 난수.
        /// 밀도 0·1은 <b>확실히</b> 숨김·표시다 — <c>Random.value</c>가 1.0을 낼 수 있어
        /// 단순 비교만 두면 밀도 1인 슬롯이 아주 가끔 사라진다.
        /// </summary>
        public static bool ShouldShow(float density, float roll)
        {
            if (density <= 0f)
            {
                return false;
            }

            if (density >= 1f)
            {
                return true;
            }

            return roll < density;
        }

        /// <summary>슬롯에 얹을 Y축 회전(도). 지터 폭이 0이면 저작된 방향 그대로 둔다.</summary>
        public static float YawFor(float roll, float jitterDegrees)
        {
            return Mathf.Clamp01(roll) * jitterDegrees;
        }

        /// <summary>슬롯에 얹을 균등 배율. 범위가 뒤집혀 있어도 안전하게 보간한다.</summary>
        public static float ScaleFor(float roll, float min, float max)
        {
            float low = Mathf.Min(min, max);
            float high = Mathf.Max(min, max);
            return Mathf.Lerp(low, high, Mathf.Clamp01(roll));
        }
    }
}
