using Game.UI.MainMenu;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 메뉴 상하 이동 검증 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §8.1.
    ///
    /// <para>고정하려는 것 셋 — <b>순환</b>, <b>잠긴 항목 건너뛰기</b>, <b>항목이 없어도 예외 없음</b>.
    /// 앞의 둘은 화면이 멈춘 것처럼 보이는 두 가지 경우를 막고, 마지막은 세션 준비 전처럼
    /// 전부 잠긴 순간에 내비게이션이 터지지 않게 한다.</para>
    /// </summary>
    public sealed class MenuNavigationTests
    {
        private static readonly bool[] AllOn = { true, true, true, true };

        [Test]
        public void 아래로_한_칸씩_내려간다()
        {
            Assert.AreEqual(1, MenuNavigation.Move(0, AllOn, 1));
            Assert.AreEqual(2, MenuNavigation.Move(1, AllOn, 1));
            Assert.AreEqual(3, MenuNavigation.Move(2, AllOn, 1));
        }

        [Test]
        public void 끝에서_반대편으로_돌아온다()
        {
            Assert.AreEqual(0, MenuNavigation.Move(3, AllOn, 1), "맨 아래에서 아래로 → 맨 위");
            Assert.AreEqual(3, MenuNavigation.Move(0, AllOn, -1), "맨 위에서 위로 → 맨 아래");
        }

        [Test]
        public void 잠긴_항목은_건너뛴다()
        {
            bool[] lockedMiddle = { true, false, false, true };

            Assert.AreEqual(3, MenuNavigation.Move(0, lockedMiddle, 1), "1·2가 잠겼으면 3으로");
            Assert.AreEqual(0, MenuNavigation.Move(3, lockedMiddle, 1), "3에서 아래로 → 순환해서 0");
            Assert.AreEqual(0, MenuNavigation.Move(3, lockedMiddle, -1), "3에서 위로 → 1·2를 건너뛴 0");
        }

        [Test]
        public void 하나만_열려_있으면_제자리에_머문다()
        {
            bool[] onlyFirst = { true, false, false, false };

            Assert.AreEqual(0, MenuNavigation.Move(0, onlyFirst, 1));
            Assert.AreEqual(0, MenuNavigation.Move(0, onlyFirst, -1));
        }

        [Test]
        public void 전부_잠기면_갈_곳이_없다()
        {
            bool[] allOff = { false, false, false, false };

            Assert.AreEqual(MenuNavigation.None, MenuNavigation.Move(0, allOff, 1));
            Assert.AreEqual(MenuNavigation.None, MenuNavigation.First(allOff));
            Assert.AreEqual(MenuNavigation.None, MenuNavigation.Rescue(2, allOff));
        }

        [Test]
        public void 항목이_없어도_예외가_없다()
        {
            bool[] empty = new bool[0];

            Assert.AreEqual(MenuNavigation.None, MenuNavigation.Move(0, empty, 1));
            Assert.AreEqual(MenuNavigation.None, MenuNavigation.Move(5, null, -1));
            Assert.AreEqual(MenuNavigation.None, MenuNavigation.First(null));
            Assert.AreEqual(MenuNavigation.None, MenuNavigation.Rescue(0, empty));
            Assert.AreEqual(0, MenuNavigation.Normalize(7, 0));
        }

        [Test]
        public void 범위를_벗어난_현재값도_접어서_받는다()
        {
            Assert.AreEqual(0, MenuNavigation.Normalize(4, 4));
            Assert.AreEqual(3, MenuNavigation.Normalize(-1, 4));
            Assert.AreEqual(1, MenuNavigation.Normalize(-7, 4));

            Assert.AreEqual(1, MenuNavigation.Move(4, AllOn, 1), "4는 0과 같은 자리");
            Assert.AreEqual(3, MenuNavigation.Move(-4, AllOn, -1));
        }

        [Test]
        public void 첫_유효_항목을_찾는다()
        {
            Assert.AreEqual(0, MenuNavigation.First(AllOn));
            Assert.AreEqual(2, MenuNavigation.First(new[] { false, false, true, true }));
        }

        [Test]
        public void 잠긴_자리에_있으면_가장_가까운_곳으로_구조된다()
        {
            bool[] firstLocked = { false, true, true, true };

            Assert.AreEqual(1, MenuNavigation.Rescue(0, firstLocked), "잠긴 0에서 아래쪽 1로");
            Assert.AreEqual(2, MenuNavigation.Rescue(2, firstLocked), "이미 유효하면 그대로");
        }
    }
}
