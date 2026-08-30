namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 위 건축물의 종류 (M5 3차 — 건축물 종류화). 칸의 개성은 종류가 아니라
    /// 칸 위 건축물이 만든다는 M3 결정의 실현부다.
    /// 값은 <see cref="StructureEntry"/>에 byte로 직렬화되므로 한 번 배정한 값은 바꾸지 않는다
    /// (<see cref="Game.Gameplay.Inventory.ResourceType"/>과 같은 규약).
    /// </summary>
    public enum StructureKind : byte
    {
        /// <summary>온실 돔 — 그늘(더위 완화) 건축물. 통행 차단 문제로 설치 목록에서 제외
        /// (건축 개편 1차, 계획서 §1.2 — 카탈로그 설치 가능 플래그). 직렬화 규약상 값 0은 유지한다.</summary>
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

        /// <summary>
        /// 거치 기관총 — <b>사람이 붙어서</b> 쏘는 열차 화력 (M7 4차, 기획서 §7.2 포수 역할).
        /// 소유가 아니라 점유다: 내려놓고 가면 다음 사람이 쓴다. 무기 정의는 카탈로그 엔트리의
        /// <see cref="MountedWeaponSettings"/>가 든다 — 어느 종류가 무기인지를 코드가 알지 않는다.
        /// </summary>
        MountedGun = 7,

        /// <summary>
        /// 자동 터렛 — <b>사람 없이</b> 쏜다 (M7 4차 B단계). 사격 파이프라인은 거치 기관총과 같고
        /// 조작자만 AI다. 탄은 사람이 다가가 채운다 — 낮에 준비하고 밤에 소모하는 루프.
        /// </summary>
        Turret = 8,

        /// <summary>
        /// 천막 — <b>가변 크기</b> 그늘 건축물 (천막 계획 결정 ①). 사막 낮 45 ℃를 피하는 수단이고,
        /// 온실 돔(0)이 통행 차단으로 빠진 자리를 대신한다. 돔과 달리 <b>점유가 네 기둥 셀뿐</b>이라
        /// 안쪽에 다른 건축물이 들어간다 (결정 ⑥ — 카탈로그 <see cref="StructureCatalog.Entry.Occupancy"/>가 소유).
        /// 크기는 설치할 때 정해져 항목에 실리므로 카탈로그 발자국은 최소값만 든다.
        /// </summary>
        Tent = 9,
    }
}
