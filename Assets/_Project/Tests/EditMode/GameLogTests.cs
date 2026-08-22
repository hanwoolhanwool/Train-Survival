using System.Collections.Generic;
using Game.Core.Logging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// <see cref="GameLog"/>의 계약을 고정한다 — 접두어 형식·카테고리 필터·횟수 상한.
    /// 로그는 <see cref="Application.logMessageReceived"/>로 직접 받아 세므로
    /// "출력되지 않아야 한다"까지 검증할 수 있다.
    /// </summary>
    public class GameLogTests
    {
        private readonly List<(LogType Type, string Message)> _captured = new List<(LogType, string)>();
        private LogCategory _savedEnabled;

        [SetUp]
        public void SetUp()
        {
            _savedEnabled = GameLog.Enabled;
            GameLog.ResetAllLimits();
            _captured.Clear();
            Application.logMessageReceived += Capture;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= Capture;
            GameLog.Enabled = _savedEnabled;
            GameLog.ResetAllLimits();
        }

        private string Dump() => "캡처=[" + string.Join(" || ", _captured.ConvertAll(c => c.Type + ":" + c.Message)) + "]";

        private void Capture(string condition, string stackTrace, LogType type)
        {
            _captured.Add((type, condition));
        }

        [Test]
        public void Info_카테고리와_호출한_스크립트가_접두어로_붙는다()
        {
            GameLog.EnableAll();

            GameLog.Info(LogCategory.Harpoon, "본문");

            Assert.AreEqual(1, _captured.Count, Dump());
            Assert.AreEqual("[Harpoon/GameLogTests] 본문", _captured[0].Message);
        }

        [Test]
        public void Warn_경고_수준으로_나간다()
        {
            GameLog.EnableAll();

            GameLog.Warn(LogCategory.Net, "경고 본문");

            Assert.AreEqual(1, _captured.Count, Dump());
            Assert.AreEqual(LogType.Warning, _captured[0].Type);
            Assert.AreEqual("[Net/GameLogTests] 경고 본문", _captured[0].Message);
        }

        [Test]
        public void Info_꺼진_카테고리는_출력하지_않는다()
        {
            GameLog.Only(LogCategory.Monsters);

            GameLog.Info(LogCategory.Harpoon, "나오면 안 된다");
            GameLog.Info(LogCategory.Monsters, "나와야 한다");

            Assert.AreEqual(1, _captured.Count, Dump());
            StringAssert.Contains("나와야 한다", _captured[0].Message);
        }

        [Test]
        public void Enable_켠_카테고리가_추가로_통과한다()
        {
            GameLog.Only(LogCategory.Monsters);
            GameLog.Enable(LogCategory.Harpoon);

            GameLog.Info(LogCategory.Harpoon, "이제 통과한다");
            GameLog.Info(LogCategory.Monsters, "여전히 통과한다");
            GameLog.Info(LogCategory.Train, "이건 막힌다");

            Assert.AreEqual(2, _captured.Count, Dump());
        }

        [Test]
        public void Error_필터가_전부_꺼져도_출력된다()
        {
            GameLog.DisableAll();

            // 일부러 내는 오류 로그라 러너에 미리 알린다 — 알리지 않으면 그 자체로 테스트가 실패한다.
            const string expected = "[Harpoon/GameLogTests] 오류는 항상 남는다";
            LogAssert.Expect(LogType.Error, expected);

            GameLog.Error(LogCategory.Harpoon, "오류는 항상 남는다");

            Assert.AreEqual(1, _captured.Count, Dump());
            Assert.AreEqual(LogType.Error, _captured[0].Type);
            Assert.AreEqual(expected, _captured[0].Message);
        }

        [Test]
        public void InfoLimited_상한까지만_출력하고_횟수를_덧붙인다()
        {
            GameLog.EnableAll();

            for (int i = 0; i < 5; i++)
            {
                GameLog.InfoLimited(LogCategory.Qa, "test.limit", 3, "반복");
            }

            Assert.AreEqual(3, _captured.Count, Dump());
            StringAssert.EndsWith("(#1/3)", _captured[0].Message);
            StringAssert.EndsWith("(#3/3)", _captured[2].Message);
        }

        [Test]
        public void InfoLimited_키가_다르면_상한을_따로_센다()
        {
            GameLog.EnableAll();

            GameLog.InfoLimited(LogCategory.Qa, "test.a", 1, "A");
            GameLog.InfoLimited(LogCategory.Qa, "test.b", 1, "B");
            GameLog.InfoLimited(LogCategory.Qa, "test.a", 1, "A 두 번째 — 막힌다");

            Assert.AreEqual(2, _captured.Count, Dump());
        }

        [Test]
        public void InfoOnce_한_번만_출력한다()
        {
            GameLog.EnableAll();

            GameLog.InfoOnce(LogCategory.Qa, "test.once", "처음");
            GameLog.InfoOnce(LogCategory.Qa, "test.once", "두 번째 — 막힌다");

            Assert.AreEqual(1, _captured.Count, Dump());
        }

        [Test]
        public void ResetLimit_이후_다시_출력한다()
        {
            GameLog.EnableAll();
            GameLog.InfoOnce(LogCategory.Qa, "test.once", "처음");
            GameLog.InfoOnce(LogCategory.Qa, "test.once", "두 번째 — 막힌다");

            GameLog.ResetLimit("test.once");
            GameLog.InfoOnce(LogCategory.Qa, "test.once", "리셋 후 다시");

            Assert.AreEqual(2, _captured.Count, Dump());
            StringAssert.Contains("리셋 후 다시", _captured[1].Message);
        }

        [Test]
        public void 꺼진_카테고리는_상한을_소모하지_않는다()
        {
            // 카테고리를 껐다가 켰을 때, 꺼져 있던 동안의 호출이 상한을 갉아먹으면
            // 정작 보고 싶을 때 로그가 나오지 않는다.
            GameLog.DisableAll();
            for (int i = 0; i < 5; i++)
            {
                GameLog.InfoLimited(LogCategory.Qa, "test.gate", 2, "꺼진 동안");
            }

            GameLog.EnableAll();
            GameLog.InfoLimited(LogCategory.Qa, "test.gate", 2, "켠 뒤");

            Assert.AreEqual(1, _captured.Count, Dump());
            StringAssert.Contains("켠 뒤", _captured[0].Message);
        }
    }
}
