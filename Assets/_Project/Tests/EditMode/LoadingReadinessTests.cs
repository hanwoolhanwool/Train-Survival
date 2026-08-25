using Game.Systems.Loading;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 전원 대기 판정 검증 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §3.4 · §3.5 · §7.2 · §10.
    ///
    /// <para><b>"다른 단계의 보고를 세지 않는다"가 여기서 가장 중요하다</b>(§7.2). 느린
    /// 클라이언트의 예고 완료 보고가 늦게 도착하면, 단계를 대조하지 않는 한 그것만으로
    /// 정착 대기가 풀린다 — <b>아직 아무것도 미리 만들지 못한 사람을 데리고 출발</b>하게 되고,
    /// 증상은 "가끔 한 명만 첫 건축에서 튄다"로 나타나 원인을 짚기 매우 어렵다.</para>
    /// </summary>
    public sealed class LoadingReadinessTests
    {
        private const float Timeout = LoadingReadiness.DefaultTimeoutSeconds;

        // ── 단계 대조 ────────────────────────────────────────────────────

        [Test]
        public void 기다리는_단계의_보고만_센다()
        {
            Assert.IsTrue(LoadingReadiness.CountsAsReport(LoadingStage.Prepare, LoadingStage.Prepare));
            Assert.IsTrue(LoadingReadiness.CountsAsReport(LoadingStage.Settle, LoadingStage.Settle));
        }

        [Test]
        public void 지연된_예고_보고를_정착_보고로_세지_않는다()
        {
            Assert.IsFalse(LoadingReadiness.CountsAsReport(LoadingStage.Settle, LoadingStage.Prepare));
            Assert.IsFalse(LoadingReadiness.CountsAsReport(LoadingStage.Prepare, LoadingStage.Settle));
        }

        [Test]
        public void 로딩_중이_아니면_어떤_보고도_세지_않는다()
        {
            Assert.IsFalse(LoadingReadiness.CountsAsReport(LoadingStage.Idle, LoadingStage.Idle));
            Assert.IsFalse(LoadingReadiness.CountsAsReport(LoadingStage.Idle, LoadingStage.Prepare));
        }

        // ── 성립 조건 ────────────────────────────────────────────────────

        [Test]
        public void 전원이_보고하면_성립한다()
        {
            Assert.IsTrue(LoadingReadiness.IsSatisfied(4, 4));
            Assert.IsFalse(LoadingReadiness.IsSatisfied(4, 3));
        }

        [Test]
        public void 혼자면_보고_하나로_성립한다()
        {
            // §7.4 — 1인 플레이도 같은 경로를 지난다. 지름길을 만들지 않는다.
            Assert.IsTrue(LoadingReadiness.IsSatisfied(1, 1));
            Assert.IsFalse(LoadingReadiness.IsSatisfied(1, 0));
        }

        [Test]
        public void 총원이_0이면_기다릴_사람이_없다()
        {
            // 대기실 상태가 아직 안 섰거나 Boot만 열어 본 경우 — 막히면 안 된다.
            Assert.IsTrue(LoadingReadiness.IsSatisfied(0, 0));
        }

        [Test]
        public void 이탈로_총원이_줄면_즉시_성립한다()
        {
            // 넷 중 셋이 보고한 채 한 명이 나갔다 — 남은 인원만으로 조건이 성립해야 한다(§3.5).
            Assert.IsFalse(LoadingReadiness.ShouldAdvance(4, 3, 0f, Timeout));
            Assert.IsTrue(LoadingReadiness.ShouldAdvance(3, 3, 0f, Timeout));
        }

        // ── 타임아웃 ─────────────────────────────────────────────────────

        [Test]
        public void 상한을_넘으면_보고가_모자라도_진행한다()
        {
            Assert.IsFalse(LoadingReadiness.ShouldAdvance(4, 1, Timeout - 1f, Timeout));
            Assert.IsTrue(LoadingReadiness.ShouldAdvance(4, 1, Timeout, Timeout));
            Assert.IsTrue(LoadingReadiness.ShouldAdvance(4, 1, Timeout + 10f, Timeout));
        }

        [Test]
        public void 상한이_0이면_기다리지_않는다()
        {
            Assert.IsTrue(LoadingReadiness.IsTimedOut(0f, 0f));
            Assert.IsTrue(LoadingReadiness.IsTimedOut(0f, -1f));
        }

        [Test]
        public void 기본_상한은_20초다()
        {
            // §3.5 — 무한 대기는 방을 죽인다. 값이 바뀌면 4차 두 벌 검증 기록과 어긋난다.
            Assert.AreEqual(20f, LoadingReadiness.DefaultTimeoutSeconds, 1e-4f);
        }

        // ── 진행도 ───────────────────────────────────────────────────────

        [Test]
        public void 진행도는_보고_인원_나누기_총원이다()
        {
            Assert.AreEqual(0f, LoadingReadiness.Progress(4, 0), 1e-4f);
            Assert.AreEqual(0.5f, LoadingReadiness.Progress(4, 2), 1e-4f);
            Assert.AreEqual(1f, LoadingReadiness.Progress(4, 4), 1e-4f);
        }

        [Test]
        public void 진행도는_0과_1_사이를_벗어나지_않는다()
        {
            Assert.AreEqual(1f, LoadingReadiness.Progress(0, 0), 1e-4f);
            Assert.AreEqual(1f, LoadingReadiness.Progress(2, 5), 1e-4f);
            Assert.AreEqual(0f, LoadingReadiness.Progress(2, -1), 1e-4f);
        }

        [Test]
        public void 대기_진행도가_100퍼센트여도_화면은_100퍼센트가_아니다()
        {
            // §4.3의 두 번째 보장 — 대기 단계의 상한은 전체의 1보다 작다.
            Assert.Less(
                LoadingProgressMath.Combine(LoadingStage.WaitSettle, LoadingReadiness.Progress(4, 4)),
                1f);
        }
    }
}
