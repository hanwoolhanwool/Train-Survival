using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 역 소품의 지상 스폰 계약 (기차역 2차) — 서버 전용.
    ///
    /// <para><b>왜 <see cref="IStorageBundleSpawner"/>에 얹지 않는가.</b> 그쪽은 "파괴된 창고를
    /// 회수 기회로 되돌리는" 계약이고 위치도 갑판/투척/안착이라는 <b>파괴 문맥</b>을 전제한다.
    /// 역 소품은 앵커가 정한 좌표에 그대로 놓이고(승강장 위라 <b>y를 존중해야 한다</b>) 요구 집게
    /// 등급도 종류가 정한다 — 인자가 달라 같은 메서드에 담기지 않는다.</para>
    ///
    /// <para>구현은 <see cref="GroundResourceSpawner"/>가 맡는다. 스폰만 떼어 놓으면
    /// <b>후방 회수 목록에 등록되지 않아</b> 지나간 역의 소품이 영원히 남기 때문이다.</para>
    /// </summary>
    public interface IStationPropSpawner
    {
        /// <summary>
        /// 앵커 위치에 소품을 안착 스폰한다 — 좌표를 그대로 쓰고(승강장 위 포함) 요구 집게 등급을 주입한다.
        /// 자원 노드와 같은 회수 규약(후방 회수·소실 회수)을 탄다.
        /// </summary>
        /// <param name="requiredTier">요구 집게 등급 (1~3). 0이면 프리팹 값.</param>
        bool ServerSpawnProp(HotbarSlotView[] contents, Vector3 position, int requiredTier);
    }
}
