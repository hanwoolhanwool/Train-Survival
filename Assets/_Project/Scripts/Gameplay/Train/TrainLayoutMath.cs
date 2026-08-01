using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 레이아웃의 순수 좌표 계산 — 데이터(<see cref="TrainLayoutSettings"/>)와 분리해
    /// 물리·씬 없이 EditMode로 검증 가능하게 둔다.
    /// </summary>
    public static class TrainLayoutMath
    {
        /// <summary>편성 인덱스 칸의 중심 Z. 열차는 선두 고정·후미로만 자라므로 증설 칸에도 유효하다.</summary>
        public static float GetCarCenterZ(int index, float frontZ, float carLength, float couplingGap)
        {
            return frontZ - carLength * 0.5f - index * (carLength + couplingGap);
        }

        /// <summary>
        /// Z 좌표 위에 있는 칸의 인덱스를 역산한다. 칸과 칸 사이(연결부 간격) 위이거나
        /// 편성 범위 밖이면 false — "어느 칸 위에 서 있는가" 판정에 쓴다.
        /// </summary>
        public static bool TryGetCarIndexAtZ(
            float z, float frontZ, float carLength, float couplingGap, int carCount, out int index)
        {
            index = -1;

            if (carCount <= 0 || carLength <= 0f)
            {
                return false;
            }

            float pitch = carLength + couplingGap;
            if (pitch <= 0f)
            {
                return false;
            }

            // GetCarCenterZ의 역함수를 반올림해 가장 가까운 칸을 고른 뒤, 실제로 그 칸 위인지 확인한다.
            int nearest = Mathf.RoundToInt((frontZ - carLength * 0.5f - z) / pitch);
            if (nearest < 0 || nearest >= carCount)
            {
                return false;
            }

            float centerZ = GetCarCenterZ(nearest, frontZ, carLength, couplingGap);
            if (Mathf.Abs(z - centerZ) > carLength * 0.5f)
            {
                // 연결부 간격 위 — 어느 칸에도 속하지 않는다.
                return false;
            }

            index = nearest;
            return true;
        }
    }
}
