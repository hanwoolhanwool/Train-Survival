namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸·건축물 건설 계약 (개발 가이드 §M3 — 칸 증설/연결, 기획서 §7.1).
    /// 칸은 첫 빈 슬롯(파괴·소실)을 재건하고, 빈 슬롯이 없으면 후미에 증설한다(상한 = 씬 예비 슬롯 수).
    /// 건축물은 살아 붙은 확장 칸 위에 1개씩 설치한다 (자유 다중 설치는 M5 확장).
    /// 판정(Can*)은 복제 상태 기반이라 전 피어에서 동일하고, 확정(ServerTry*)은 호스트 전용이다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainExpansion
    {
        /// <summary>편성 상한 — 씬에 미리 확보된 예비 슬롯 수까지만 늘릴 수 있다.</summary>
        int MaxCarCount { get; }

        /// <summary>칸 1칸 건설(재건·증설 공통)에 드는 자원 수.</summary>
        int CarBuildCost { get; }

        /// <summary>건축물 1개 설치에 드는 자원 수.</summary>
        int StructureBuildCost { get; }

        /// <summary>지금 칸을 지을 수 있는지 — 재건할 빈 슬롯이 있거나 후미 증설 여유가 있어야 한다.</summary>
        bool CanBuildCar();

        /// <summary>
        /// 칸 1개를 짓는다 — 첫 빈 슬롯이면 그 자리 재건(앞 연결부 복구 포함), 없으면 후미 증설.
        /// 서버 전용 — 클라이언트 호출은 항상 false.
        /// </summary>
        bool ServerTryBuildCar();

        /// <summary>이 칸 위에 건축물을 설치할 수 있는지 — 칸 건재 + 살아 있는 건축물 없음(기관차 제외).</summary>
        bool CanBuildStructure(int carIndex);

        /// <summary>칸 위에 건축물 1개를 설치한다. 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryBuildStructure(int carIndex);
    }
}
