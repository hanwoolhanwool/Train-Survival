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

        // ── 좌우 손 지정 (미러링) ──

        [Test]
        public void 손_판정은_로컬X_부호를_따른다()
        {
            // RevolverPivot 0.27 / HarpoonPivot 0.3588 = 오른팔, MeleePivot 0 = 중앙.
            Assert.That(ViewLabMath.ResolveHand(0.27f), Is.EqualTo(PivotHand.Right));
            Assert.That(ViewLabMath.ResolveHand(-0.3588f), Is.EqualTo(PivotHand.Left));
            Assert.That(ViewLabMath.ResolveHand(0f), Is.EqualTo(PivotHand.Center));
        }

        [Test]
        public void 중앙_판정_폭은_최소_넛지보다_좁다()
        {
            // 미세 스텝 0.005로 한 번만 밀어도 손이 정해져야 한다.
            Assert.That(ViewLabMath.ResolveHand(0.005f), Is.EqualTo(PivotHand.Right));
            Assert.That(ViewLabMath.ResolveHand(-0.005f), Is.EqualTo(PivotHand.Left));
        }

        [Test]
        public void 미러_위치는_X만_뒤집는다()
        {
            Vector3 mirrored = ViewLabMath.MirrorPosition(new Vector3(0.3588f, -0.6005f, 0.7767f));
            Assert.That(mirrored.x, Is.EqualTo(-0.3588f).Within(1e-6f));
            Assert.That(mirrored.y, Is.EqualTo(-0.6005f).Within(1e-6f));
            Assert.That(mirrored.z, Is.EqualTo(0.7767f).Within(1e-6f));
        }

        [Test]
        public void 미러_위치는_손을_반대편으로_옮긴다()
        {
            Vector3 right = new Vector3(0.27f, -0.2778f, 0.502f);
            Vector3 left = ViewLabMath.MirrorPosition(right);
            Assert.That(ViewLabMath.ResolveHand(right.x), Is.EqualTo(PivotHand.Right));
            Assert.That(ViewLabMath.ResolveHand(left.x), Is.EqualTo(PivotHand.Left));
        }

        [Test]
        public void 미러_회전은_반사_행렬과_같은_결과를_낸다()
        {
            // M·R·M (M = diag(-1,1,1))과 일치하는지 임의 벡터로 확인 — 공식 유도의 실증.
            Quaternion rotation = Quaternion.Euler(15.71f, -0.51f, 110.41f);   // HammerPivot 실측값
            Quaternion mirrored = ViewLabMath.MirrorRotation(rotation);

            foreach (Vector3 v in new[] { Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.3f, -0.7f, 0.6f) })
            {
                Vector3 expected = Mirror(rotation * Mirror(v));
                Assert.That((mirrored * v - expected).magnitude, Is.LessThan(1e-4f),
                    $"벡터 {v}에서 미러 회전이 반사 행렬과 어긋났다");
            }
        }

        [Test]
        public void 미러_회전을_두_번_하면_원래대로_돌아온다()
        {
            Quaternion rotation = Quaternion.Euler(15.71f, -0.51f, 110.41f);
            Quaternion round = ViewLabMath.MirrorRotation(ViewLabMath.MirrorRotation(rotation));
            Assert.That(Quaternion.Angle(rotation, round), Is.LessThan(1e-3f));
        }

        [Test]
        public void Y축_180도_회전은_미러링해도_그대로다()
        {
            // 총기 3종의 초기 회전 — 좌우 대칭 자세라 미러 후에도 같아야 한다.
            Quaternion yaw180 = Quaternion.Euler(0f, 180f, 0f);
            Assert.That(Quaternion.Angle(ViewLabMath.MirrorRotation(yaw180), yaw180), Is.LessThan(1e-3f));
        }

        private static Vector3 Mirror(Vector3 v)
        {
            return new Vector3(-v.x, v.y, v.z);
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
