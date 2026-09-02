using Game.Utilities.Performance;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>벤치·스모크 실행 인자 해석 검증 (성능 프로파일링 자동화 계획 1차 1.2).</summary>
    public sealed class PerfRunArgsResolverTests
    {
        [Test]
        public void 인자가_없으면_자동_주행이_아니다()
        {
            Assert.That(PerfRunArgsResolver.Resolve(null).IsAutomatedRun, Is.False);
            Assert.That(PerfRunArgsResolver.Resolve(new string[0]).IsAutomatedRun, Is.False);
            Assert.That(PerfRunArgsResolver.Resolve(new[] { "game.exe", "-batchmode" }).IsAutomatedRun, Is.False);
        }

        [Test]
        public void perfrun_은_시나리오_이름을_가져온다()
        {
            PerfRunArgs args = PerfRunArgsResolver.Resolve(new[] { "game.exe", "-perfrun", "forest-day-60s" });

            Assert.That(args.Mode, Is.EqualTo(PerfRunMode.Benchmark));
            Assert.That(args.Scenario, Is.EqualTo("forest-day-60s"));
        }

        [Test]
        public void perfout_은_결과_경로를_가져온다()
        {
            PerfRunArgs args = PerfRunArgsResolver.Resolve(
                new[] { "-perfrun", "forest-day-60s", "-perfout", "Perf/runs/a.json" });

            Assert.That(args.OutputPath, Is.EqualTo("Perf/runs/a.json"));
        }

        [Test]
        public void 시나리오_이름이_없는_perfrun_은_자동_주행이_아니다()
        {
            // 이름 없이 벤치를 시작하면 60초를 달리고도 어느 기준선과 비교할지 알 수 없다.
            Assert.That(PerfRunArgsResolver.Resolve(new[] { "game.exe", "-perfrun" }).IsAutomatedRun, Is.False);
            Assert.That(PerfRunArgsResolver.Resolve(new[] { "-perfrun", "-perfout", "a.json" }).IsAutomatedRun,
                Is.False);
        }

        [Test]
        public void smoke_는_값이_없으면_기본_길이를_쓴다()
        {
            PerfRunArgs args = PerfRunArgsResolver.Resolve(new[] { "game.exe", "-smoke" });

            Assert.That(args.Mode, Is.EqualTo(PerfRunMode.Smoke));
            Assert.That(args.DurationSeconds, Is.EqualTo(PerfRunArgsResolver.DefaultSmokeSeconds));
        }

        [Test]
        public void smoke_는_초를_받는다()
        {
            Assert.That(PerfRunArgsResolver.Resolve(new[] { "-smoke", "45" }).DurationSeconds, Is.EqualTo(45f));
        }

        [Test]
        public void smoke_길이는_허용_범위로_잘린다()
        {
            Assert.That(PerfRunArgsResolver.Resolve(new[] { "-smoke", "0" }).DurationSeconds,
                Is.EqualTo(PerfRunArgsResolver.MinSmokeSeconds));
            Assert.That(PerfRunArgsResolver.Resolve(new[] { "-smoke", "99999" }).DurationSeconds,
                Is.EqualTo(PerfRunArgsResolver.MaxSmokeSeconds));
        }

        [Test]
        public void smoke_뒤에_다른_옵션이_오면_값으로_읽지_않는다()
        {
            // "-smoke -perfout x" 에서 -perfout 을 초로 해석하면 기본 길이가 조용히 사라진다.
            PerfRunArgs args = PerfRunArgsResolver.Resolve(new[] { "-smoke", "-perfout", "a.json" });

            Assert.That(args.Mode, Is.EqualTo(PerfRunMode.Smoke));
            Assert.That(args.DurationSeconds, Is.EqualTo(PerfRunArgsResolver.DefaultSmokeSeconds));
        }

        [Test]
        public void 둘_다_있으면_벤치가_이긴다()
        {
            // 재라고 시킨 실행을 스모크로 강등시키면 결과 파일이 조용히 사라진다.
            PerfRunArgs args = PerfRunArgsResolver.Resolve(new[] { "-smoke", "-perfrun", "forest-day-60s" });

            Assert.That(args.Mode, Is.EqualTo(PerfRunMode.Benchmark));
            Assert.That(args.Scenario, Is.EqualTo("forest-day-60s"));
        }

        [Test]
        public void 대소문자를_가리지_않는다()
        {
            Assert.That(PerfRunArgsResolver.Resolve(new[] { "-SMOKE" }).Mode, Is.EqualTo(PerfRunMode.Smoke));
            Assert.That(PerfRunArgsResolver.Resolve(new[] { "-PerfRun", "x" }).Mode,
                Is.EqualTo(PerfRunMode.Benchmark));
        }
    }
}
