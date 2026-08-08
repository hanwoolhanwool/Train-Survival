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
    }
}
