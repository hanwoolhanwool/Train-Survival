using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 부활 대기 Day 비례 계산 검증 (M6 3차 결정 ① — 기획서 §9.1 n값 확정).
    /// 기본값(5 + Day당 1, 상한 20)은 에셋과 같은 직렬화 기본값을 쓴다.
    /// </summary>
    public sealed class PlayerHealthSettingsTests
    {
        private PlayerHealthSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<PlayerHealthSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Day_1은_기본_대기_시간이다()
        {
            Assert.That(_settings.GetRespawnDelaySeconds(1), Is.EqualTo(5f));
        }

        [Test]
        public void Day가_오를수록_대기_시간이_비례_증가한다()
        {
            Assert.That(_settings.GetRespawnDelaySeconds(2), Is.EqualTo(6f));
            Assert.That(_settings.GetRespawnDelaySeconds(10), Is.EqualTo(14f));
        }

        [Test]
        public void 상한에_도달하면_더_오르지_않는다()
        {
            // 기본값 기준 상한 도달 = Day 16 (5 + 15 = 20).
            Assert.That(_settings.GetRespawnDelaySeconds(16), Is.EqualTo(20f));
            Assert.That(_settings.GetRespawnDelaySeconds(17), Is.EqualTo(20f));
            Assert.That(_settings.GetRespawnDelaySeconds(999), Is.EqualTo(20f));
        }

        [Test]
        public void Day_1_미만은_기본_대기_시간으로_수렴한다()
        {
            // Day 서비스 부재 폴백(1)이나 비정상 입력에서도 기본값 아래로 내려가지 않는다.
            Assert.That(_settings.GetRespawnDelaySeconds(0), Is.EqualTo(5f));
            Assert.That(_settings.GetRespawnDelaySeconds(-3), Is.EqualTo(5f));
        }
    }
}
