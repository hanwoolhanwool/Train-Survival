using System.Collections.Generic;
using Game.Systems.Networking.Lobby;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 대기실 칸 배정 규칙 검증 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §9.1.
    ///
    /// <para>지키려는 것은 넷이다 — <b>호스트는 언제나 첫 칸</b>, <b>나머지는 접속 순서</b>,
    /// <b>빈자리는 뒤로 몰린다</b>, 그리고 <b>정원을 넘겨도 죽지 않는다.</b></para>
    ///
    /// <para>마지막 하나가 특히 중요하다. 5번째 접속을 막는 자리는 승인 콜백이지 여기가 아니고,
    /// 여기서 예외를 던지면 <b>대기실이 통째로 죽는다.</b></para>
    /// </summary>
    public sealed class RosterOrderingTests
    {
        private const ulong Host = 0UL;

        [Test]
        public void 호스트는_언제나_첫_칸이다()
        {
            var slots = new ulong[RosterOrdering.Capacity];

            // 접속 순서에서 호스트가 꼴찌여도 첫 칸으로 온다.
            int count = RosterOrdering.Arrange(new List<ulong> { 7UL, 3UL, Host }, Host, slots);

            Assert.AreEqual(3, count);
            Assert.AreEqual(Host, slots[0], "호스트가 첫 칸이 아니다");
        }

        [Test]
        public void 게스트는_접속_순서대로_앉는다()
        {
            var slots = new ulong[RosterOrdering.Capacity];
            int count = RosterOrdering.Arrange(new List<ulong> { Host, 5UL, 9UL }, Host, slots);

            Assert.AreEqual(3, count);
            Assert.AreEqual(Host, slots[0]);
            Assert.AreEqual(5UL, slots[1]);
            Assert.AreEqual(9UL, slots[2]);
        }

        [Test]
        public void 가운데가_빠지면_뒤가_당겨진다()
        {
            var slots = new ulong[RosterOrdering.Capacity];
            RosterOrdering.Arrange(new List<ulong> { Host, 5UL, 9UL }, Host, slots);

            // 5번이 나갔다 — 9번이 앞으로 온다. 칸 사이에 구멍이 남으면 안 된다.
            int count = RosterOrdering.Arrange(new List<ulong> { Host, 9UL }, Host, slots);

            Assert.AreEqual(2, count);
            Assert.AreEqual(Host, slots[0]);
            Assert.AreEqual(9UL, slots[1], "빈 칸이 가운데에 남았다");
        }

        [Test]
        public void 호스트가_혼자여도_성립한다()
        {
            var slots = new ulong[RosterOrdering.Capacity];
            int count = RosterOrdering.Arrange(new List<ulong> { Host }, Host, slots);

            Assert.AreEqual(1, count);
            Assert.AreEqual(Host, slots[0]);
        }

        [Test]
        public void 정원을_넘겨도_예외가_없다()
        {
            var slots = new ulong[RosterOrdering.Capacity];
            var crowd = new List<ulong> { Host, 1UL, 2UL, 3UL, 4UL, 5UL };

            int count = RosterOrdering.Arrange(crowd, Host, slots);

            Assert.AreEqual(RosterOrdering.Capacity, count, "정원보다 많이 앉혔다");
        }

        [Test]
        public void 빈_입력과_null도_예외가_없다()
        {
            var slots = new ulong[RosterOrdering.Capacity];

            Assert.AreEqual(0, RosterOrdering.Arrange(new List<ulong>(), Host, slots));
            Assert.AreEqual(0, RosterOrdering.Arrange(null, Host, slots));
            Assert.AreEqual(0, RosterOrdering.Arrange(new List<ulong> { Host }, Host, null));
        }

        [Test]
        public void 칸_배열이_정원보다_짧아도_넘치지_않는다()
        {
            var slots = new ulong[2];
            int count = RosterOrdering.Arrange(new List<ulong> { Host, 1UL, 2UL, 3UL }, Host, slots);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void 호스트가_목록에_없으면_앉히지_않는다()
        {
            // 호스트가 아직 등록되기 전의 찰나 — 게스트를 첫 칸에 올려 호스트로 보이게 하면 안 된다.
            var slots = new ulong[RosterOrdering.Capacity];
            int count = RosterOrdering.Arrange(new List<ulong> { 5UL, 9UL }, Host, slots);

            Assert.AreEqual(2, count);
            Assert.AreEqual(5UL, slots[0]);
            Assert.AreEqual(9UL, slots[1]);
        }

        [Test]
        public void 표시_이름은_칸_번호에서_나온다()
        {
            Assert.AreEqual("플레이어 1", RosterOrdering.DisplayName(0));
            Assert.AreEqual("플레이어 2", RosterOrdering.DisplayName(1));
            Assert.AreEqual("플레이어 3", RosterOrdering.DisplayName(2));
            Assert.AreEqual("플레이어 4", RosterOrdering.DisplayName(3));
        }

        [Test]
        public void 정원은_넷이다()
        {
            // 패널 그림이 4장 고정이고 Steam 로비도 친구 전용 4인이다 — 늘리려면 그림부터 바꿔야 한다.
            Assert.AreEqual(4, RosterOrdering.Capacity);
        }
    }
}
