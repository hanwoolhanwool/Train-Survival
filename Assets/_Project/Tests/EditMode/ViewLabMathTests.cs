using Game.Gameplay.Debugging;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ViewLabMathTests
    {
        /// <summary>AC_Player 블렌드 트리 임계값과 같은 근거 (PlayerMovementSettings 4.5 / 7).</summary>
        private const float WalkSpeed = 4.5f;

        private const float RunSpeed = 7f;

        // ── 순환 인덱스 ──

        [Test]
        public void 순환_인덱스는_끝에서_처음으로_돈다()
        {
            Assert.That(ViewLabMath.CycleIndex(4, +1, 5), Is.EqualTo(0));
        }

        [Test]
        public void 순환_인덱스는_음수_델타로_끝으로_돈다()
        {
            Assert.That(ViewLabMath.CycleIndex(0, -1, 5), Is.EqualTo(4));
        }

        [Test]
        public void 빈_목록의_순환_인덱스는_마이너스1이다()
        {
            Assert.That(ViewLabMath.CycleIndex(0, +1, 0), Is.EqualTo(-1));
        }

        // ── 넛지 스텝 ──

        [Test]
        public void 미세_스텝이_켜지면_작은_폭을_쓴다()
        {
            Assert.That(ViewLabMath.Step(fine: true, 0.05f, 0.005f), Is.EqualTo(0.005f));
            Assert.That(ViewLabMath.Step(fine: false, 0.05f, 0.005f), Is.EqualTo(0.05f));
        }

        [Test]
        public void 위치_넛지는_지정_축만_움직인다()
        {
            Vector3 nudged = ViewLabMath.NudgePosition(new Vector3(1f, 2f, 3f), axis: 1, signedStep: -0.05f);
            Assert.That(nudged.x, Is.EqualTo(1f));
            Assert.That(nudged.y, Is.EqualTo(1.95f).Within(1e-5f));
            Assert.That(nudged.z, Is.EqualTo(3f));
        }

        [Test]
        public void 회전_넛지는_현재_회전의_로컬_축_기준이다()
        {
            Quaternion baseRotation = Quaternion.Euler(0f, 180f, 0f);
            Quaternion nudged = ViewLabMath.NudgeRotation(baseRotation, axis: 0, signedStep: 5f);
            Quaternion expected = baseRotation * Quaternion.Euler(5f, 0f, 0f);
            Assert.That(Quaternion.Angle(nudged, expected), Is.LessThan(1e-3f));
        }

        [Test]
        public void 스케일_넛지는_하한_밑으로_내려가지_않는다()
        {
            Vector3 nudged = ViewLabMath.NudgeUniformScale(new Vector3(0.02f, 0.02f, 0.02f), -0.05f);
            Assert.That(nudged.x, Is.EqualTo(0.01f).Within(1e-6f));
            Assert.That(nudged.y, Is.EqualTo(nudged.x));
            Assert.That(nudged.z, Is.EqualTo(nudged.x));
        }

        // ── Speed ↔ 이동 상태 매핑 ──

        [Test]
        public void 이동_상태_매핑은_블렌드_트리_임계값과_같다()
        {
            Assert.That(ViewLabMath.SpeedForTier(LocomotionTier.Idle, WalkSpeed, RunSpeed), Is.EqualTo(0f));
            Assert.That(ViewLabMath.SpeedForTier(LocomotionTier.Walk, WalkSpeed, RunSpeed), Is.EqualTo(WalkSpeed));
            Assert.That(ViewLabMath.SpeedForTier(LocomotionTier.Run, WalkSpeed, RunSpeed), Is.EqualTo(RunSpeed));
        }

        // ── dirty 판정 ──

        [Test]
        public void 허용_오차_이내_차이는_변경이_아니다()
        {
            bool differs = ViewLabMath.TrsDiffers(
                Vector3.zero, Quaternion.identity, Vector3.one,
                new Vector3(0.0001f, 0f, 0f), Quaternion.identity, Vector3.one);
            Assert.That(differs, Is.False);
        }

        [Test]
        public void 스케일만_달라도_변경으로_판정한다()
        {
            // HarpoonPivot scale 0.75 — 위치·회전만 다루면 스케일이 유실된다 (계획 §7).
            bool differs = ViewLabMath.TrsDiffers(
                Vector3.zero, Quaternion.identity, new Vector3(0.75f, 0.75f, 0.75f),
                Vector3.zero, Quaternion.identity, Vector3.one);
            Assert.That(differs, Is.True);
        }

        [Test]
        public void 회전_차이는_각도로_판정한다()
        {
            bool differs = ViewLabMath.TrsDiffers(
                Vector3.zero, Quaternion.Euler(0f, 0.5f, 0f), Vector3.one,
                Vector3.zero, Quaternion.identity, Vector3.one);
            Assert.That(differs, Is.True);
        }

        // ── 피벗 이름 매칭 (저장 대상 열거) ──

        [Test]
        public void 피벗_이름_매칭은_직속_자식만_찾는다()
        {
            var parent = new GameObject("AimPivot");
            var child = new GameObject("HarpoonPivot");
            var grandchild = new GameObject("Weapon_Harpoon_Launcher");
            try
            {
                child.transform.SetParent(parent.transform);
                grandchild.transform.SetParent(child.transform);

                Assert.That(ViewLabMath.FindChildByName(parent.transform, "HarpoonPivot"),
                    Is.EqualTo(child.transform));
                Assert.That(ViewLabMath.FindChildByName(parent.transform, "Weapon_Harpoon_Launcher"),
                    Is.Null);
                Assert.That(ViewLabMath.FindChildByName(parent.transform, "RiflePivot"), Is.Null);
                Assert.That(ViewLabMath.FindChildByName(null, "HarpoonPivot"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
