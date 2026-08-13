using Game.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 스탬피드 순수 로직 검증 (M7 1차, 기획서 §4.3) — 발생 추첨(날씨 규약: 첫날 제외·확률 0
    /// 미발생)·유입 계획 클램프·통과 속도. 난수는 주입되므로 경계를 결정론으로 확인한다.
    /// </summary>
    public sealed class StampedeMathTests
    {
        // ── 발생 추첨 (결정 ④ — 낮 시작 확률 추첨) ─────────────────────────

        [Test]
        public void 확률이_0인_지역에서는_발생하지_않는다()
        {
            Assert.That(StampedeMath.ShouldTrigger(0f, 2, 0f), Is.False, "확률 0 — 숲·사막");
            Assert.That(StampedeMath.ShouldTrigger(-1f, 2, 0f), Is.False, "음수 방어");
        }

        [Test]
        public void 지역_첫날은_발생하지_않는다()
        {
            // 날씨와 같은 규약 — 지형조차 아직 도착하지 않은 시점이라 전환 연출과 겹쳐 읽힌다.
            Assert.That(StampedeMath.ShouldTrigger(1f, 1, 0f), Is.False, "1일차 제외");
            Assert.That(StampedeMath.ShouldTrigger(1f, 0, 0f), Is.False);
            Assert.That(StampedeMath.ShouldTrigger(1f, 2, 0f), Is.True, "2일차부터 추첨");
        }

        [Test]
        public void 난수가_확률_미만이면_발생한다()
        {
            Assert.That(StampedeMath.ShouldTrigger(0.5f, 2, 0.49f), Is.True);
            Assert.That(StampedeMath.ShouldTrigger(0.5f, 2, 0.5f), Is.False, "경계 — roll == chance는 미발생");
            Assert.That(StampedeMath.ShouldTrigger(0.5f, 2, 0.99f), Is.False);
        }

        [Test]
        public void 범위_밖_난수는_0과_1로_고정된다()
        {
            Assert.That(StampedeMath.ShouldTrigger(0.5f, 2, -1f), Is.True, "clamp → 0");
            Assert.That(StampedeMath.ShouldTrigger(0.5f, 2, 2f), Is.False, "clamp → 1");
        }

        // ── 유입 계획 (연속 유입 열 — 동시 수 억제) ────────────────────────

        [Test]
        public void 계획은_설정값을_그대로_담는다()
        {
            StampedePlan plan = StampedeMath.Plan(30, 1.2f, 12, 12);

            Assert.That(plan.TotalCount, Is.EqualTo(30));
            Assert.That(plan.SpawnInterval, Is.EqualTo(1.2f));
            Assert.That(plan.MaxAlive, Is.EqualTo(12));
        }

        [Test]
        public void 무효_설정값은_유효_범위로_강제된다()
        {
            StampedePlan plan = StampedeMath.Plan(0, 0f, 0, 0);

            Assert.That(plan.TotalCount, Is.EqualTo(1), "총량 ≥ 1");
            Assert.That(plan.SpawnInterval, Is.EqualTo(0.1f), "간격 ≥ 0.1s");
            Assert.That(plan.MaxAlive, Is.EqualTo(1), "동시 상한 ≥ 1");
        }

        [Test]
        public void 동시_상한은_대역폭_방어선을_넘지_않는다()
        {
            StampedePlan plan = StampedeMath.Plan(100, 1f, 50, 12);

            Assert.That(plan.MaxAlive, Is.EqualTo(12), "설정이 커도 cap에 눌린다");
        }

        // ── 통과 경로 (결정 ③ — 열차와 평행한 직선 주행) ───────────────────

        [Test]
        public void 통과_속도는_자체_주행에_스크롤을_가산한_후방_직진이다()
        {
            Vector3 velocity = StampedeMath.ComputePassVelocity(9f, 6f);

            Assert.That(velocity.x, Is.EqualTo(0f), "측면 이탈 없음 — 직선 주행");
            Assert.That(velocity.y, Is.EqualTo(0f));
            Assert.That(velocity.z, Is.EqualTo(-15f), "-Z (전방에서 후방으로 스쳐 지나간다)");
        }

        [Test]
        public void 음수_속도는_0으로_방어된다()
        {
            Vector3 velocity = StampedeMath.ComputePassVelocity(-5f, -3f);

            Assert.That(velocity, Is.EqualTo(Vector3.zero));
        }
    }
}
