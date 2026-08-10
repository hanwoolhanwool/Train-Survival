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

        /// <summary>이탈 오프셋만큼 뒤로 밀린 칸의 실제 중심 Z. 오프셋 0이면 슬롯 중심과 같다.</summary>
        public static float GetCarCenterZ(
            int index, float frontZ, float carLength, float couplingGap, float ejectOffset)
        {
            return GetCarCenterZ(index, frontZ, carLength, couplingGap) - Mathf.Max(0f, ejectOffset);
        }

        /// <summary>
        /// 열차 하부 즉사 존 판정 (M5 6차) — 열차 발자국 안(|x| ≤ 반폭, RearZ‥FrontZ)이면서
        /// 바퀴 높이 이하인가. killHeight 0 이하 = 존 비활성 (에셋으로 끌 수 있는 축).
        /// 대상 제한(견인·파지·기절만)은 호출부의 몫이다 — 여기는 기하만 본다.
        /// </summary>
        public static bool IsInWheelKillZone(
            Vector3 position, float halfWidth, float rearZ, float frontZ, float killHeight)
        {
            return killHeight > 0f
                && position.y <= killHeight
                && Mathf.Abs(position.x) <= halfWidth
                && position.z >= rearZ && position.z <= frontZ;
        }

        /// <summary>
        /// 갑판 낙하 판정의 폭·높이 게이트 (M5 7차 A3) — 열차 폭 안이며 갑판 높이 근처(위)인가.
        /// "어느 칸의 Z 위인가"는 <see cref="IsZOnCar"/>가 칸별로 판정한다 (즉사 존과 같은 분담).
        /// </summary>
        public static bool IsWithinDeckAperture(
            Vector3 position, float halfWidth, float deckHeight, float margin)
        {
            return Mathf.Abs(position.x) <= halfWidth + margin
                && position.y >= deckHeight - margin;
        }

        /// <summary>
        /// Z가 그 칸의 갑판 범위 안인가 — 이탈 오프셋을 반영한다. 칸마다 오프셋이 달라 슬롯 기준
        /// 역산(O(1) 역함수)이 성립하지 않으므로, "어느 칸 위인가"는 호출부가 편성을 순회하며 묻는다.
        /// </summary>
        public static bool IsZOnCar(
            float z, int index, float frontZ, float carLength, float couplingGap, float ejectOffset)
        {
            if (index < 0 || carLength <= 0f)
            {
                return false;
            }

            float centerZ = GetCarCenterZ(index, frontZ, carLength, couplingGap, ejectOffset);
            return Mathf.Abs(z - centerZ) <= carLength * 0.5f;
        }
    }
}
