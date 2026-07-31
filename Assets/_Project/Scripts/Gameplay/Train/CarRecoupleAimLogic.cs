using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 재결합 조준 안내 상태 (손잡이-이탈저항 스펙 §4.1) — 확정 조건 중 먼저 걸리는 것 하나만 표시한다.
    /// 순서는 구조적 순서 → 진행 → 비용: 못 고칠 이유부터 알려야 플레이어가 다음 행동을 고를 수 있다.
    /// </summary>
    public enum RecouplePrompt
    {
        /// <summary>조준이 성립하지 않음.</summary>
        None,

        /// <summary>앞 칸이 편성에 없다 — 파괴된 자리면 칸 건설, 아직 이탈 중이면 그 칸부터 재결합해야 한다.</summary>
        FrontCarMissing,

        /// <summary>아직 슬롯에 닿지 않았다 — 손잡이로 더 끌어와야 한다.</summary>
        NotAtSlot,

        /// <summary>재결합 자원이 부족하다.</summary>
        InsufficientResources,

        /// <summary>전부 충족 — 지금 우클릭하면 붙는다.</summary>
        Ready,
    }

    /// <summary>
    /// 이탈 칸 재결합 조준 순수 계산 (손잡이-이탈저항 스펙 §4.1).
    /// 겨눌 칸·이어질 연결부 자리·안내 문구 우선순위를 판정한다. MonoBehaviour 비의존 — EditMode 테스트 대상.
    /// 조준 지점 좌표는 칸 건설과 같은 계산(<see cref="CarBuildAimLogic.AnchorZ"/>)을 쓴다 — 같은 도구의 같은
    /// 조작이므로 겨누는 자리가 갈리면 배우는 규칙만 늘어난다. 지점이 슬롯 기준 고정 좌표라 칸이 멀리 있어도
    /// 겨누는 위치가 도망가지 않는다.
    /// </summary>
    public static class CarRecoupleAimLogic
    {
        /// <summary>슬롯 도달로 보는 오프셋 허용치(m) — 클라 표시 보간 값에는 미세한 잔차가 남는다.</summary>
        public const float SlotArrivalEpsilon = 0.05f;

        /// <summary>
        /// 지금 재결합을 겨눌 칸 — 선두부터 훑어 첫 '이탈 중이고 살아 있으며 소실 전'인 칸.
        /// 앞에서부터 순차로 붙이므로 뒤쪽 이탈 칸은 이 칸을 되붙인 다음 차례가 된다. 없으면 -1.
        /// </summary>
        public static int FindRecoupleTarget(CarState[] cars, float[] ejectOffsets, float lostDistance)
        {
            if (cars == null)
            {
                return -1;
            }

            for (int i = TrainStateLogic.LocomotiveIndex + 1; i < cars.Length; i++)
            {
                CarState car = cars[i];
                if (car.Attached || car.Health <= 0f)
                {
                    continue;
                }

                float offset = ejectOffsets != null && i < ejectOffsets.Length ? ejectOffsets[i] : 0f;
                if (offset < lostDistance)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 이어질 연결부 자리 — 프리뷰 테두리가 그릴 부피. 칸은 이미 눈에 보이므로, 지어질 부피를 그리는
        /// 칸 건설(<see cref="CarBuildAimLogic.BuildVolume"/>)과 달리 강조할 것은 두 칸을 잇는 간극이다.
        /// 열차는 원점 고정이라 X 중심은 0이고, 갑판이 y=<paramref name="deckHeight"/>에 오도록 바닥을 y=0에 맞춘다.
        /// </summary>
        public static void CouplingVolume(float slotCenterZ, float carLength, float couplingGap,
            float carWidth, float deckHeight, out Vector3 center, out Vector3 size)
        {
            center = new Vector3(0f, deckHeight * 0.5f,
                CarBuildAimLogic.AnchorZ(slotCenterZ, carLength, couplingGap));
            size = new Vector3(carWidth, deckHeight, Mathf.Max(0.01f, couplingGap));
        }

        /// <summary>
        /// 안내 상태 — 확정 조건 중 먼저 걸리는 것 하나만 돌려준다. 조준 성립 자체는 확정 조건보다 느슨하다:
        /// 아직 끌어오는 중(<paramref name="offset"/> &gt; 0)이어도 성립시켜 "얼마나 더 끌어와야 하는지"를 알려준다.
        /// </summary>
        public static RecouplePrompt ResolvePrompt(bool frontCarPresent, float offset, bool canAfford)
        {
            if (!frontCarPresent)
            {
                return RecouplePrompt.FrontCarMissing;
            }

            if (offset > SlotArrivalEpsilon)
            {
                return RecouplePrompt.NotAtSlot;
            }

            return canAfford ? RecouplePrompt.Ready : RecouplePrompt.InsufficientResources;
        }
    }
}
