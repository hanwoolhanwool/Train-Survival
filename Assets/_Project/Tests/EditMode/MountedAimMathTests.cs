using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 거치 무기 조준 수학 검증 (M7 4차 §2.3·§2.4).
    /// 사각 제한은 아군 오사와 "포신이 칸을 뚫는" 그림을 데이터로 막는 축이므로,
    /// <b>클램프가 성립하는 것</b>과 <b>서버가 되돌려 본 각이 같은 것</b>이 이 파일의 핵심이다.
    /// 각도 규약: yaw는 좌우(오른쪽 +), pitch는 앙각(위 +).
    /// </summary>
    public sealed class MountedAimMathTests
    {
        [Test]
        public void 각도는_반바퀴_범위로_접힌다()
        {
            Assert.That(MountedAimMath.NormalizeAngle(0f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(MountedAimMath.NormalizeAngle(190f), Is.EqualTo(-170f).Within(0.001f));
            Assert.That(MountedAimMath.NormalizeAngle(-190f), Is.EqualTo(170f).Within(0.001f));
            Assert.That(MountedAimMath.NormalizeAngle(360f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(MountedAimMath.NormalizeAngle(720f + 45f), Is.EqualTo(45f).Within(0.001f));
        }

        [Test]
        public void 사각_안의_각은_그대로_통과한다()
        {
            MountedAimMath.Clamp(80f, 20f, 110f, -15f, 40f, out float yaw, out float pitch);

            Assert.That(yaw, Is.EqualTo(80f).Within(0.001f));
            Assert.That(pitch, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void 좌우_한계를_넘으면_접힌다()
        {
            MountedAimMath.Clamp(150f, 0f, 110f, -15f, 40f, out float right, out _);
            MountedAimMath.Clamp(-150f, 0f, 110f, -15f, 40f, out float left, out _);

            Assert.That(right, Is.EqualTo(110f).Within(0.001f));
            Assert.That(left, Is.EqualTo(-110f).Within(0.001f));
        }

        [Test]
        public void 앙각은_아래위_한계로_접힌다()
        {
            MountedAimMath.Clamp(0f, 80f, 110f, -15f, 40f, out _, out float up);
            MountedAimMath.Clamp(0f, -80f, 110f, -15f, 40f, out _, out float down);

            Assert.That(up, Is.EqualTo(40f).Within(0.001f));
            Assert.That(down, Is.EqualTo(-15f).Within(0.001f));
        }

        [Test]
        public void 한계가_반바퀴_이상이면_좌우_제한이_없다()
        {
            // 사각을 열어 둔 무기(향후 종류)를 위한 여지 — 데이터만으로 제한이 사라진다.
            MountedAimMath.Clamp(170f, 0f, 180f, -89f, 89f, out float yaw, out _);

            Assert.That(yaw, Is.EqualTo(170f).Within(0.001f));
        }

        [Test]
        public void 사각_판정은_클램프와_같은_경계를_본다()
        {
            Assert.That(MountedAimMath.IsWithinArc(109f, 30f, 110f, -15f, 40f, 0f), Is.True);
            Assert.That(MountedAimMath.IsWithinArc(111f, 30f, 110f, -15f, 40f, 0f), Is.False);
            Assert.That(MountedAimMath.IsWithinArc(0f, 41f, 110f, -15f, 40f, 0f), Is.False);
            Assert.That(MountedAimMath.IsWithinArc(0f, -16f, 110f, -15f, 40f, 0f), Is.False);
        }

        [Test]
        public void 허용_오차는_지연_중_보고를_살려_준다()
        {
            // 조작 계층이 클램프를 지켜도 왕복 사이 한 프레임의 오차가 낀다 — 1도는 열어 둔다.
            Assert.That(MountedAimMath.IsWithinArc(110.5f, 40.5f, 110f, -15f, 40f), Is.True);
            Assert.That(MountedAimMath.IsWithinArc(120f, 40f, 110f, -15f, 40f), Is.False);
        }

        [Test]
        public void 정면_조준은_거치대_전방이다()
        {
            Vector3 forward = MountedAimMath.ResolveForward(Quaternion.identity, 0f, 0f);

            Assert.That(forward.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(forward.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(forward.z, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 앙각_양수는_위를_향한다()
        {
            // 화면 좌표계는 아래가 +다 — 이 부호를 놓치면 포신이 반대로 든다.
            Vector3 forward = MountedAimMath.ResolveForward(Quaternion.identity, 0f, 90f);

            Assert.That(forward.y, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 오른쪽_yaw는_오른쪽을_향한다()
        {
            Vector3 forward = MountedAimMath.ResolveForward(Quaternion.identity, 90f, 0f);

            Assert.That(forward.x, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 방향과_각은_서로_되돌릴_수_있다()
        {
            // 서버는 보고된 월드 방향을 이 역함수로 되돌려 사각을 본다 — 왕복이 어긋나면 정상 발사가 기각된다.
            Quaternion mount = MountedAimMath.ResolveMountRotation(1);
            Vector3 forward = MountedAimMath.ResolveForward(mount, -37f, 22f);

            Assert.That(MountedAimMath.TryResolveAim(mount, forward, out float yaw, out float pitch), Is.True);
            Assert.That(yaw, Is.EqualTo(-37f).Within(0.01f));
            Assert.That(pitch, Is.EqualTo(22f).Within(0.01f));
        }

        [Test]
        public void 길이가_없는_방향은_판정할_수_없다()
        {
            Assert.That(
                MountedAimMath.TryResolveAim(Quaternion.identity, Vector3.zero, out _, out _),
                Is.False);
        }

        [Test]
        public void 거치대_회전은_설치_회전만이_진실이다()
        {
            // 뷰 트랜스폼을 읽지 않으므로 서버·클라·뷰 미스폰 피어가 같은 값을 얻는다.
            Assert.That(MountedAimMath.ResolveMountRotation(1).eulerAngles.y, Is.EqualTo(90f).Within(0.01f));
            Assert.That(MountedAimMath.ResolveMountRotation(2).eulerAngles.y, Is.EqualTo(180f).Within(0.01f));

            // 4 이상은 마스크로 접힌다 — 조작된 회전 값이 들어와도 네 방향을 벗어나지 않는다.
            Assert.That(MountedAimMath.ResolveMountRotation(5).eulerAngles.y, Is.EqualTo(90f).Within(0.01f));
        }
    }
}
