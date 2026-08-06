using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 버린 자원의 지상 낙하 스폰 계약 (M5 3차 — 아이템 버리기). 서버 전용.
    /// 스폰 책임을 스포너(<see cref="GroundResourceSpawner"/>)에 둬 낙하 노드도
    /// 기존 컨베이어·후방 회수 목록에 등록되게 한다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface IResourceDropper
    {
        /// <summary>
        /// 자원을 열차 측면 지상에 낙하 스폰한다 — 수량만큼 노드를 산포한다(노드 1개 = 자원 1개).
        /// 버린 플레이어의 위치 근처(z)에 떨어져 집게로 다시 회수할 수 있다.
        /// 서버 전용 — 스폰이 하나도 성립하지 않으면 false(버리기 확정을 되돌린다).
        /// </summary>
        bool ServerSpawnDropped(Inventory.ResourceType type, int count, Vector3 dropOrigin);
    }
}
