using System;
using Game.Systems.Networking.Lobby;
using Game.UI.Ready;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 난이도 스테퍼 검증 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §9.1.
    ///
    /// <para>고정할 것은 넷이다 — <b>3단계 순환</b>, <b>기본값이 가운데 "보통"</b>,
    /// <b>범위 밖 인덱스 방어</b>, 그리고 <b>단계 수가 1일 때 예외 없음</b>.</para>
    ///
    /// <para>마지막 하나가 실제로 위험한 자리다. 단계 수로 나머지를 구하는 코드라
    /// 0이나 1이 들어오면 나누기에서 죽거나 무한히 제자리를 돈다 — 대기실이 통째로 멈춘다.</para>
    /// </summary>
    public sealed class DifficultyStepperTests
    {
        [Test]
        public void 기본값은_가운데다()
        {
            Assert.AreEqual(1, DifficultyStepper.DefaultIndex, "기본값이 가운데가 아니다");
            Assert.AreEqual("보통", DifficultyStepper.Name(DifficultyStepper.DefaultIndex));
        }

        [Test]
        public void 세_단계가_앞으로_순환한다()
        {
            int index = 0;

            index = DifficultyStepper.Next(index, DifficultyStepper.Count);
            Assert.AreEqual(1, index);

            index = DifficultyStepper.Next(index, DifficultyStepper.Count);
            Assert.AreEqual(2, index);

            // 어려움에서 한 번 더 — 쉬움으로 돌아온다.
            index = DifficultyStepper.Next(index, DifficultyStepper.Count);
            Assert.AreEqual(0, index, "마지막에서 처음으로 돌아오지 않는다");
        }

        [Test]
        public void 세_단계가_뒤로_순환한다()
        {
            // 쉬움에서 ◀ — 어려움으로 넘어간다.
            Assert.AreEqual(2, DifficultyStepper.Prev(0, DifficultyStepper.Count));
            Assert.AreEqual(1, DifficultyStepper.Prev(2, DifficultyStepper.Count));
            Assert.AreEqual(0, DifficultyStepper.Prev(1, DifficultyStepper.Count));
        }

        [Test]
        public void 범위_밖_인덱스를_접어_넣는다()
        {
            Assert.AreEqual(0, DifficultyStepper.Clamp(3, 3));
            Assert.AreEqual(2, DifficultyStepper.Clamp(-1, 3), "음수가 음수로 남았다");
            Assert.AreEqual(1, DifficultyStepper.Clamp(-5, 3));
            Assert.AreEqual(1, DifficultyStepper.Clamp(7, 3));
        }

        [Test]
        public void 단계가_하나여도_예외가_없다()
        {
            Assert.AreEqual(0, DifficultyStepper.DefaultFor(1));
            Assert.AreEqual(0, DifficultyStepper.Next(0, 1));
            Assert.AreEqual(0, DifficultyStepper.Prev(0, 1));
            Assert.AreEqual(0, DifficultyStepper.Clamp(9, 1));
        }

        [Test]
        public void 단계가_없어도_예외가_없다()
        {
            // 있을 수 없는 입력이지만, 나머지 연산이 0으로 나누면 대기실이 통째로 멈춘다.
            Assert.AreEqual(0, DifficultyStepper.Next(0, 0));
            Assert.AreEqual(0, DifficultyStepper.Prev(0, 0));
            Assert.AreEqual(0, DifficultyStepper.Clamp(4, 0));
            Assert.AreEqual(0, DifficultyStepper.DefaultFor(0));
        }

        [Test]
        public void 단계_이름은_쉬움_보통_어려움이다()
        {
            Assert.AreEqual("쉬움", DifficultyStepper.Name(0));
            Assert.AreEqual("보통", DifficultyStepper.Name(1));
            Assert.AreEqual("어려움", DifficultyStepper.Name(2));
        }

        [Test]
        public void 단계_이름은_범위_밖에서도_비지_않는다()
        {
            Assert.IsNotEmpty(DifficultyStepper.Name(-1));
            Assert.IsNotEmpty(DifficultyStepper.Name(99));
        }

        [Test]
        public void 단계_수는_실려_가는_값의_개수와_같다()
        {
            // 여기가 어긋나면 UI가 고를 수 있는 단계와 네트워크로 나르는 값이 갈린다 —
            // 화면에는 있는데 실려 가지 않는 난이도가 생긴다.
            Assert.AreEqual(
                Enum.GetValues(typeof(GameDifficulty)).Length,
                DifficultyStepper.Count,
                "단계 수와 GameDifficulty의 값 개수가 다르다");
        }

        [Test]
        public void 단계와_실려_가는_값이_서로_옮겨진다()
        {
            Assert.AreEqual(GameDifficulty.Easy, DifficultyStepper.ToLevel(0));
            Assert.AreEqual(GameDifficulty.Normal, DifficultyStepper.ToLevel(1));
            Assert.AreEqual(GameDifficulty.Hard, DifficultyStepper.ToLevel(2));

            Assert.AreEqual(0, DifficultyStepper.ToIndex(GameDifficulty.Easy));
            Assert.AreEqual(1, DifficultyStepper.ToIndex(GameDifficulty.Normal));
            Assert.AreEqual(2, DifficultyStepper.ToIndex(GameDifficulty.Hard));
        }

        [Test]
        public void 기본값은_실려_가는_기본값과_같다()
        {
            // 대기실 상태의 NetworkVariable 초기값이 Normal이다 — 화면과 어긋나면
            // 처음 열었을 때 보이는 값과 실제로 실려 가는 값이 다르다.
            Assert.AreEqual(GameDifficulty.Normal, DifficultyStepper.ToLevel(DifficultyStepper.DefaultIndex));
        }
    }
}
