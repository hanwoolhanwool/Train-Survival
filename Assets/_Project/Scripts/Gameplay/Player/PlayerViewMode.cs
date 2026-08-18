namespace Game.Gameplay.Player
{
    /// <summary>
    /// 소유자 화면의 시점 표현 방식 (1인칭 통합 시점 전환 계획 §3.1) — <b>로컬 표현 선택</b>이며
    /// 복제하지 않는다 (기술 확정 ⑥). 판정·복제·원격 표현은 두 모드가 완전히 공유하므로,
    /// 이 값이 바뀔 때 달라지는 것은 <b>그 피어의 화면뿐</b>이다 (§4.2 — QA 비교의 기준선).
    /// </summary>
    public enum PlayerViewMode : byte
    {
        /// <summary>
        /// FP/TP 분리 (현행) — 소유자는 카메라에 붙은 뷰모델을 보고, 자기 몸·손 무기는
        /// 그림자만 남긴다. 원격 피어는 손 소켓의 TP 월드모델을 본다.
        /// </summary>
        SplitFpTp = 0,

        /// <summary>
        /// 통합 1인칭 — 소유자가 자기 몸과 손에 쥔 무기를 그대로 본다. 화면 전용 뷰모델은 없다.
        /// </summary>
        UnifiedFirstPerson = 1,
    }
}
