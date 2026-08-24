namespace Game.Gameplay.Train
{
    /// <summary>점유 요청이 기각된 사유 (M7 4차 §2.2) — 문구·로그가 이 값으로 갈린다.</summary>
    public enum MountRejectReason : byte
    {
        /// <summary>승인.</summary>
        None = 0,

        /// <summary>거치 무기가 아닌 건축물 — 조작된 요청 방어.</summary>
        NotMountedWeapon = 1,

        /// <summary>자동 무기(터렛)라 붙을 자리가 없다 — 탄은 1회 상호작용으로 채운다 (B단계).</summary>
        Automated = 2,

        /// <summary>파괴됐거나 얹힌 칸이 죽었다.</summary>
        Destroyed = 3,

        /// <summary>이미 다른 사람이 붙어 있다 — 한 무기에 1인 확정.</summary>
        Occupied = 4,

        /// <summary>좌석 반경 밖.</summary>
        TooFar = 5,
    }

    /// <summary>
    /// 거치 무기 점유의 순수 판정 (M7 4차 §2.2 — EditMode 대상).
    /// 물리·네트워크 조회는 호출부가 하고, <b>규칙은 이 함수들이 소유한다</b>
    /// (레벨 검사기·건축 그리드에서 세운 것과 같은 규약).
    /// </summary>
    public static class MountOccupancyLogic
    {
        /// <summary>
        /// 붙을 수 있는가 — 판정 순서는 <b>정체 → 생존 → 경합 → 거리</b>다.
        /// 거리를 마지막에 두는 이유: 먼 곳의 파괴된 무기에 "너무 멀다"고 답하면 사유가 사실을 가린다.
        /// </summary>
        /// <param name="isMountedWeapon">카탈로그에 거치 무기 설정이 물려 있는 종류인가.</param>
        /// <param name="manned">사람이 붙는 무기인가 (false = 자동 터렛).</param>
        /// <param name="structureAlive">건축물이 살아 있고 얹힌 칸도 살아 있는가.</param>
        /// <param name="occupiedByOther">이미 다른 사람이 붙어 있는가.</param>
        /// <param name="distanceSq">요청자와 좌석 기준점(건축물 점유 영역 중심) 사이 거리의 제곱.</param>
        /// <param name="radiusSq">좌석 반경의 제곱.</param>
        public static MountRejectReason CanMount(
            bool isMountedWeapon, bool manned, bool structureAlive, bool occupiedByOther,
            float distanceSq, float radiusSq)
        {
            if (!isMountedWeapon)
            {
                return MountRejectReason.NotMountedWeapon;
            }

            if (!manned)
            {
                return MountRejectReason.Automated;
            }

            if (!structureAlive)
            {
                return MountRejectReason.Destroyed;
            }

            if (occupiedByOther)
            {
                return MountRejectReason.Occupied;
            }

            return distanceSq > radiusSq ? MountRejectReason.TooFar : MountRejectReason.None;
        }

        /// <summary>
        /// 강제 하차 사유가 성립하는가 (§2.7) — 건축물 파괴·철거·칸 파괴(<paramref name="structureAlive"/>가
        /// 거짓), 점유자 사망, 점유자 접속 종료. 넷 중 무엇이든 서버가 <b>먼저</b> 점유를 지운다
        /// (유령 점유 방어 — 리스크 1).
        /// </summary>
        public static bool ShouldForceDismount(bool structureAlive, bool occupantAlive, bool occupantConnected)
        {
            return !structureAlive || !occupantAlive || !occupantConnected;
        }

        /// <summary>건축물 Id로 점유 항목을 찾는다 — 없으면 false(= 비점유).</summary>
        public static bool TryFindByStructure(MountOccupancy[] occupancies, int structureId, out int index)
        {
            if (occupancies != null && structureId > 0)
            {
                for (int i = 0; i < occupancies.Length; i++)
                {
                    if (occupancies[i].StructureId == structureId)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// 클라이언트 Id로 점유 항목을 찾는다 — <b>한 사람은 하나만</b> 규칙의 판정면이다.
        /// 다른 무기를 새로 요청하면 호출부가 이 항목을 먼저 지운다 (경합은 서버 도착 순서로 끝난다).
        /// </summary>
        public static bool TryFindByClient(MountOccupancy[] occupancies, ulong clientId, out int index)
        {
            if (occupancies != null)
            {
                for (int i = 0; i < occupancies.Length; i++)
                {
                    if (occupancies[i].OccupantClientId == clientId)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        /// <summary>그 건축물을 그 사람이 점유 중인가 — 사격·장전 보고의 권위 검증 1단계.</summary>
        public static bool IsOccupiedBy(MountOccupancy[] occupancies, int structureId, ulong clientId)
        {
            return TryFindByStructure(occupancies, structureId, out int index)
                && occupancies[index].OccupantClientId == clientId;
        }
    }
}
