using Game.Gameplay.Inventory;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 창고 보따리 스폰 계약 (M5 8차) — 서버 전용. 파괴 확정 지점(<see cref="Train.TrainStorage"/>)이
    /// 내용물 스냅샷과 위치 인자를 넘기면, 스포너가 프리팹 스폰과 회수 목록 등록
    /// (소실 칸 회수·후방 회수 — 자원 노드와 같은 규약)을 담당한다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface IStorageBundleSpawner
    {
        /// <summary>건축물 파괴 (칸 생존) — 그 칸 갑판 위 휴지 상태로 스폰한다 (위치 = 칸 중심 + 갑판 높이).</summary>
        bool ServerSpawnDeckResting(HotbarSlotView[] contents, int carIndex, float deckHeight, float carCenterZ, float ejectOffset);

        /// <summary>칸 파괴 — 그 자리 지상(선로변)으로 낙하 스폰한다 (월드 프레임 소속).</summary>
        bool ServerSpawnOnGround(HotbarSlotView[] contents, float originZ);
    }
}
