using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 상호작용 초점의 프레임 단위 중재자 (건축물 다중 타겟 수정) — 창고·제작대·연료 투입구·거치 무기가
    /// 각자 안내를 띄우고 각자 E키를 소비하던 것을, <b>겨눈 것 하나</b>로 좁힌다.
    /// <para>
    /// 사용 규약: 후보가 성립한 프레임마다 <see cref="Submit(InteractionSource, float, float)"/>하고,
    /// 안내 표시와 E키 처리는 <see cref="IsFocused(InteractionSource)"/>가 참일 때만 한다.
    /// 창을 연 주체는 <see cref="Capture(InteractionSource)"/>로 초점을 붙잡고 닫을 때
    /// <see cref="Release(InteractionSource)"/>한다 — 열린 창 위로 남의 안내가 겹치지 않게 하는 장치다.
    /// </para>
    /// <para>
    /// 초점은 <b>직전 프레임</b>의 제출분으로 확정한다. Update 실행 순서는 보장되지 않으므로,
    /// 같은 프레임에 제출하고 같은 프레임에 묻는 구조는 순서에 따라 답이 갈린다. 한 프레임(≈16 ms) 늦는
    /// 대신 누가 먼저 도느냐와 무관하게 모두가 같은 답을 받는다.
    /// </para>
    /// 로컬 표시·입력 판정 전용 — 서버 재검증은 각 호출부가 자기 기준점으로 그대로 수행한다.
    /// </summary>
    public static class InteractionArbiter
    {
        private static readonly List<InteractionCandidate> Pending = new List<InteractionCandidate>(8);

        private static int _pendingFrame = int.MinValue;
        private static InteractionFocus _focus = InteractionFocus.None;
        private static InteractionFocus _captured = InteractionFocus.None;
        private static bool _hasCapture;

        /// <summary>이번 프레임의 초점 — 붙잡은 주체가 있으면 그쪽이다.</summary>
        public static InteractionFocus Focus
        {
            get
            {
                Advance(Time.frameCount);
                return _hasCapture ? _captured : _focus;
            }
        }

        /// <summary>후보 제출 (주체가 하나뿐인 경우) — 근접·시선 판정을 통과한 프레임에만 부른다.</summary>
        public static void Submit(InteractionSource source, float lookDot, float sqrDistance)
        {
            Submit(Time.frameCount, source, 0, lookDot, sqrDistance);
        }

        /// <summary>후보 제출 (같은 주체가 실물로 여럿인 경우) — 인스턴스 구분자를 함께 낸다.</summary>
        public static void Submit(InteractionSource source, int instanceKey, float lookDot, float sqrDistance)
        {
            Submit(Time.frameCount, source, instanceKey, lookDot, sqrDistance);
        }

        /// <summary>이 주체가 이번 프레임의 초점인지 — 안내 표시와 E키 처리의 유일한 관문.</summary>
        public static bool IsFocused(InteractionSource source)
        {
            return IsFocused(source, 0);
        }

        /// <summary>이 인스턴스가 이번 프레임의 초점인지.</summary>
        public static bool IsFocused(InteractionSource source, int instanceKey)
        {
            return IsFocused(Time.frameCount, source, instanceKey);
        }

        /// <summary>
        /// 초점 독점 — 창을 열거나 거치 무기에 붙은 주체가 부른다. 붙잡은 동안 다른 후보는 초점을 얻지 못한다.
        /// 이미 다른 주체가 붙잡고 있으면 빼앗지 않는다 (먼저 연 창이 우선).
        /// </summary>
        public static void Capture(InteractionSource source)
        {
            Capture(source, 0);
        }

        /// <summary>초점 독점 (인스턴스 지정).</summary>
        public static void Capture(InteractionSource source, int instanceKey)
        {
            if (source != InteractionSource.None && !_hasCapture)
            {
                _captured = new InteractionFocus(source, instanceKey);
                _hasCapture = true;
            }
        }

        /// <summary>독점 해제 — 자기가 붙잡고 있을 때만 풀린다 (남의 독점을 대신 풀지 않는다).</summary>
        public static void Release(InteractionSource source)
        {
            Release(source, 0);
        }

        /// <summary>독점 해제 (인스턴스 지정).</summary>
        public static void Release(InteractionSource source, int instanceKey)
        {
            if (_hasCapture && _captured.Matches(source, instanceKey))
            {
                _captured = InteractionFocus.None;
                _hasCapture = false;
            }
        }

        /// <summary>씬 진입·세션 종료 시 초기화 — 정적 상태가 다음 판까지 남지 않게 한다.</summary>
        public static void Reset()
        {
            Pending.Clear();
            _pendingFrame = int.MinValue;
            _focus = InteractionFocus.None;
            _captured = InteractionFocus.None;
            _hasCapture = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Reset();
        }

        internal static void Submit(
            int frame, InteractionSource source, int instanceKey, float lookDot, float sqrDistance)
        {
            if (source == InteractionSource.None)
            {
                return;
            }

            Advance(frame);

            // 같은 인스턴스가 한 프레임에 두 번 내면 더 잘 겨눈 쪽만 남긴다 — 후보 목록은 인스턴스당 하나다.
            for (int i = 0; i < Pending.Count; i++)
            {
                if (Pending[i].Source != source || Pending[i].InstanceKey != instanceKey)
                {
                    continue;
                }

                if (lookDot > Pending[i].LookDot)
                {
                    Pending[i] = new InteractionCandidate(source, instanceKey, lookDot, sqrDistance);
                }

                return;
            }

            Pending.Add(new InteractionCandidate(source, instanceKey, lookDot, sqrDistance));
        }

        internal static bool IsFocused(int frame, InteractionSource source, int instanceKey)
        {
            if (source == InteractionSource.None)
            {
                return false;
            }

            Advance(frame);
            return (_hasCapture ? _captured : _focus).Matches(source, instanceKey);
        }

        /// <summary>프레임이 넘어갔으면 직전 프레임 제출분으로 초점을 확정하고 목록을 비운다.</summary>
        private static void Advance(int frame)
        {
            if (_pendingFrame == frame)
            {
                return;
            }

            // 프레임을 건너뛰었다면(정지·씬 전환) 남은 제출분은 이미 지난 판정이다 — 그래도 마지막 것으로 확정하고 비운다.
            _focus = InteractionArbitrationLogic.SelectFocus(Pending);
            Pending.Clear();
            _pendingFrame = frame;
        }
    }
}
