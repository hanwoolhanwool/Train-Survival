namespace Game.Gameplay.Player
{
    /// <summary>
    /// 플레이어 이동 권위 상태 (네트워크 문서 §4.2 개입 규약).
    /// 전환은 호스트가 확정하고 NetworkVariable로 전파한다.
    /// 슬라이스에는 전환 콘텐츠가 없지만 구조를 먼저 깐다 (개발 가이드 M1 지침).
    /// </summary>
    public enum PlayerMovementState : byte
    {
        /// <summary>소유자 권위 — 평상시.</summary>
        Normal = 0,

        /// <summary>호스트 구동 — 몬스터에게 붙잡혀 끌려가는 중 (구속형).</summary>
        Grabbed = 1,

        /// <summary>호스트 구동 — 처형·구출 연출 등 운반 상태 (구속형).</summary>
        Carried = 2
    }
}
