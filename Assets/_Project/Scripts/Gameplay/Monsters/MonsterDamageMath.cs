namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터 피해 산정의 순수 규칙 (M5 5차 처형) — 엔진·네트워크 무의존, EditMode 테스트 대상.
    /// 무력화(그로기) 중에는 <b>무기 종류를 보지 않고</b> 배율을 곱한다: 근접 처형과
    /// "아군 앞으로 끌어와 협동 처치"(기획서 §3.2) 두 축이 한 규칙으로 성립하고,
    /// 배율을 낮추면 협동 쪽으로 기울일 수 있다 (에셋 조정).
    /// </summary>
    public static class MonsterDamageMath
    {
        /// <summary>
        /// 실제 적용할 피해량. 그로기가 아니거나 배율이 1 이하면 원래 값 그대로다.
        /// 음수 피해는 회복이 아니므로 0으로 막는다.
        /// </summary>
        public static float ResolveDamage(float amount, bool stunned, float stunnedMultiplier)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            if (!stunned || stunnedMultiplier <= 1f)
            {
                return amount;
            }

            return amount * stunnedMultiplier;
        }
    }
}
