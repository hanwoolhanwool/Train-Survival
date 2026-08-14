namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 자원의 종류 — 네트워크 식별자 (기획서 §4 지역별 자원, §6.1 탄약 제작).
    /// 값은 <see cref="Game.Gameplay.Inventory"/>의 NetworkSlot에 byte로 직렬화되므로 한 번 배정한 값은 바꾸지 않는다.
    /// 표시명·스택 상한·발열량 등 데이터는 <see cref="ResourceCatalog"/>가 담당한다.
    /// 0~15 = 원자재(채집), 16~ = 제작품 대역.
    /// </summary>
    public enum ResourceType : byte
    {
        None = 0,

        /// <summary>목재 — 숲. 고발열 연료 겸 건자재.</summary>
        Wood = 1,

        /// <summary>돌 — 숲. 저발열, 건자재.</summary>
        Stone = 2,

        /// <summary>고철 — 사막 (모래에 묻힌 난파 열차). 탄약·무기 제작 재료.</summary>
        Scrap = 3,

        /// <summary>화약 원료 — 사막 (초석·유황 통합). 연료 투입 불가, 탄약 전용.</summary>
        Niter = 4,

        /// <summary>식재료 — 숲·사막 공용 (M5 4차 요리·허기). 화덕 요리 재료, 연료·건자재 불가.</summary>
        RawFood = 5,

        /// <summary>
        /// 원목 — 숲 (M5 5차 상위 자원, 무게 2). 목재의 2배 발열 + 건자재.
        /// 2단계 집게가 있어야 낚아챌 수 있다 — "2단계를 만들면 연료 문제가 풀린다".
        /// </summary>
        Timber = 6,

        /// <summary>
        /// 광맥 — 사막 (M5 5차 상위 자원, 무게 3). 연료·건자재 불가, 탄약 대량 제작 전용.
        /// 3단계 집게가 있어야 낚아챌 수 있다 — "3단계를 만들면 탄약 경제가 풀린다".
        /// </summary>
        OreVein = 7,

        /// <summary>벼 — 대초원 (M7 1차, 기획서 §4.3). 비연료·비건자재, 밥 요리 재료.</summary>
        Rice = 8,

        /// <summary>소금 — 사막 (M7 1차 소급, 기획서 §4.2). 비연료·비건자재, 보존식 재료.</summary>
        Salt = 9,

        /// <summary>
        /// 보스 핵 — 지역 보스 처치 보상 (M7 2차 결정 ③). 비연료·비건자재.
        /// 채집으로는 얻을 수 없는 유일한 자연 대역 자원이다 — 소비처는 3~5차에서 연결한다.
        /// </summary>
        BossCore = 10,

        /// <summary>얼음 — 북극 (M7 3차, 기획서 §4.4). 비연료·비건자재. 정수 지점에서 식수가 된다.</summary>
        Ice = 11,

        /// <summary>희귀 금속 — 북극 유적 (M7 3차). 비연료·비건자재. 방한 세트 4부위의 공통 재료다.</summary>
        RareMetal = 12,

        /// <summary>
        /// 유적 부품 — 북극 유적 (M7 3차 결정 ④). 비연료·비건자재, 스택 5로 희소.
        /// 보스 핵과 같은 규약으로 획득·보관까지만 성립하고, 소비처(궤도 도약 기관)는 5차에서 연결한다.
        /// </summary>
        RelicPart = 13,

        /// <summary>권총탄 — 제작품 (고철 + 화약 원료).</summary>
        RevolverAmmo = 16,

        /// <summary>산탄 — 제작품. 샷건 전용 (기획서 §6.2 탄약 3종).</summary>
        ShotgunAmmo = 17,

        /// <summary>소총탄 — 제작품. 볼트액션 전용 (기획서 §6.2 탄약 3종).</summary>
        RifleAmmo = 18,

        /// <summary>구운 식사 — 화덕 요리 (M5 4차). 허기 회복 + 재생 버프. 효과는 FoodCatalog가 정한다.</summary>
        CookedMeal = 19,

        /// <summary>든든한 스튜 — 화덕 요리 (M5 4차). 허기 회복 대 + 소량 재생 + 보온 버프.</summary>
        HeartyStew = 20,

        /// <summary>밥 — 화덕 요리 (M7 1차, 기획서 §7.3). 벼 기반, 허기 회복 중상 + 낮은 재생.</summary>
        CookedRice = 21,

        /// <summary>보존식 — 화덕 요리 (M7 1차, 기획서 §4.3 소금 절임). 고스택 비축형 — 북극행 식량.</summary>
        PreservedMeal = 22,

        /// <summary>
        /// 식수 — 정수 지점 제작품 (M7 3차 결정 ①, 기획서 §4.4 "얼음은 정화 장치로 식수화").
        /// 갈증 스탯을 만들지 않고 <b>요리 재료</b>로 닫는다 — 북극 보온식의 필수 재료다.
        /// </summary>
        PurifiedWater = 23,

        /// <summary>온차 — 화덕 요리 (M7 3차). 식수 기반 북극 보온식 — 허기 소량 + 즉시 체온 + 보온 대.</summary>
        WarmingTea = 24,
    }
}
