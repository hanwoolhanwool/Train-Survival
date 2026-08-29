using Game.Gameplay.Region;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 날씨 입자 연출의 켜짐 판정 (사막 지역 구현 계획 4차 §8).
    /// 지역마다 입자가 달라야 하므로 <b>담당 날씨가 아닌 날씨에는 켜지지 않는 것</b>이 규격이다 —
    /// 북극 블리자드에 모래가 날리면 안 된다.
    /// </summary>
    public sealed class WeatherParticleViewTests
    {
        private WeatherDefinition _sandstorm;
        private WeatherDefinition _blizzard;

        [SetUp]
        public void SetUp()
        {
            _sandstorm = ScriptableObject.CreateInstance<WeatherDefinition>();
            _blizzard = ScriptableObject.CreateInstance<WeatherDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sandstorm);
            Object.DestroyImmediate(_blizzard);
        }

        [Test]
        public void 맑으면_켜지지_않는다()
        {
            Assert.IsFalse(WeatherParticleView.ShouldPlay(null, _sandstorm));
            Assert.IsFalse(WeatherParticleView.ShouldPlay(null, null));
        }

        [Test]
        public void 담당_날씨면_켜진다()
        {
            Assert.IsTrue(WeatherParticleView.ShouldPlay(_sandstorm, _sandstorm));
        }

        [Test]
        public void 다른_날씨에는_켜지지_않는다()
        {
            // 북극 블리자드에 사막 모래가 날리면 지역 정체성이 무너진다.
            Assert.IsFalse(WeatherParticleView.ShouldPlay(_blizzard, _sandstorm));
        }

        [Test]
        public void 담당을_비우면_어떤_날씨에나_켜진다()
        {
            Assert.IsTrue(WeatherParticleView.ShouldPlay(_sandstorm, null));
            Assert.IsTrue(WeatherParticleView.ShouldPlay(_blizzard, null));
        }
    }
}
