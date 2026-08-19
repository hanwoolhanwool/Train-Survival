namespace Game.Gameplay.Harpoon
{
    /// <summary>그랩 유지 중 무기 슬롯을 바꾸려 할 때의 판정 (집게 단계별 파지 계획 §3.2).</summary>
    public enum SwitchOutcome
    {
        /// <summary>그대로 전환한다.</summary>
        Allow,

        /// <summary>전환 자체를 막는다 — 1단계는 잡는 동안 오른손이 묶인다.</summary>
        Deny,

        /// <summary>잡은 것을 놓고 나서 전환한다 — 2·3단계가 양손 무기를 고른 경우.</summary>
        ReleaseThenAllow
    }

    /// <summary>
    /// 그랩 유지 중 무기 전환 게이트의 순수 규칙 (기획서 §3.1 — 집게 등급이 "어느 손으로 드는가"를 바꾼다).
    /// <para>
    /// 1단계는 <b>오른손을 점유</b>하므로 잡은 동안 아무 무기도 들 수 없고, 2단계부터는 집게가 왼손으로
    /// 옮겨가 오른손 한손 무기를 함께 쓸 수 있다. 양손 무기는 두 손을 다 요구하므로 그랩이 풀린다.
    /// </para>
    /// <b>이 규칙은 M5 6차 2차의 "무기를 바꿔도 파지는 유지된다"를 부분 번복한다</b> — 자유 전환은
    /// 등급의 보상으로 재배치됐다. 판정은 소유자 로컬에서 끝난다(상태·등급 모두 로컬에 있다).
    /// 네트워크·엔진 무의존이라 EditMode 테스트로 전수 검증한다.
    /// </summary>
    public static class HarpoonSwitchRules
    {
        /// <summary>집게가 왼손으로 옮겨가 오른손이 풀리는 등급 — 2단계부터.</summary>
        public const int HandFreeingTier = 2;

        /// <summary>
        /// 그랩을 붙들고 있는 상태인가 — 승인 대기·릴 감기·파지. <see cref="HarpoonState.Firing"/>은
        /// 아직 잡은 것이 없으므로 포함하지 않는다(명중 전 전환은 기존 규약대로 흘러간다).
        /// </summary>
        public static bool IsGrabHeld(HarpoonState state)
        {
            return state == HarpoonState.PendingGrab
                || state == HarpoonState.Reeling
                || state == HarpoonState.Holding;
        }

        /// <summary>
        /// 전환 판정. <paramref name="harpoonTier"/>는 집게 등급(1~), <paramref name="targetIsTwoHanded"/>는
        /// 고르려는 슬롯이 양손 무기인지 (자원·빈 칸은 false).
        /// </summary>
        public static SwitchOutcome Evaluate(HarpoonState state, int harpoonTier, bool targetIsTwoHanded)
        {
            if (!IsGrabHeld(state))
            {
                return SwitchOutcome.Allow;
            }

            if (harpoonTier < HandFreeingTier)
            {
                return SwitchOutcome.Deny;
            }

            return targetIsTwoHanded ? SwitchOutcome.ReleaseThenAllow : SwitchOutcome.Allow;
        }
    }
}
