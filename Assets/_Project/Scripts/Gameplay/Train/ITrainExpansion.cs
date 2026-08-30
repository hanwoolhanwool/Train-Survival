namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸·건축물 건설 계약 (개발 가이드 §M3 — 칸 증설/연결, 기획서 §7.1).
    /// 칸은 첫 빈 슬롯(파괴·소실)을 재건하고, 빈 슬롯이 없으면 후미에 증설한다(상한 = 씬 예비 슬롯 수).
    /// 건축물은 칸 바닥 그리드의 빈 셀 묶음 위에 다중 설치한다 (건축 개편 1차 — 칸당 1슬롯 폐지).
    /// 판정(Can*)은 복제 상태 기반이라 전 피어에서 동일하고, 확정(ServerTry*)은 호스트 전용이다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainExpansion
    {
        /// <summary>편성 상한 — 씬에 미리 확보된 예비 슬롯 수까지만 늘릴 수 있다.</summary>
        int MaxCarCount { get; }

        /// <summary>칸 1칸 건설(재건·증설 공통)에 드는 자원 수.</summary>
        int CarBuildCost { get; }

        /// <summary>이 종류의 건축물 1개 설치에 드는 자원 수 — 종류별 비용은 StructureCatalog가 정한다.</summary>
        int GetStructureBuildCost(StructureKind kind);

        /// <summary>
        /// 다음 건설이 들어갈 슬롯 — 재건할 첫 빈 슬롯, 없으면 후미 증설 슬롯.
        /// 복제 상태 기반이라 전 피어 동일 — 건설 조준·프리뷰가 위치를 계산하는 데 쓴다. 지을 수 없으면 false.
        /// </summary>
        bool TryGetBuildSlot(out int slotIndex);

        /// <summary>
        /// 칸 1개를 짓는다 — 첫 빈 슬롯이면 그 자리 재건(앞 연결부 복구 포함), 없으면 후미 증설.
        /// 서버 전용 — 클라이언트 호출은 항상 false.
        /// </summary>
        bool ServerTryBuildCar();

        /// <summary>
        /// 이 자리(칸·셀·회전)에 건축물을 설치할 수 있는지 (건축 개편 1차 — 계획서 §2.3) —
        /// 칸 건재(기관차 제외) + 설치 가능 종류 + 점유 셀 전부 그리드 내부·비점유.
        /// 복제 상태 기반이라 전 피어 동일 — 프리뷰와 호스트 확정이 같은 판정을 쓴다.
        /// </summary>
        bool CanPlaceStructure(int carIndex, int cellX, int cellZ, int rotation, StructureKind kind);

        /// <summary>지정 자리에 건축물 1개를 설치한다. 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryBuildStructure(int carIndex, int cellX, int cellZ, int rotation, StructureKind kind);

        /// <summary>
        /// 크기를 지정한 설치 판정 (천막 계획 §4.2) — 가변 크기 종류는 카탈로그 발자국이 최소값이라
        /// 드래그가 정한 크기로 묻는다. 고정 크기 종류에 쓰면 카탈로그 값과 같은 값을 넘기면 된다.
        /// </summary>
        bool CanPlaceStructureSized(int carIndex, int cellX, int cellZ, int rotation,
            StructureKind kind, int width, int length);

        /// <summary>크기를 지정해 건축물 1채를 설치한다. 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryBuildStructureSized(int carIndex, int cellX, int cellZ, int rotation,
            StructureKind kind, int width, int length);

        /// <summary>
        /// 이 종류의 건축물 철거 시 반환되는 자원 수 (건축 개편 2차 — 결정 ⑤) —
        /// floor(건설 비용 × 반환 비율). 자원 종류는 StructureCatalog가 정한다.
        /// </summary>
        int GetStructureDemolishRefund(StructureKind kind);

        /// <summary>
        /// 건축물 하나를 철거한다 (건축 개편 2차 — 결정 ④) — 창고면 내용물을 보따리로 배출한 뒤
        /// 항목을 제거한다. 반환 자원 지급은 호출부(망치 RPC)의 몫이다 — 제거된 항목을 돌려준다.
        /// 서버 전용 — 클라이언트 호출은 항상 false.
        /// </summary>
        bool ServerTryDemolishStructure(int structureId, out StructureEntry removed);

        // ── 판자 증축 (건축 개편 3차 — 결정 ⑥: 셀 열 단위) ──────────────────

        /// <summary>판자 1열 건설에 드는 자원 수.</summary>
        int PlankBuildCost { get; }

        /// <summary>판자 1열 철거 시 반환되는 자원 수 — floor(판자 비용 × 반환 비율).</summary>
        int PlankDemolishRefund { get; }

        /// <summary>판자 철거 반환 자원 종류 — 건축물의 종류별 반환 자원(StructureCatalog)에 대응하는 판자 몫.</summary>
        Game.Gameplay.Inventory.ResourceType PlankRefundResource { get; }

        /// <summary>
        /// 이 칸 이 쪽에 판자 1열을 더 붙일 수 있는지 — 칸 건재(기관차 제외) + 상한 미만.
        /// 복제 상태 기반이라 전 피어 동일 — 프리뷰와 호스트 확정이 같은 판정을 쓴다.
        /// </summary>
        bool CanBuildPlank(int carIndex, PlankSide side);

        /// <summary>판자 1열을 붙인다. 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryBuildPlank(int carIndex, PlankSide side);

        /// <summary>
        /// 이 칸 이 쪽 가장 바깥 판자 1열을 뜯을 수 있는지 — 그 열 위에 건축물이 있으면 기각
        /// (계획서 §2.9). 복제 상태 기반이라 전 피어 동일.
        /// </summary>
        bool CanRemovePlank(int carIndex, PlankSide side);

        /// <summary>판자 1열을 뜯는다. 반환 자원 지급은 호출부(망치 RPC)의 몫이다.
        /// 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryRemovePlank(int carIndex, PlankSide side);
    }
}
