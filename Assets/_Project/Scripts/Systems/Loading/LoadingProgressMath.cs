using UnityEngine;

namespace Game.Systems.Loading
{
    /// <summary>
    /// 단계 가중 진행률 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §4.
    ///
    /// <para><b>가중치는 이 타입만 안다.</b> 코디네이터도 화면도 전체 진행률을 직접 계산하지 않는다
    /// (§4.2) — 검사기는 도구가 갖고, 판정은 순수 함수가 소유한다.</para>
    ///
    /// <para><b>정확성은 보장하지 않는다</b>(§4.3). 보장하는 것은 둘뿐이다:</para>
    /// <list type="number">
    /// <item><description><b>단조 증가</b> — 단계가 뒤로 가도 표시값은 내려가지 않는다
    /// (<see cref="Monotonic"/>). 내려가는 진행바는 고장으로 읽힌다.</description></item>
    /// <item><description><b>100 %에서 멈추지 않는다</b> — 전원 대기 단계는 자기 상한에 묶이고,
    /// 그 뒤에 <see cref="LoadingStage.Depart"/> 몫이 남아 있어 <b>기다리는 동안 100 %가 뜨지
    /// 않는다.</b> 계획 §4.1이 "③ 전원 대기 + ④ 출발 = 0.05"로 묶은 것을 여기서 둘로 나눈
    /// 이유가 이것이다.</description></item>
    /// </list>
    ///
    /// <para><b>이 값들은 추정이다</b>(§12 미결 1번). 1차 실측이 다른 답을 내면 그대로 옮긴다 —
    /// 가중치가 실제 시간과 어긋나면 진행바가 특정 구간에서만 기어간다.</para>
    /// </summary>
    internal static class LoadingProgressMath
    {
        // ── §4.1 단계 가중치 ─────────────────────────────────────────────
        //
        // 계획 §4.1의 표를 LoadingStage 단위로 접은 값이다.
        //   ① 폰트 글리프 0.05 + 지형 타일 0.30 → Prepare 0.35
        //   ③ 건축물 0.20 + UI 워밍업 0.10      → Settle  0.30
        //   ③ 전원 대기 + ④ 출발 0.05           → WaitSettle 0.03 + Depart 0.02
        // 합계는 정확히 1이어야 한다 — LoadingProgressMathTests가 고정한다.

        /// <summary>① 예고 — 폰트 글리프와 지형 타일 프리웜.</summary>
        public const float PrepareWeight = 0.35f;

        /// <summary>① 전원 대기.</summary>
        public const float WaitPrepareWeight = 0.05f;

        /// <summary>② 씬 로드.</summary>
        public const float LoadSceneWeight = 0.25f;

        /// <summary>③ 정착 — 건축물 프리로드와 UI 워밍업.</summary>
        public const float SettleWeight = 0.30f;

        /// <summary>③ 전원 대기 — <b>여기서 100 %가 뜨면 안 된다</b>.</summary>
        public const float WaitSettleWeight = 0.03f;

        /// <summary>④ 출발 — 최소 표시 시간과 페이드 아웃 몫.</summary>
        public const float DepartWeight = 0.02f;

        /// <summary>단계가 차지하는 몫. <see cref="LoadingStage.Idle"/>·<see cref="LoadingStage.Done"/>은 0이다.</summary>
        public static float Weight(LoadingStage stage)
        {
            switch (stage)
            {
                case LoadingStage.Prepare: return PrepareWeight;
                case LoadingStage.WaitPrepare: return WaitPrepareWeight;
                case LoadingStage.LoadScene: return LoadSceneWeight;
                case LoadingStage.Settle: return SettleWeight;
                case LoadingStage.WaitSettle: return WaitSettleWeight;
                case LoadingStage.Depart: return DepartWeight;
                default: return 0f;
            }
        }

        /// <summary>
        /// 그 단계가 시작되는 지점의 전체 진행률. 앞선 단계들의 가중치 합이다.
        /// <see cref="LoadingStage.Done"/>은 정확히 1을 돌려준다 — 누산 오차가 마지막 칸을
        /// 채우지 못하는 일이 없어야 한다.
        /// </summary>
        public static float Start(LoadingStage stage)
        {
            if (stage >= LoadingStage.Done)
            {
                return 1f;
            }

            float sum = 0f;
            for (LoadingStage s = LoadingStage.Prepare; s < stage; s++)
            {
                sum += Weight(s);
            }

            return sum;
        }

        /// <summary>
        /// 단계와 그 단계 안의 0~1을 받아 전체 0~1을 돌려준다.
        /// <paramref name="stageProgress"/>가 범위를 벗어나면 접어 넣는다.
        /// </summary>
        public static float Combine(LoadingStage stage, float stageProgress)
        {
            if (stage >= LoadingStage.Done)
            {
                return 1f;
            }

            if (stage <= LoadingStage.Idle)
            {
                return 0f;
            }

            return Mathf.Clamp01(Start(stage) + Weight(stage) * Mathf.Clamp01(stageProgress));
        }

        /// <summary>
        /// 표시값의 단조 증가 보장(§4.3) — 새 값이 지금 보이는 값보다 작으면 그대로 둔다.
        /// 되돌림은 진행바가 아니라 상태 문구로 알린다.
        /// </summary>
        public static float Monotonic(float shown, float next)
        {
            float clamped = Mathf.Clamp01(next);
            return clamped > shown ? clamped : shown;
        }
    }
}
