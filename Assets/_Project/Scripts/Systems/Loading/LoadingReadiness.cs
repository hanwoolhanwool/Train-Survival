namespace Game.Systems.Loading
{
    /// <summary>
    /// "전원이 준비됐는가" 판정 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §3.4 · §3.5 · §7.2.
    ///
    /// <para><b>왜 기다리는가</b>(§3.4): 기다리지 않으면 먼저 도착한 사람이 빈 세계에서 혼자 뛰고,
    /// 느린 PC만 첫 건축·첫 창고에서 여전히 튄다. 로딩을 만든 값이 절반으로 준다.</para>
    ///
    /// <para><b>그러나 영영 기다리지는 않는다</b>(§3.5). 멈춘 클라이언트 하나가 방을 죽이면
    /// 로딩 화면은 고장 화면이 된다 — 타임아웃 뒤에는 <b>그냥 간다.</b></para>
    ///
    /// <para>전부 순수 함수다. 네트워크도 시간도 여기서 읽지 않고 인자로 받는다.</para>
    /// </summary>
    public static class LoadingReadiness
    {
        /// <summary>단계별 대기 상한 (초). 넘으면 강제로 진행한다.</summary>
        public const float DefaultTimeoutSeconds = 20f;

        /// <summary>
        /// 도착한 보고가 <b>지금 기다리는 그 보고인가</b>.
        ///
        /// <para><b>단계를 대조하지 않으면 지연된 ① 보고가 ③ 보고로 오인된다</b>(§7.2) —
        /// 느린 클라이언트의 예고 완료 보고가 늦게 도착하면, 그것만으로 정착 단계의 대기가
        /// 풀려 <b>아직 아무것도 미리 만들지 못한 사람을 데리고 출발</b>하게 된다.</para>
        /// </summary>
        public static bool CountsAsReport(LoadingStage waitingFor, LoadingStage reported)
        {
            return waitingFor != LoadingStage.Idle && waitingFor == reported;
        }

        /// <summary>총원이 전부 보고했는가. 총원이 0이면 <b>기다릴 사람이 없으므로</b> 참이다.</summary>
        public static bool IsSatisfied(int memberCount, int reportedMemberCount)
        {
            return memberCount <= 0 || reportedMemberCount >= memberCount;
        }

        /// <summary>대기가 상한을 넘었는가. 상한이 0 이하면 기다리지 않는다는 뜻이다.</summary>
        public static bool IsTimedOut(float elapsedSeconds, float timeoutSeconds)
        {
            return timeoutSeconds <= 0f || elapsedSeconds >= timeoutSeconds;
        }

        /// <summary>
        /// 다음 단계로 넘어가도 되는가 — 전원이 보고했거나, 상한을 넘었거나.
        ///
        /// <para><b>이탈은 저절로 처리된다</b>(§3.5): 총원이 줄면 같은 보고 수로 조건이
        /// 성립하므로, 나간 사람을 지우는 별도 경로가 필요 없다.</para>
        /// </summary>
        public static bool ShouldAdvance(
            int memberCount, int reportedMemberCount, float elapsedSeconds, float timeoutSeconds)
        {
            return IsSatisfied(memberCount, reportedMemberCount)
                || IsTimedOut(elapsedSeconds, timeoutSeconds);
        }

        /// <summary>
        /// 대기 단계의 0~1 진행도 — 보고 인원 / 총원. 총원이 0이면 1이다.
        /// <b>이 값은 단계 가중치 안에서만 움직인다</b>(§4.3) — 화면에 100 %가 뜨지 않는다.
        /// </summary>
        public static float Progress(int memberCount, int reportedMemberCount)
        {
            if (memberCount <= 0)
            {
                return 1f;
            }

            if (reportedMemberCount <= 0)
            {
                return 0f;
            }

            return reportedMemberCount >= memberCount
                ? 1f
                : reportedMemberCount / (float)memberCount;
        }
    }
}
