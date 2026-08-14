namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 위 건축물의 종류 (M5 3차 — StructureState 종류화). 칸의 개성은 종류가 아니라
    /// 칸 위 건축물이 만든다는 M3 결정의 실현부다.
    /// 값은 <see cref="StructureState"/>에 byte로 직렬화되므로 한 번 배정한 값은 바꾸지 않는다
    /// (<see cref="Game.Gameplay.Inventory.ResourceType"/>과 같은 규약).
    /// </summary>
    public enum StructureKind : byte
    {
        /// <summary>온실 돔 — 그늘(더위 완화)을 제공하는 기본 건축물. default(StructureState)와 일치하도록 0.</summary>
        Dome = 0,

        /// <summary>난방기 — 추위를 완화한다 (사막 밤·M7 북극 대비).</summary>
        Heater = 1,

        /// <summary>공유 창고 — 팀 공용 저장고 표면. 저장 슬롯은 TrainStorage가 소유한다.</summary>
        Storage = 2,

        /// <summary>제작대 — 제작 상호작용 지점. 레시피·확정 경로는 CraftingStation을 재사용한다.</summary>
        Workbench = 3,

        /// <summary>화덕 — 요리 상호작용 지점 (M5 4차). 난방 없음 — 건축물 1종 = 역할 1개 계약 유지.</summary>
        Campfire = 4,

        /// <summary>
        /// 정수기 — 얼음을 식수로 바꾸는 제작 지점 (M7 3차 결정 ①, 기획서 §4.4).
        /// 제작 지점으로 구현하므로 레시피 경로를 그대로 재사용한다 — 난방 없음.
        /// </summary>
        Purifier = 5,

        /// <summary>
        /// 강화 난방로 — 난방 제공 + <b>열차 연료를 태운다</b> (M7 3차 결정 ③-ⓑ).
        /// 연료가 남아 있는 동안 지역 한파 페널티가 0이 되어 북극에서도 완전한 안전지대이고,
        /// 떨어지면 일반 난방기와 같아진다 (별도 고장 상태 없음).
        /// </summary>
        Furnace = 6,
    }
}
