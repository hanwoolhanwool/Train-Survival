namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 장비 착용 부위 (기획서 §6.3 — 의류/방어구, M5 3차). 값은 착용 목록의 인덱스이자
    /// RPC 페이로드이므로 한 번 배정한 값은 바꾸지 않는다.
    /// </summary>
    public enum EquipSlot : byte
    {
        Head = 0,
        Body = 1,
        Legs = 2,
        Feet = 3,
    }
}
