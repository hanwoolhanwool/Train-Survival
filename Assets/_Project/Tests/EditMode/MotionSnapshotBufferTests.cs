using Game.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>저주기 스냅샷 보간 버퍼 검증 (네트워크 문서 §6.2 — 10~15Hz + 보간).</summary>
    public sealed class MotionSnapshotBufferTests
    {
        [Test]
        public void 스냅샷이_없으면_샘플이_실패한다()
        {
            var buffer = new MotionSnapshotBuffer();

            Assert.That(buffer.TrySample(1.0, out _, out _), Is.False);
        }

        [Test]
        public void 두_스냅샷_사이는_선형_보간된다()
        {
            var buffer = new MotionSnapshotBuffer();
            buffer.AddSnapshot(new Vector3(0f, 0f, 0f), 0f, 1.0);
            buffer.AddSnapshot(new Vector3(0f, 0f, 10f), 90f, 2.0);

            Assert.That(buffer.TrySample(1.5, out Vector3 position, out float yaw), Is.True);
            Assert.That(position.z, Is.EqualTo(5f).Within(0.001f));
            Assert.That(yaw, Is.EqualTo(45f).Within(0.001f));
        }

        [Test]
        public void 범위_밖_렌더_시각은_끝_값으로_고정되고_외삽하지_않는다()
        {
            // 외삽 금지 — 워프/역행 방지 (§6.2 보간 원칙).
            var buffer = new MotionSnapshotBuffer();
            buffer.AddSnapshot(new Vector3(0f, 0f, 3f), 10f, 1.0);
            buffer.AddSnapshot(new Vector3(0f, 0f, 7f), 20f, 2.0);

            buffer.TrySample(0.5, out Vector3 before, out _);
            buffer.TrySample(9.9, out Vector3 after, out _);

            Assert.That(before.z, Is.EqualTo(3f).Within(0.001f));
            Assert.That(after.z, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void 방향_보간은_각도_경계를_넘어_최단_경로를_따른다()
        {
            var buffer = new MotionSnapshotBuffer();
            buffer.AddSnapshot(Vector3.zero, 350f, 1.0);
            buffer.AddSnapshot(Vector3.zero, 10f, 2.0);

            buffer.TrySample(1.5, out _, out float yaw);

            Assert.That(Mathf.DeltaAngle(yaw, 0f), Is.EqualTo(0f).Within(0.001f), "350°→10°의 중간은 0°");
        }

        [Test]
        public void 오래된_스냅샷은_자동_정리된다()
        {
            var buffer = new MotionSnapshotBuffer();
            for (int i = 0; i < 100; i++)
            {
                buffer.AddSnapshot(new Vector3(0f, 0f, i), 0f, i * 0.1);
            }

            Assert.That(buffer.Count, Is.LessThan(20), "1초 초과분은 버려진다");
        }

        [Test]
        public void 초기화하면_비워진다()
        {
            var buffer = new MotionSnapshotBuffer();
            buffer.AddSnapshot(Vector3.one, 0f, 1.0);

            buffer.Clear();

            Assert.That(buffer.Count, Is.EqualTo(0));
            Assert.That(buffer.TrySample(1.0, out _, out _), Is.False);
        }
    }
}
