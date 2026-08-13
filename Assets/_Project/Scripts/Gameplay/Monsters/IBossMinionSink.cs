using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 보스 소속 개체(부하 소환·무리 호출)의 스폰·회수 계약 (M7 2차 결정 ②).
    /// 보스는 "몇 마리를 어디에 부른다"만 알고, 프리팹·풀·변종·NGO 스폰 규약은 구현이 맡는다 —
    /// 보스 개체가 스폰 파이프라인에 직접 의존하지 않게 하는 경계다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface IBossMinionSink
    {
        /// <summary>현재 살아 있는 보스 소속 개체 수.</summary>
        int ActiveMinionCount { get; }

        /// <summary>
        /// 보스 소속 개체를 스폰한다 (서버 전용). 합산 cap에 걸리면 요청보다 적게 스폰되며,
        /// <b>실제 스폰된 수</b>를 돌려준다.
        /// </summary>
        /// <param name="count">요청 마릿수.</param>
        /// <param name="origin">스폰 기준 위치 (보스 위치).</param>
        /// <param name="variantIndex">몬스터 변종 인덱스 (−1 = 프리팹 기본).</param>
        /// <param name="ownedCap">이 보스가 동시에 거느릴 수 있는 상한.</param>
        /// <param name="passThrough">통과 모드(무리 호출)로 스폰할지 — false면 일반 추격(부하 소환).</param>
        int ServerSpawnMinions(int count, Vector3 origin, int variantIndex, int ownedCap, bool passThrough);

        /// <summary>보스 소속 개체를 전부 회수한다 (보스 사망·회수 시 — 사망 처리가 아니다).</summary>
        void ServerRetreatMinions();
    }
}
