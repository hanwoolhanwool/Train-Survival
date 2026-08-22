using Game.Gameplay.Combat;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 총구 앵커 선택 규칙 (집게 발사위치 통합 계획 §3 단계 2) — 소유 여부 2종 × 앵커 가용성 4종 전수.
    /// <para>
    /// 축은 <b>보는 사람</b>이다: 소유자는 자기 화면의 FP 뷰모델 총구를, 원격 피어는 그 캐릭터가
    /// 손에 쥔 TP 모델의 총구를 쓴다. 소유자의 시점 모드(분리/통합)는 이 규칙에 들어오지 않는다 —
    /// 모드 축을 얹는 것은 1인칭 통합 시점 전환 계획 §3.5의 몫이며, 그때 인자가 하나 는다.
    /// </para>
    /// </summary>
    public sealed class WeaponMuzzleRulesTests
    {
        private const bool Owner = true;
        private const bool Remote = false;
        private const bool Wired = true;
        private const bool Missing = false;

        [Test]
        public void 소유자는_FP_총구를_쓴다()
        {
            Assert.That(WeaponMuzzleRules.ResolveAnchor(Owner, Wired, Wired),
                Is.EqualTo(MuzzleAnchor.Fp), "둘 다 있으면 소유자는 FP");
            Assert.That(WeaponMuzzleRules.ResolveAnchor(Owner, Wired, Missing),
                Is.EqualTo(MuzzleAnchor.Fp), "FP만 있어도 FP");
        }

        [Test]
        public void 원격_피어는_TP_총구를_쓴다()
        {
            Assert.That(WeaponMuzzleRules.ResolveAnchor(Remote, Wired, Wired),
                Is.EqualTo(MuzzleAnchor.Tp), "둘 다 있으면 원격은 TP");
            Assert.That(WeaponMuzzleRules.ResolveAnchor(Remote, Missing, Wired),
                Is.EqualTo(MuzzleAnchor.Tp), "TP만 있어도 TP");
        }

        /// <summary>
        /// 선호 앵커가 비면 반대쪽으로 물러선다 — 무기 전환 중 모델이 아직 붙지 않은 프레임에도
        /// 로프가 그려져야 하고, 남은 한쪽이 레거시 고정점보다는 언제나 무기에 가깝다.
        /// </summary>
        [Test]
        public void 선호_앵커가_없으면_반대쪽으로_물러선다()
        {
            Assert.That(WeaponMuzzleRules.ResolveAnchor(Owner, Missing, Wired),
                Is.EqualTo(MuzzleAnchor.Tp), "소유자인데 FP가 없으면 TP");
            Assert.That(WeaponMuzzleRules.ResolveAnchor(Remote, Wired, Missing),
                Is.EqualTo(MuzzleAnchor.Fp), "원격인데 TP가 없으면 FP");
        }

        /// <summary>둘 다 비었을 때만 레거시 총구로 물러선다 — 이 경로는 배선 누락의 신호다.</summary>
        [Test]
        public void 앵커가_하나도_없으면_폴백이다()
        {
            Assert.That(WeaponMuzzleRules.ResolveAnchor(Owner, Missing, Missing),
                Is.EqualTo(MuzzleAnchor.Fallback));
            Assert.That(WeaponMuzzleRules.ResolveAnchor(Remote, Missing, Missing),
                Is.EqualTo(MuzzleAnchor.Fallback));
        }

        /// <summary>같은 입력은 언제나 같은 앵커를 준다 — 프레임마다 다시 고르므로 흔들리면 로프가 튄다.</summary>
        [Test]
        public void 여덟_조합_전수가_결정적이다()
        {
            foreach (bool isOwner in new[] { Owner, Remote })
            {
                foreach (bool fp in new[] { Wired, Missing })
                {
                    foreach (bool tp in new[] { Wired, Missing })
                    {
                        MuzzleAnchor first = WeaponMuzzleRules.ResolveAnchor(isOwner, fp, tp);
                        Assert.That(WeaponMuzzleRules.ResolveAnchor(isOwner, fp, tp),
                            Is.EqualTo(first), $"owner={isOwner} fp={fp} tp={tp}");

                        // 배선된 앵커만 선택된다 — 없는 쪽을 고르면 로프 시작점이 원점으로 튄다.
                        if (first == MuzzleAnchor.Fp)
                        {
                            Assert.That(fp, Is.True, "없는 FP를 골랐다");
                        }
                        else if (first == MuzzleAnchor.Tp)
                        {
                            Assert.That(tp, Is.True, "없는 TP를 골랐다");
                        }
                        else
                        {
                            Assert.That(fp || tp, Is.False, "앵커가 있는데도 폴백으로 갔다");
                        }
                    }
                }
            }
        }
    }
}
