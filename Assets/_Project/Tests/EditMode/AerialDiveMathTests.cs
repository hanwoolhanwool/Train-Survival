using Game.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 하늘 위협의 급강하 왕복 검증 (바다 지역 구현 계획 §13).
    /// 실측 규격 — 갑판 3.566 · 산탄총 20 m · 순항 34 · 강하 18 m/s · 상승 11.7 m/s · 체공 1.2초.
    /// </summary>
    public sealed class AerialDiveMathTests
    {
        private const float DeckY = 3.566f;
        private const float ShotgunRange = 20f;
        private const float CruiseY = 34f;
        private const float StrikeY = DeckY + 1.6f;   // 표적 머리 위
        private const float DiveSpeed = 18f;
        private const float ClimbSpeed = 18f * 0.65f;
        private const float Hover = 1.2f;
        private const float TriggerRange = 8f;

        // ── 순항 고도가 손 무기 밖이어야 한다 ──

        [Test]
        public void 순항_중에는_산탄총이_닿지_않는다()
        {
            // 이것이 무너지면 하늘은 "조금 높은 적"이 된다 — 왕복이 성립하지 않는다.
            Assert.IsFalse(AerialDiveMath.IsWithinWeaponReach(CruiseY, DeckY, ShotgunRange));

            // 천장은 갑판 + 사거리 = 23.566 이다.
            Assert.IsTrue(AerialDiveMath.IsWithinWeaponReach(23.5f, DeckY, ShotgunRange));
            Assert.IsFalse(AerialDiveMath.IsWithinWeaponReach(23.6f, DeckY, ShotgunRange));
        }

        [Test]
        public void 내려온_순간에는_닿는다()
        {
            Assert.IsTrue(AerialDiveMath.IsWithinWeaponReach(StrikeY, DeckY, ShotgunRange));
        }

        [Test]
        public void 거치_무기는_순항_중에도_닿는다()
        {
            // 거치 기관총 90 m — 순항을 손 무기 밖에 두어도 대응 수단이 0이 되지 않는 근거다.
            Assert.IsTrue(AerialDiveMath.IsWithinWeaponReach(CruiseY, DeckY, 90f));
        }

        // ── 반격 창 — 이 값이 곧 난이도다 ──

        [Test]
        public void 반격_창이_3초를_넘는다()
        {
            float window = AerialDiveMath.ReachWindowSeconds(
                CruiseY, StrikeY, DeckY, ShotgunRange, DiveSpeed, ClimbSpeed, Hover);

            // 강하 (23.566−5.166)/18 = 1.02 + 체공 1.2 + 상승 18.4/11.7 = 1.57 → 약 3.8초
            Assert.That(window, Is.EqualTo(3.79f).Within(0.05f));
            Assert.Greater(window, 3f, "손 무기로 대응할 수 없을 만큼 짧으면 안 된다");
        }

        [Test]
        public void 상승이_느려야_창이_길어진다()
        {
            float slow = AerialDiveMath.ReachWindowSeconds(
                CruiseY, StrikeY, DeckY, ShotgunRange, DiveSpeed, ClimbSpeed, Hover);
            float fast = AerialDiveMath.ReachWindowSeconds(
                CruiseY, StrikeY, DeckY, ShotgunRange, DiveSpeed, DiveSpeed, Hover);

            Assert.Greater(slow, fast, "상승 속도를 강하의 65 %로 둔 이유");
        }

        [Test]
        public void 순항이_천장_아래면_왕복_전체가_사거리_안이다()
        {
            // 순항을 낮게 잡으면 "내려온 순간만 잡힌다"가 성립하지 않는다는 것을 계약으로 남긴다.
            float window = AerialDiveMath.ReachWindowSeconds(
                cruiseY: 12f, StrikeY, DeckY, ShotgunRange, DiveSpeed, ClimbSpeed, Hover);
            float full = (12f - StrikeY) / DiveSpeed + Hover + (12f - StrikeY) / ClimbSpeed;

            Assert.That(window, Is.EqualTo(full).Within(0.01f));
        }

        // ── 국면 전이는 한 방향으로만 돈다 ──

        [Test]
        public void 사거리_안에_들면_강하한다()
        {
            Assert.That(
                AerialDiveMath.ResolvePhase(
                    AerialPhase.Cruise, CruiseY, StrikeY, CruiseY, 6f, TriggerRange, 0f, true),
                Is.EqualTo(AerialPhase.Dive));
        }

        [Test]
        public void 멀면_순항을_유지한다()
        {
            Assert.That(
                AerialDiveMath.ResolvePhase(
                    AerialPhase.Cruise, CruiseY, StrikeY, CruiseY, 20f, TriggerRange, 0f, true),
                Is.EqualTo(AerialPhase.Cruise));
        }

        [Test]
        public void 표적_높이에_닿으면_체공으로_넘어간다()
        {
            Assert.That(
                AerialDiveMath.ResolvePhase(
                    AerialPhase.Dive, StrikeY, StrikeY, CruiseY, 2f, TriggerRange, 0f, true),
                Is.EqualTo(AerialPhase.Hover));

            // 아직 높으면 계속 내려온다.
            Assert.That(
                AerialDiveMath.ResolvePhase(
                    AerialPhase.Dive, 20f, StrikeY, CruiseY, 2f, TriggerRange, 0f, true),
                Is.EqualTo(AerialPhase.Dive));
        }

        [Test]
        public void 체공이_끝나면_상승한다()
        {
            Assert.That(
                AerialDiveMath.ResolvePhase(
                    AerialPhase.Hover, StrikeY, StrikeY, CruiseY, 2f, TriggerRange, 0f, true),
                Is.EqualTo(AerialPhase.Climb));
        }

        [Test]
        public void 순항_고도에_닿으면_왕복이_한_바퀴_돈다()
        {
            Assert.That(
                AerialDiveMath.ResolvePhase(
                    AerialPhase.Climb, CruiseY, StrikeY, CruiseY, 2f, TriggerRange, 0f, true),
                Is.EqualTo(AerialPhase.Cruise));
        }

        [Test]
        public void 표적을_잃으면_상승으로_빠져나간다()
        {
            // 표적이 잠깐 사라져도 공중에서 굳지 않아야 한다.
            Assert.That(
                AerialDiveMath.ResolvePhase(
                    AerialPhase.Dive, 20f, StrikeY, CruiseY, 2f, TriggerRange, 0f, hasTarget: false),
                Is.EqualTo(AerialPhase.Climb));

            Assert.That(
                AerialDiveMath.ResolvePhase(
                    AerialPhase.Hover, StrikeY, StrikeY, CruiseY, 2f, TriggerRange, 1f, hasTarget: false),
                Is.EqualTo(AerialPhase.Climb));
        }

        // ── 고도 이동 ──

        [Test]
        public void 목표_고도를_넘어가지_않는다()
        {
            // 넘으면 국면 판정이 왕복하며 진동한다.
            float y = AerialDiveMath.StepAltitude(6f, StrikeY, DiveSpeed, 1f);

            Assert.That(y, Is.EqualTo(StrikeY));
        }

        [Test]
        public void 국면이_향하는_고도가_갈린다()
        {
            Assert.That(AerialDiveMath.TargetAltitude(AerialPhase.Dive, CruiseY, StrikeY), Is.EqualTo(StrikeY));
            Assert.That(AerialDiveMath.TargetAltitude(AerialPhase.Hover, CruiseY, StrikeY), Is.EqualTo(StrikeY));
            Assert.That(AerialDiveMath.TargetAltitude(AerialPhase.Climb, CruiseY, StrikeY), Is.EqualTo(CruiseY));
            Assert.That(AerialDiveMath.TargetAltitude(AerialPhase.Cruise, CruiseY, StrikeY), Is.EqualTo(CruiseY));
        }

        // ── 수평 접근 ──

        [Test]
        public void 스크롤을_이겨야_제자리에_남지_않는다()
        {
            Vector3 v = AerialDiveMath.ComputeApproachVelocity(
                new Vector3(0f, CruiseY, 20f), new Vector3(0f, DeckY, 0f), moveSpeed: 7f, scrollSpeed: 6f);

            // 표적이 -Z 쪽이므로 자체 추격 -7 에 스크롤 -6 이 더해진다.
            Assert.That(v.z, Is.EqualTo(-13f).Within(0.01f));
            Assert.That(v.y, Is.EqualTo(0f), "고도는 국면이 몬다");
        }

        [Test]
        public void 표적_위에_있으면_스크롤만_남는다()
        {
            Vector3 v = AerialDiveMath.ComputeApproachVelocity(
                new Vector3(0f, CruiseY, 0f), new Vector3(0f, DeckY, 0f), 7f, 6f);

            Assert.That(v.x, Is.EqualTo(0f));
            Assert.That(v.z, Is.EqualTo(-6f).Within(0.01f));
        }
    }
}
