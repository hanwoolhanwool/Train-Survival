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

        /// <summary>이동 요청의 컨테이너 식별 — 창고 보따리 (M5 8차).</summary>
        const byte ContainerBundle = 2;

        /// <summary>창고 하나의 슬롯 수.</summary>
        int SlotsPerStorage { get; }

        /// <summary>창고 슬롯 조회 — 복제 상태 기반이라 전 피어 동일 (UI 표시용).</summary>
        HotbarSlotView GetSlot(int carIndex, int slotIndex);

        /// <summary>
        /// 슬롯 이동 요청 (개인↔창고·창고 내 재배치) — 로컬에서 호출한다.
        /// 확정은 호스트: 창고 생존·거리 재검증 후 순수 로직으로 병합·스왑·이동을 판정한다.
        /// </summary>
        void RequestTransfer(int carIndex, byte fromContainer, int fromIndex, byte toContainer, int toIndex);

        /// <summary>보따리 슬롯 조회 (M5 8차) — 복제 상태 기반이라 전 피어 동일 (E창 표시용).</summary>
        HotbarSlotView GetBundleSlot(ulong bundleObjectId, int slotIndex);

        /// <summary>보따리 슬롯 수 — 보따리가 없으면(회수됨) 0. E창은 0이면 닫는다.</summary>
        int GetBundleSlotCount(ulong bundleObjectId);

        /// <summary>
        /// 개인↔보따리 슬롯 이동 요청 (M5 8차) — 로컬에서 호출한다. 창고 이동과 같은 확정 규약:
        /// 호스트가 보따리 생존(회수·운반 중 기각)·거리를 재검증하고 순수 로직으로 판정한다.
        /// </summary>
        void RequestBundleTransfer(ulong bundleObjectId, byte fromContainer, int fromIndex, byte toContainer, int toIndex);

        /// <summary>
        /// 창고 내용물 소실 — 서버 전용. 슬롯 재건(안전망) 확정 지점에서 호출된다
        /// ("칸 위 건축물은 칸과 운명을 같이한다" — 이탈 중에는 호출되지 않아 재결합 시 보존).
        /// </summary>
        void ServerClearStorage(int carIndex);

        /// <summary>
        /// 창고 내용물을 보따리로 내놓고 비운다 (M5 8차) — 서버 전용. 파괴 확정 지점에서 호출된다.
        /// deckAlive = 칸(갑판) 생존 여부 — true(건축물만 파괴)면 갑판 휴지 스폰, false(칸 파괴)면
        /// 지상 낙하. 내용물이 전부 비었거나 스포너가 없으면 보따리 없이 비우기만 한다.
        /// 슬롯 재건 안전망은 이 경로가 아니라 <see cref="ServerClearStorage"/>다 — 이중 생성 방지.
        /// </summary>
        void ServerDropStorageAsBundle(int carIndex, bool deckAlive);
    }
}
