using Game.Gameplay.Region;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 날씨 발생 추첨 검증 (기획서 §7.4, M7 3차 — weather-events §11의 EditMode 공백을 메운다).
    /// 북극에서 날씨가 2종이 되며 <b>추첨이 처음으로 의미를 갖는다</b>.
    /// </summary>
    public sealed class WeatherRollTests
    {
        [Test]
        public void 지역_진입_첫날은_날씨가_걸리지_않는다()
        {
            // 지형조차 아직 도착하지 않은 시점이라 전환 연출과 폭풍이 겹쳐 읽힌다 (2026-08-03 검증 피드백).
            Assert.That(WeatherRoll.Evaluate(1, 2, 1f, 0f, 0f), Is.EqualTo(WeatherRoll.Clear));
            Assert.That(WeatherRoll.Evaluate(0, 2, 1f, 0f, 0f), Is.EqualTo(WeatherRoll.Clear));
        }

        [Test]
        public void 확률_0인_지역에서는_발생하지_않는다()
        {
            // 숲·대초원 — 날씨 축이 없는 지역은 난수와 무관하게 항상 맑다.
            Assert.That(WeatherRoll.Evaluate(3, 2, 0f, 0f, 0f), Is.EqualTo(WeatherRoll.Clear));
        }

        [Test]
        public void 등재된_날씨가_없으면_발생하지_않는다()
        {
            Assert.That(WeatherRoll.Evaluate(3, 0, 1f, 0f, 0f), Is.EqualTo(WeatherRoll.Clear));
        }

        [Test]
        public void 확률을_넘는_난수는_맑음이다()
        {
            // 북극 0.7 — 0.7 이하면 발생, 초과면 맑음 (경계 포함).
            Assert.That(WeatherRoll.Evaluate(2, 2, 0.7f, 0.7f, 0f), Is.EqualTo(0), "경계는 발생 쪽");
            Assert.That(WeatherRoll.Evaluate(2, 2, 0.7f, 0.71f, 0f), Is.EqualTo(WeatherRoll.Clear));
        }

        [Test]
        public void 날씨_2종은_균등하게_갈린다()
        {
            // 북극 = 폭설·혹한파. 선택 난수의 앞 절반이 0번, 뒤 절반이 1번이 되어
            // 지역 확률 0.7이 종류별 0.35로 나뉜다 (계획 §1 밸런스 표).
            Assert.That(WeatherRoll.Evaluate(2, 2, 1f, 0f, 0f), Is.EqualTo(0));
            Assert.That(WeatherRoll.Evaluate(2, 2, 1f, 0f, 0.49f), Is.EqualTo(0));
            Assert.That(WeatherRoll.Evaluate(2, 2, 1f, 0f, 0.5f), Is.EqualTo(1));
            Assert.That(WeatherRoll.Evaluate(2, 2, 1f, 0f, 0.99f), Is.EqualTo(1));
        }

        [Test]
        public void 선택_난수_1은_배열_밖으로_나가지_않는다()
        {
            // UnityEngine.Random.value는 1을 포함한다 — 인덱스가 배열 밖으로 나가면 안 된다.
            Assert.That(WeatherRoll.Evaluate(2, 2, 1f, 0f, 1f), Is.EqualTo(1));
            Assert.That(WeatherRoll.Evaluate(2, 3, 1f, 0f, 1f), Is.EqualTo(2));
            Assert.That(WeatherRoll.Evaluate(2, 1, 1f, 0f, 1f), Is.EqualTo(0));
        }

        [Test]
        public void 날씨_1종_지역은_항상_그_하나다()
        {
            // 사막(모래폭풍 1종) 무회귀 — 선택 난수가 무엇이든 0번이다.
            for (int i = 0; i <= 10; i++)
            {
                Assert.That(WeatherRoll.Evaluate(3, 1, 1f, 0f, i / 10f), Is.EqualTo(0));
            }
        }
    }
}
