using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 건설 조준 순수 계산 (M3 피드백 — 건설 포트의 망치 통합).
    /// 건설 지점(다음 칸이 붙을 연결부)의 좌표와 시선 조준 성립 여부를 판정한다.
    /// MonoBehaviour 비의존 — EditMode 테스트 대상.
    /// </summary>
    public static class CarBuildAimLogic
    {
        /// <summary>
        /// 슬롯 앞 연결부 중앙 Z — 겨눌 건설 지점. 재건 슬롯(앞 칸과의 연결부)과
        /// 후미 증설 슬롯(후미 칸 뒤끝) 모두 같은 수식이 성립한다.
        /// </summary>
        public static float AnchorZ(float slotCenterZ, float carLength, float couplingGap)
        {
            return slotCenterZ + carLength * 0.5f + couplingGap * 0.5f;
        }

        /// <summary>
        /// 지어질 칸이 차지할 부피 — 프리뷰 테두리이자 자리 점유 판정 영역이다(둘이 같은 상자여야
        /// "초록 테두리 안이 비어야 지어진다"가 성립한다). 열차는 원점 고정이라 X 중심은 0이고,
        /// 상자의 <b>윗면</b>이 y=<paramref name="deckHeight"/>에 오도록 <paramref name="carBodyHeight"/>만큼
        /// 아래로 채운다.
        ///
        /// <para>높이를 갑판 높이와 따로 받는 이유: 열차가 지면에 붙어 있다는 보장이 없다.
        /// 궤도 위에 얹히면 갑판만 올라가고 몸통 두께는 그대로라, 하나로 묶으면 상자가 지면을 파고든다.</para>
        /// </summary>
        public static void BuildVolume(float slotCenterZ, float carWidth, float deckHeight, float carBodyHeight,
            float carLength, out Vector3 center, out Vector3 size)
        {
            center = new Vector3(0f, deckHeight - carBodyHeight * 0.5f, slotCenterZ);
            size = new Vector3(carWidth, carBodyHeight, carLength);
        }

        /// <summary>
        /// 건설 지점을 겨누고 있는지 — 사거리 안이고, 시선이 정렬되며,
        /// 더 가까운 물리 표적(<paramref name="blockingDistance"/>)이 시야를 막지 않아야 한다.
        /// </summary>
        public static bool IsAiming(Vector3 origin, Vector3 forward, Vector3 anchor,
            float maxDistance, float lookDotThreshold, float blockingDistance)
        {
            Vector3 toAnchor = anchor - origin;
            float distance = toAnchor.magnitude;
            if (distance > maxDistance || blockingDistance < distance)
            {
                return false;
            }

            if (distance < 0.001f)
            {
                return true;
            }

            return Vector3.Dot(forward, toAnchor / distance) >= lookDotThreshold;
        }
    }
}
