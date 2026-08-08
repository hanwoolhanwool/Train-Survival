namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터 무력화(그로기) 상태 조회 계약 (M5 5차). 그로기의 <b>소유자</b>는 그랩 관심사
    /// (<see cref="MonsterGrabTarget"/>)이고, 체력은 "그로기인가"만 알면 처형 배율을 적용할 수 있다 —
    /// <see cref="MonsterHealth"/>가 그랩을 알 필요가 없도록 이 최소 표면만 노출한다 (ISP).
    /// 복제 값 기반이라 전 피어가 같은 값을 본다.
    /// </summary>
    public interface IMonsterStun
    {
        /// <summary>지금 무력화(그로기) 상태인가.</summary>
        bool IsStunned { get; }
    }
}
