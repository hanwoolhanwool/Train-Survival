namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 피격 계약 — 데미지 적용·사망 확정은 호스트 권위 (권위 분담표).
    /// 공격 주체는 구체 타입이 아니라 이 인터페이스에 의존한다 (DIP).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>살아 있는 대상만 피격 대상이 된다.</summary>
        bool IsAlive { get; }

        /// <summary>데미지를 적용한다. 서버 전용 — 클라이언트 호출은 무시된다.</summary>
        void ApplyDamage(float amount, ulong instigatorClientId);
    }
}
