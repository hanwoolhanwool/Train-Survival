using System.Collections.Generic;
using Game.Gameplay.Player;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 상호작용 대상 중재 검증 (건축물 다중 타겟 수정) — 상자와 작업대가 나란히 있을 때
    /// 안내와 E키가 <b>겨눈 것 하나</b>에만 가는지, 그 판정이 제출 순서와 무관한지를 본다.
    /// </summary>
    public sealed class InteractionArbitrationTests
    {
        private static List<InteractionCandidate> Candidates(params InteractionCandidate[] items)
        {
            return new List<InteractionCandidate>(items);
        }

        private static InteractionCandidate Candidate(
            InteractionSource source, float lookDot, float sqrDistance, int instanceKey = 0)
        {
            return new InteractionCandidate(source, instanceKey, lookDot, sqrDistance);
        }

        [Test]
        public void SelectFocus_NoCandidates_ReturnsNone()
        {
            Assert.AreEqual(InteractionSource.None,
                InteractionArbitrationLogic.SelectFocus(Candidates()).Source);
            Assert.AreEqual(InteractionSource.None,
                InteractionArbitrationLogic.SelectFocus(null).Source);
        }

        [Test]
        public void SelectFocus_SingleCandidate_Wins()
        {
            InteractionFocus focus = InteractionArbitrationLogic.SelectFocus(
                Candidates(Candidate(InteractionSource.Storage, 0.9f, 4f)));

            Assert.AreEqual(InteractionSource.Storage, focus.Source);
        }

        /// <summary>사용자 신고 상황 — 상자와 작업대가 동시에 성립해도 초점은 하나뿐이다.</summary>
        [Test]
        public void SelectFocus_StorageAndCrafting_PicksAimedOne()
        {
            // 작업대가 더 가깝지만, 정면으로 겨눈 것은 상자다.
            InteractionFocus focus = InteractionArbitrationLogic.SelectFocus(Candidates(
                Candidate(InteractionSource.Crafting, 0.82f, 2f),
                Candidate(InteractionSource.Storage, 0.99f, 6f)));

            Assert.AreEqual(InteractionSource.Storage, focus.Source);
        }

        [Test]
        public void SelectFocus_SimilarAim_PicksNearer()
        {
            // 정렬도 차가 오차 안(0.02)이면 "같은 것을 겨눴다"고 보고 거리가 판정을 넘겨받는다.
            InteractionFocus focus = InteractionArbitrationLogic.SelectFocus(Candidates(
                Candidate(InteractionSource.Storage, 0.99f, 9f),
                Candidate(InteractionSource.Crafting, 0.98f, 3f)));

            Assert.AreEqual(InteractionSource.Crafting, focus.Source);
        }

        [Test]
        public void SelectFocus_IsIndependentOfSubmitOrder()
        {
            InteractionCandidate storage = Candidate(InteractionSource.Storage, 0.90f, 5f);
            InteractionCandidate crafting = Candidate(InteractionSource.Crafting, 0.95f, 8f);
            InteractionCandidate fuel = Candidate(InteractionSource.EngineFuel, 0.94f, 7f);

            InteractionSource forward =
                InteractionArbitrationLogic.SelectFocus(Candidates(storage, crafting, fuel)).Source;
            InteractionSource reversed =
                InteractionArbitrationLogic.SelectFocus(Candidates(fuel, crafting, storage)).Source;
            InteractionSource shuffled =
                InteractionArbitrationLogic.SelectFocus(Candidates(crafting, storage, fuel)).Source;

            Assert.AreEqual(forward, reversed);
            Assert.AreEqual(forward, shuffled);
        }

        [Test]
        public void SelectFocus_ExactTie_BreaksBySourceThenInstance()
        {
            InteractionFocus bySource = InteractionArbitrationLogic.SelectFocus(Candidates(
                Candidate(InteractionSource.Crafting, 0.9f, 4f),
                Candidate(InteractionSource.MountedWeapon, 0.9f, 4f)));

            Assert.AreEqual(InteractionSource.MountedWeapon, bySource.Source);

            InteractionFocus byInstance = InteractionArbitrationLogic.SelectFocus(Candidates(
                Candidate(InteractionSource.Bundle, 0.9f, 4f, 7),
                Candidate(InteractionSource.Bundle, 0.9f, 4f, 3)));

            Assert.AreEqual(InteractionSource.Bundle, byInstance.Source);
            Assert.AreEqual(3, byInstance.InstanceKey);
        }

        [Test]
        public void SelectFocus_KeepsInstanceKeyOfWinner()
        {
            InteractionFocus focus = InteractionArbitrationLogic.SelectFocus(Candidates(
                Candidate(InteractionSource.Bundle, 0.80f, 2f, 11),
                Candidate(InteractionSource.Bundle, 0.99f, 6f, 12)));

            Assert.AreEqual(12, focus.InstanceKey);
        }

        [Test]
        public void SelectFocus_IgnoresNoneSource()
        {
            InteractionFocus focus = InteractionArbitrationLogic.SelectFocus(Candidates(
                Candidate(InteractionSource.None, 1f, 0f),
                Candidate(InteractionSource.Storage, 0.9f, 4f)));

            Assert.AreEqual(InteractionSource.Storage, focus.Source);
        }

        // ── 프레임 단위 중재자 ─────────────────────────────────────────────

        [SetUp]
        public void ResetArbiter()
        {
            InteractionArbiter.Reset();
        }

        [TearDown]
        public void ClearArbiter()
        {
            InteractionArbiter.Reset();
        }

        [Test]
        public void Arbiter_FocusesPreviousFrameSubmissions()
        {
            // 1프레임: 제출만 — 아직 확정된 초점이 없다.
            InteractionArbiter.Submit(1, InteractionSource.Storage, 0, 0.99f, 4f);
            InteractionArbiter.Submit(1, InteractionSource.Crafting, 0, 0.85f, 2f);
            Assert.IsFalse(InteractionArbiter.IsFocused(1, InteractionSource.Storage, 0));

            // 2프레임: 직전 프레임 제출분으로 확정 — 겨눈 창고 하나만 참이다.
            Assert.IsTrue(InteractionArbiter.IsFocused(2, InteractionSource.Storage, 0));
            Assert.IsFalse(InteractionArbiter.IsFocused(2, InteractionSource.Crafting, 0));
        }

        [Test]
        public void Arbiter_AnswerDoesNotDependOnUpdateOrder()
        {
            // 같은 프레임 안에서는 누가 먼저 묻든 같은 답이어야 한다 (Update 순서는 보장되지 않는다).
            InteractionArbiter.Submit(1, InteractionSource.Crafting, 0, 0.95f, 3f);
            InteractionArbiter.Submit(1, InteractionSource.Storage, 0, 0.70f, 1f);

            bool craftingFirst = InteractionArbiter.IsFocused(2, InteractionSource.Crafting, 0);
            bool storageSecond = InteractionArbiter.IsFocused(2, InteractionSource.Storage, 0);

            Assert.IsTrue(craftingFirst);
            Assert.IsFalse(storageSecond);

            // 순서를 뒤집어 다시 물어도 결과가 같다.
            Assert.IsFalse(InteractionArbiter.IsFocused(2, InteractionSource.Storage, 0));
            Assert.IsTrue(InteractionArbiter.IsFocused(2, InteractionSource.Crafting, 0));
        }

        [Test]
        public void Arbiter_ClearsFocusWhenNobodySubmits()
        {
            InteractionArbiter.Submit(1, InteractionSource.Storage, 0, 0.99f, 4f);
            Assert.IsTrue(InteractionArbiter.IsFocused(2, InteractionSource.Storage, 0));

            // 2프레임에 아무도 내지 않았다 — 3프레임에는 초점이 사라진다(안내가 얼어붙지 않는다).
            Assert.IsFalse(InteractionArbiter.IsFocused(3, InteractionSource.Storage, 0));
        }

        [Test]
        public void Arbiter_KeepsBestSubmissionPerInstance()
        {
            // 한 프레임에 같은 인스턴스가 두 번 내면 더 잘 겨눈 쪽만 남는다.
            InteractionArbiter.Submit(1, InteractionSource.Crafting, 0, 0.70f, 1f);
            InteractionArbiter.Submit(1, InteractionSource.Crafting, 0, 0.99f, 9f);
            InteractionArbiter.Submit(1, InteractionSource.Storage, 0, 0.85f, 2f);

            Assert.IsTrue(InteractionArbiter.IsFocused(2, InteractionSource.Crafting, 0));
            Assert.IsFalse(InteractionArbiter.IsFocused(2, InteractionSource.Storage, 0));
        }

        [Test]
        public void Arbiter_CaptureBlocksOtherSources()
        {
            // 창고 창이 열렸다 — 그 동안 제작대가 아무리 잘 겨눠져도 안내가 뜨지 않는다.
            InteractionArbiter.Capture(InteractionSource.Storage);
            InteractionArbiter.Submit(1, InteractionSource.Crafting, 0, 0.99f, 1f);

            Assert.IsTrue(InteractionArbiter.IsFocused(2, InteractionSource.Storage, 0));
            Assert.IsFalse(InteractionArbiter.IsFocused(2, InteractionSource.Crafting, 0));

            // 창을 닫으면 중재가 다시 돈다.
            InteractionArbiter.Release(InteractionSource.Storage);
            InteractionArbiter.Submit(2, InteractionSource.Crafting, 0, 0.99f, 1f);
            Assert.IsTrue(InteractionArbiter.IsFocused(3, InteractionSource.Crafting, 0));
        }

        [Test]
        public void Arbiter_CaptureIsNotStolenAndReleaseIsOwnerOnly()
        {
            InteractionArbiter.Capture(InteractionSource.Storage);
            InteractionArbiter.Capture(InteractionSource.Crafting);

            // 먼저 연 창이 초점을 지킨다.
            Assert.IsTrue(InteractionArbiter.IsFocused(1, InteractionSource.Storage, 0));
            Assert.IsFalse(InteractionArbiter.IsFocused(1, InteractionSource.Crafting, 0));

            // 남의 독점은 대신 풀리지 않는다.
            InteractionArbiter.Release(InteractionSource.Crafting);
            Assert.IsTrue(InteractionArbiter.IsFocused(1, InteractionSource.Storage, 0));

            InteractionArbiter.Release(InteractionSource.Storage);
            Assert.IsFalse(InteractionArbiter.IsFocused(1, InteractionSource.Storage, 0));
        }

        [Test]
        public void Arbiter_CaptureDistinguishesInstances()
        {
            InteractionArbiter.Capture(InteractionSource.Bundle, 42);

            Assert.IsTrue(InteractionArbiter.IsFocused(1, InteractionSource.Bundle, 42));
            Assert.IsFalse(InteractionArbiter.IsFocused(1, InteractionSource.Bundle, 43));

            // 다른 인스턴스가 해제를 시도해도 풀리지 않는다.
            InteractionArbiter.Release(InteractionSource.Bundle, 43);
            Assert.IsTrue(InteractionArbiter.IsFocused(1, InteractionSource.Bundle, 42));
        }

        [Test]
        public void Arbiter_ResetClearsEverything()
        {
            InteractionArbiter.Capture(InteractionSource.Storage);
            InteractionArbiter.Submit(1, InteractionSource.Crafting, 0, 0.99f, 1f);
            InteractionArbiter.Reset();

            Assert.IsFalse(InteractionArbiter.IsFocused(2, InteractionSource.Storage, 0));
            Assert.IsFalse(InteractionArbiter.IsFocused(2, InteractionSource.Crafting, 0));
        }
    }
}
