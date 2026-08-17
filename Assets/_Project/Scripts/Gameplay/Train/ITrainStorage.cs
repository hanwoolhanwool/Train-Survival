using Game.Gameplay.Inventory;

namespace Game.Gameplay.Train
{
    /// <summary>블록 해제 시 내용물 처리 방식 (건축 개편 2차 — §2.8).</summary>
    public enum StorageReleaseMode : byte
    {
        /// <summary>소실 — 보따리 없이 비운다 (소실 칸 자리 재건의 잔해 제거).</summary>
        Discard = 0,

        /// <summary>건축물 파괴·철거 (칸 생존) — 그 자리 갑판 위 휴지 보따리로 배출.</summary>
        DeckBundle = 1,

        /// <summary>칸 파괴 — 지상 선로변으로 투척 배출.</summary>
        GroundBundle = 2,
    }

    /// <summary>
    /// 공유 창고 계약 (M5 3차 — 창고 건축물, 건축 개편 2차 — 저장 블록 = 건축물 Id).
    /// 창고 건축물(<see cref="StructureKind.Storage"/>)마다 독립 저장 블록을 가지며(다중 설치
    /// 허용 — 결정 ⑦), 팀 전원이 접근한다. 식별은 그리드 항목의 서버 발급 Id다 — 칸
    /// 이탈·재결합과 무관하게 블록이 보존된다. 슬롯의 진실은 호스트 권위
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

        /// <summary>창고 슬롯 조회 (식별 = 건축물 Id) — 복제 상태 기반이라 전 피어 동일 (UI 표시용).</summary>
        HotbarSlotView GetSlot(int storageId, int slotIndex);

        /// <summary>
        /// 슬롯 이동 요청 (개인↔창고·창고 내 재배치) — 로컬에서 호출한다.
        /// 확정은 호스트: 창고 생존·거리 재검증 후 순수 로직으로 병합·스왑·이동을 판정한다.
        /// </summary>
        void RequestTransfer(int storageId, byte fromContainer, int fromIndex, byte toContainer, int toIndex);

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
        /// 창고 설치 확정 시 저장 블록을 할당한다 (건축 개편 2차 — §2.8) — 서버 전용.
        /// 설치 확정 지점(<see cref="TrainState"/>)에서 명시 호출한다. 이미 있으면 무시.
        /// </summary>
        void ServerAllocateBlock(int storageId);

        /// <summary>
        /// 저장 블록을 해제한다 (건축 개편 2차 — §2.8) — 서버 전용. 파괴·철거·잔해 제거 확정
        /// 지점에서 명시 호출한다 (이벤트 구독이 아닌 직접 호출 — 누락이 코드 리뷰에서 드러난다).
        /// 보따리 배출 모드면 내용물이 있을 때 배출 후 swap-remove로 비운다 — 배출 위치는
        /// 그리드 항목의 점유 중심이므로 <b>항목이 아직 리스트에 있을 때</b> 호출해야 한다.
        /// 이탈은 해제가 아니다 — 블록이 Id로 남아 재결합 시 그대로 보존된다.
        /// </summary>
        void ServerReleaseBlock(int storageId, StorageReleaseMode mode);
    }
}
