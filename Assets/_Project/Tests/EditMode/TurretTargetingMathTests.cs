using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 자동 터렛의 대상 선정 검증 (M7 4차 §2.6 — 결정 ⑧).
    /// 자동 무기에서 틀리면 안 되는 것은 <b>누구를 고르는가</b>가 아니라 <b>누구를 고르지 않는가</b>다 —
    /// 사각 밖·반경 밖·죽은 대상은 후보에서 빠져야 하고, 같은 입력이면 같은 답이 나와야 한다.
    /// 아군 오사 차단(적대 대상 계약)은 물리 조회 단계의 몫이라 여기 오지 않는다.
    /// </summary>
    public sealed class TurretTargetingMathTests
    {
        private const float YawLimit = 110f;
        private const float PitchMin = -15f;
        private const float PitchMax = 40f;
        private const float SearchRadius = 35f;

        private static TurretCandidate At(float x, float y, float z, bool alive = true)
        {
            return new TurretCandidate { Position = new Vector3(x, y, z), IsAlive = alive };
        }

        private static int Select(TurretCandidate[] candidates)
        {
            return TurretTargetingMath.SelectTarget(
                candidates, candidates.Length, Vector3.zero, Quaternion.identity,
                SearchRadius, YawLimit, PitchMin, PitchMax);
        }

        [Test]
        public void 후보가_없으면_고르지_않는다()
        {
            Assert.That(Select(new TurretCandidate[0]), Is.EqualTo(-1));
            Assert.That(
                TurretTargetingMath.SelectTarget(
                    null, 3, Vector3.zero, Quaternion.identity, SearchRadius, YawLimit, PitchMin, PitchMax),
                Is.EqualTo(-1));
        }

        [Test]
        public void 가장_가까운_대상을_고른다()
        {
            TurretCandidate[] candidates = { At(0f, 0f, 20f), At(0f, 0f, 8f), At(0f, 0f, 14f) };

            Assert.That(Select(candidates), Is.EqualTo(1));
        }

        [Test]
        public void 죽은_대상은_제외한다()
        {
            // 더 가깝지만 죽었다 — 다음으로 가까운 산 것이 남는다.
            TurretCandidate[] candidates = { At(0f, 0f, 5f, alive: false), At(0f, 0f, 12f) };

            Assert.That(Select(candidates), Is.EqualTo(1));
        }

        [Test]
        public void 탐색_반경_밖은_제외한다()
        {
            TurretCandidate[] candidates = { At(0f, 0f, SearchRadius + 1f) };

            Assert.That(Select(candidates), Is.EqualTo(-1));
        }

        [Test]
        public void 탐색_반경_경계는_대상이다()
        {
            TurretCandidate[] candidates = { At(0f, 0f, SearchRadius) };

            Assert.That(Select(candidates), Is.EqualTo(0));
        }

        [Test]
        public void 사각_밖은_제외한다()
        {
            // 바로 뒤(yaw 180도) — 사람이 조작할 때와 같은 제한이라 자동이라고 뒤로 쏘지 않는다.
            TurretCandidate[] candidates = { At(0f, 0f, -10f) };

            Assert.That(Select(candidates), Is.EqualTo(-1));
        }

        [Test]
        public void 사각_밖이_더_가까워도_사각_안을_고른다()
        {
            TurretCandidate[] candidates = { At(0f, 0f, -4f), At(0f, 0f, 25f) };

            Assert.That(Select(candidates), Is.EqualTo(1));
        }

        [Test]
        public void 앙각_한계를_넘는_대상은_제외한다()
        {
            // 바로 위 — 올려다보기 한계 40도를 넘는다.
            TurretCandidate[] candidates = { At(0f, 20f, 2f) };

            Assert.That(Select(candidates), Is.EqualTo(-1));
        }

        [Test]
        public void 발밑의_대상은_내려다보기_한계에_걸린다()
        {
            TurretCandidate[] candidates = { At(0f, -10f, 3f) };

            Assert.That(Select(candidates), Is.EqualTo(-1));
        }

        [Test]
        public void 동률이면_낮은_인덱스가_이긴다()
        {
            // 같은 입력이면 같은 답 — 물리 조회 순서가 흔들려도 판정 자체는 흔들리지 않는다.
            TurretCandidate[] candidates = { At(0f, 0f, 10f), At(0f, 0f, 10f) };

            Assert.That(Select(candidates), Is.EqualTo(0));
        }

        [Test]
        public void 채워진_개수_밖은_보지_않는다()
        {
            // 재사용 버퍼의 뒤쪽 잔재를 대상으로 삼지 않는다.
            TurretCandidate[] buffer = { At(0f, 0f, 30f), At(0f, 0f, 5f) };

            int index = TurretTargetingMath.SelectTarget(
                buffer, 1, Vector3.zero, Quaternion.identity, SearchRadius, YawLimit, PitchMin, PitchMax);

            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void 거치대가_돌면_사각도_함께_돈다()
        {
            // 90도로 설치된 터렛에게는 +X 쪽이 정면이다.
            TurretCandidate[] candidates = { At(15f, 0f, 0f) };
            Quaternion mount = MountedAimMath.ResolveMountRotation(1);

            Assert.That(
                TurretTargetingMath.SelectTarget(
                    candidates, 1, Vector3.zero, mount, SearchRadius, YawLimit, PitchMin, PitchMax),
                Is.EqualTo(0));
            Assert.That(Select(candidates), Is.EqualTo(0));

            // 같은 대상이 0도 설치에서는 정면 기준 90도라 아직 사각 안이지만, 뒤쪽은 어느 회전에서도 빠진다.
            TurretCandidate[] behind = { At(-15f, 0f, 0f) };
            Assert.That(
                TurretTargetingMath.SelectTarget(
                    behind, 1, Vector3.zero, mount, SearchRadius, YawLimit, PitchMin, PitchMax),
                Is.EqualTo(-1));
        }
    }
}
