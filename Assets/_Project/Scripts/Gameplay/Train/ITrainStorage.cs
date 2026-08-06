using Game.Gameplay.Inventory;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 공유 창고 계약 (M5 3차 — 창고 건축물). 창고 건축물(<see cref="StructureKind.Storage"/>)마다
    /// 독립된 저장 슬롯 세트를 가지며, 팀 전원이 접근한다. 슬롯의 진실은 호스트 권위
    /// (<see cref="TrainStorage"/>의 NetworkList)다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainStorage
    {
        /// <summary>이동 요청의 컨테이너 식별 — 개인 인벤토리.</summary>
        const byte ContainerInventory = 0;

        /// <summary>이동 요청의 컨테이너 식별 — 창고.</summary>
        const byte ContainerStorage = 1;

        /// <summary>창고 하나의 슬롯 수.</summary>
        int SlotsPerStorage { get; }

        /// <summary>창고 슬롯 조회 — 복제 상태 기반이라 전 피어 동일 (UI 표시용).</summary>
        HotbarSlotView GetSlot(int carIndex, int slotIndex);

        /// <summary>
        /// 슬롯 이동 요청 (개인↔창고·창고 내 재배치) — 로컬에서 호출한다.
        /// 확정은 호스트: 창고 생존·거리 재검증 후 순수 로직으로 병합·스왑·이동을 판정한다.
        /// </summary>
        void RequestTransfer(int carIndex, byte fromContainer, int fromIndex, byte toContainer, int toIndex);

        /// <summary>
        /// 창고 내용물 소실 — 서버 전용. 건축물 파괴·칸 재건 확정 지점에서 호출된다
        /// ("칸 위 건축물은 칸과 운명을 같이한다" — 이탈 중에는 호출되지 않아 재결합 시 보존).
        /// </summary>
        void ServerClearStorage(int carIndex);
    }
}
