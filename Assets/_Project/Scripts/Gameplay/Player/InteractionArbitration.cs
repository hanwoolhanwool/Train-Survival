using System.Collections.Generic;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// "E키 상호작용" 후보를 내는 주체 — 한 프레임에 여럿이 성립할 수 있으므로 중재의 식별자가 된다.
    /// 열거 순서는 조준·거리가 완전히 같을 때의 최종 타이브레이크다 (결정론 보장용 — 게임 규칙 아님).
    /// </summary>
    public enum InteractionSource
    {
        None = 0,
        MountedWeapon = 1,
        Storage = 2,
        Bundle = 3,
        Crafting = 4,
        EngineFuel = 5,
    }

    /// <summary>한 프레임에 제출된 상호작용 후보 하나 — 조준 정렬도와 거리로만 겨룬다.</summary>
    public readonly struct InteractionCandidate
    {
        public readonly InteractionSource Source;

        /// <summary>
        /// 같은 주체가 실물로 여럿일 때의 구분자 (보따리처럼 인스턴스마다 컴포넌트가 붙는 경우).
        /// 주체가 하나뿐이면 0 — 창고·제작대처럼 한 컴포넌트가 여러 건축물을 대신 판정하는 쪽이다.
        /// </summary>
        public readonly int InstanceKey;

        /// <summary>카메라 전방과 대상 방향의 내적 — 1에 가까울수록 정면.</summary>
        public readonly float LookDot;

        /// <summary>플레이어 → 대상 판정 지점의 제곱 거리.</summary>
        public readonly float SqrDistance;

        public InteractionCandidate(InteractionSource source, int instanceKey, float lookDot, float sqrDistance)
        {
            Source = source;
            InstanceKey = instanceKey;
            LookDot = lookDot;
            SqrDistance = sqrDistance;
        }
    }

    /// <summary>중재로 뽑힌 초점 하나 — 주체와, 같은 주체가 여럿일 때의 인스턴스 구분자.</summary>
    public readonly struct InteractionFocus
    {
        public static readonly InteractionFocus None =
            new InteractionFocus(InteractionSource.None, 0);

        public readonly InteractionSource Source;
        public readonly int InstanceKey;

        public InteractionFocus(InteractionSource source, int instanceKey)
        {
            Source = source;
            InstanceKey = instanceKey;
        }

        public bool Matches(InteractionSource source, int instanceKey)
        {
            return Source != InteractionSource.None
                && Source == source && InstanceKey == instanceKey;
        }
    }

    /// <summary>
    /// 상호작용 대상 중재 (건축물 다중 타겟 수정) — 창고·제작대·연료 투입구·거치 무기가 서로를 모른 채
    /// 각자 안내를 띄우고 각자 E키를 소비하던 것을 하나로 좁힌다.
    /// <para>
    /// 판정은 <b>겨눈 것 하나</b>: ① 조준 정렬도가 가장 높은 후보군(임계 여유 <see cref="LookDotTieEpsilon"/>) →
    /// ② 그중 가장 가까운 것 → ③ 그래도 같으면 <see cref="InteractionSource"/> 순서.
    /// 거리부터 보면 옆에 붙은 상자를 정면으로 봐도 조금 더 가까운 작업대가 이긴다 — 그래서 조준이 먼저다.
    /// </para>
    /// 순수 함수다: 제출 순서와 무관하게 같은 입력이면 같은 승자가 나온다(2패스 — 최고 정렬도를 먼저 확정).
    /// </summary>
    public static class InteractionArbitrationLogic
    {
        /// <summary>같은 것을 겨눴다고 볼 정렬도 오차 — 이 안에서는 거리가 판정을 넘겨받는다.</summary>
        public const float LookDotTieEpsilon = 0.02f;

        /// <summary>후보 중 초점 하나 — 비었으면 <see cref="InteractionFocus.None"/>.</summary>
        public static InteractionFocus SelectFocus(IReadOnlyList<InteractionCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return InteractionFocus.None;
            }

            // 1패스: 최고 정렬도. 순서 의존을 없애려면 기준선이 먼저 확정돼야 한다
            // (근사 비교를 순차 fold로 하면 제출 순서에 따라 승자가 갈린다 — 비추이적).
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                // 빈 후보가 기준선을 올려 진짜 후보를 전부 떨어뜨리지 않게 여기서도 거른다.
                if (candidates[i].Source != InteractionSource.None && candidates[i].LookDot > bestDot)
                {
                    bestDot = candidates[i].LookDot;
                }
            }

            // 2패스: 기준선 안의 후보끼리 거리 → 종류 → 인스턴스 순으로 겨룬다.
            float threshold = bestDot - LookDotTieEpsilon;
            InteractionFocus focus = InteractionFocus.None;
            float focusSqr = float.PositiveInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                InteractionCandidate candidate = candidates[i];
                if (candidate.Source == InteractionSource.None || candidate.LookDot < threshold)
                {
                    continue;
                }

                if (Wins(candidate, focus, focusSqr))
                {
                    focus = new InteractionFocus(candidate.Source, candidate.InstanceKey);
                    focusSqr = candidate.SqrDistance;
                }
            }

            return focus;
        }

        /// <summary>후보가 현재 초점을 이기는가 — 거리 → 종류 → 인스턴스 순의 전순서(제출 순서 무관).</summary>
        private static bool Wins(InteractionCandidate candidate, InteractionFocus focus, float focusSqr)
        {
            if (focus.Source == InteractionSource.None || candidate.SqrDistance < focusSqr)
            {
                return true;
            }

            if (candidate.SqrDistance > focusSqr)
            {
                return false;
            }

            return candidate.Source != focus.Source
                ? candidate.Source < focus.Source
                : candidate.InstanceKey < focus.InstanceKey;
        }
    }
}
