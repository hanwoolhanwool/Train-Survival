using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 거치 무기 점유 판정 검증 (M7 4차 §2.2 — 결정 ⑤).
    /// 이 축의 핵심은 셋이다: <b>한 무기에 1인</b>, <b>한 사람에 하나</b>, 그리고
    /// <b>유령 점유를 남기지 않는 강제 하차</b>. 물리·네트워크 조회는 호출부가 하고
    /// 규칙은 전부 여기서 끝난다.
    /// </summary>
    public sealed class MountOccupancyLogicTests
    {
        private static MountOccupancy Item(ushort structureId, ulong clientId)
        {
            return new MountOccupancy { StructureId = structureId, OccupantClientId = clientId };
        }

        // ── CanMount ──────────────────────────────────────────────────────

        [Test]
        public void 빈_자리는_승인된다()
        {
            Assert.That(
                MountOccupancyLogic.CanMount(true, true, true, false, 4f, 6.25f),
                Is.EqualTo(MountRejectReason.None));
        }

        [Test]
        public void 거치_무기가_아니면_거부한다()
        {
            Assert.That(
                MountOccupancyLogic.CanMount(false, true, true, false, 0f, 6.25f),
                Is.EqualTo(MountRejectReason.NotMountedWeapon));
        }

        [Test]
        public void 자동_터렛에는_붙을_수_없다()
        {
            // 터렛의 탄은 점유가 아니라 1회 상호작용으로 채운다 (B단계).
            Assert.That(
                MountOccupancyLogic.CanMount(true, false, true, false, 0f, 6.25f),
                Is.EqualTo(MountRejectReason.Automated));
        }

        [Test]
        public void 죽은_건축물은_거부한다()
        {
            Assert.That(
                MountOccupancyLogic.CanMount(true, true, false, false, 0f, 6.25f),
                Is.EqualTo(MountRejectReason.Destroyed));
        }

        [Test]
        public void 이미_점유_중이면_거부한다()
        {
            Assert.That(
                MountOccupancyLogic.CanMount(true, true, true, true, 0f, 6.25f),
                Is.EqualTo(MountRejectReason.Occupied));
        }

        [Test]
        public void 좌석_반경_밖이면_거부한다()
        {
            Assert.That(
                MountOccupancyLogic.CanMount(true, true, true, false, 6.26f, 6.25f),
                Is.EqualTo(MountRejectReason.TooFar));
        }

        [Test]
        public void 좌석_반경_경계는_승인이다()
        {
            // 경계에서 튕기면 "붙었다 떨어졌다"가 반복된다 — 같거나 안쪽은 승인.
            Assert.That(
                MountOccupancyLogic.CanMount(true, true, true, false, 6.25f, 6.25f),
                Is.EqualTo(MountRejectReason.None));
        }

        [Test]
        public void 파괴와_거리가_겹치면_파괴가_먼저_보고된다()
        {
            // 사유가 사실을 가리지 않게 판정 순서를 고정한다 — 정체 → 생존 → 경합 → 거리.
            Assert.That(
                MountOccupancyLogic.CanMount(true, true, false, true, 999f, 6.25f),
                Is.EqualTo(MountRejectReason.Destroyed));
        }

        // ── 강제 하차 4사유 ────────────────────────────────────────────────

        [Test]
        public void 정상_상태에서는_하차하지_않는다()
        {
            Assert.That(MountOccupancyLogic.ShouldForceDismount(true, true, true), Is.False);
        }

        [Test]
        public void 건축물이_사라지면_하차한다()
        {
            // 파괴·철거·칸 파괴가 모두 이 한 축으로 들어온다.
            Assert.That(MountOccupancyLogic.ShouldForceDismount(false, true, true), Is.True);
        }

        [Test]
        public void 점유자가_죽으면_하차한다()
        {
            Assert.That(MountOccupancyLogic.ShouldForceDismount(true, false, true), Is.True);
        }

        [Test]
        public void 점유자가_끊기면_하차한다()
        {
            // 점유는 세션 순간 상태다 — 재접속 복원 대상이 아니다 (§2.7).
            Assert.That(MountOccupancyLogic.ShouldForceDismount(true, true, false), Is.True);
        }

        // ── 조회 ──────────────────────────────────────────────────────────

        [Test]
        public void 건축물_Id로_점유를_찾는다()
        {
            MountOccupancy[] list = { Item(3, 10), Item(7, 11) };

            Assert.That(MountOccupancyLogic.TryFindByStructure(list, 7, out int index), Is.True);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(MountOccupancyLogic.TryFindByStructure(list, 9, out int missing), Is.False);
            Assert.That(missing, Is.EqualTo(-1));
        }

        [Test]
        public void 무효_Id와_빈_목록은_비점유다()
        {
            Assert.That(MountOccupancyLogic.TryFindByStructure(null, 3, out _), Is.False);
            Assert.That(MountOccupancyLogic.TryFindByStructure(new MountOccupancy[0], 3, out _), Is.False);
            Assert.That(MountOccupancyLogic.TryFindByStructure(new[] { Item(3, 10) }, 0, out _), Is.False);
        }

        [Test]
        public void 한_사람은_하나만_점유한다()
        {
            // 같은 사람이 다른 무기를 요청하면 호출부가 이 항목을 먼저 지운다.
            MountOccupancy[] list = { Item(3, 10), Item(7, 11) };

            Assert.That(MountOccupancyLogic.TryFindByClient(list, 11, out int index), Is.True);
            Assert.That(list[index].StructureId, Is.EqualTo(7));
            Assert.That(MountOccupancyLogic.TryFindByClient(list, 12, out _), Is.False);
        }

        [Test]
        public void 남의_무기로_보낸_보고는_점유로_인정되지_않는다()
        {
            MountOccupancy[] list = { Item(3, 10) };

            Assert.That(MountOccupancyLogic.IsOccupiedBy(list, 3, 10), Is.True);
            Assert.That(MountOccupancyLogic.IsOccupiedBy(list, 3, 11), Is.False);
            Assert.That(MountOccupancyLogic.IsOccupiedBy(list, 4, 10), Is.False);
        }
    }
}
