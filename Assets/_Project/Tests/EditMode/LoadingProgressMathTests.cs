using System;
using Game.Systems.Loading;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 단계 가중 진행률 검증 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §4 · §10.
    ///
    /// <para>계획이 <b>보장하겠다고 적은 것만</b> 고정한다(§4.3). 진행률의 정확성은 보장 대상이
    /// 아니므로 "62 %가 맞는가"는 여기서 묻지 않는다. 대신 다음 넷이 깨지면 화면이 고장으로
    /// 읽힌다 — <b>구멍 없는 연결</b>, <b>단조 증가</b>, <b>기다리는 동안 100 %가 뜨지 않음</b>,
    /// <b>0~1 밖으로 나가지 않음</b>.</para>
    /// </summary>
    public sealed class LoadingProgressMathTests
    {
        private const float Tolerance = 1e-4f;

        /// <summary>몫이 있는 단계 — <see cref="LoadingStage.Idle"/>과 <see cref="LoadingStage.Done"/>은 0이다.</summary>
        private static readonly LoadingStage[] Weighted =
        {
            LoadingStage.Prepare,
            LoadingStage.WaitPrepare,
            LoadingStage.LoadScene,
            LoadingStage.Settle,
            LoadingStage.WaitSettle,
            LoadingStage.Depart,
        };

        [Test]
        public void 가중치_합은_정확히_1이다()
        {
            float sum = 0f;
            foreach (LoadingStage stage in Weighted)
            {
                sum += LoadingProgressMath.Weight(stage);
            }

            Assert.AreEqual(1f, sum, Tolerance);
        }

        [Test]
        public void 계획_4_1의_가중치를_그대로_옮긴다()
        {
            // ① 폰트 0.05 + 지형 0.30
            Assert.AreEqual(0.35f, LoadingProgressMath.Weight(LoadingStage.Prepare), Tolerance);
            Assert.AreEqual(0.05f, LoadingProgressMath.Weight(LoadingStage.WaitPrepare), Tolerance);
            Assert.AreEqual(0.25f, LoadingProgressMath.Weight(LoadingStage.LoadScene), Tolerance);

            // ③ 건축물 0.20 + UI 0.10
            Assert.AreEqual(0.30f, LoadingProgressMath.Weight(LoadingStage.Settle), Tolerance);

            // ③ 전원 대기 + ④ 출발 = 0.05를 둘로 나눈 값
            Assert.AreEqual(
                0.05f,
                LoadingProgressMath.Weight(LoadingStage.WaitSettle) + LoadingProgressMath.Weight(LoadingStage.Depart),
                Tolerance);
        }

        [Test]
        public void 단계_경계에_구멍이_없다()
        {
            foreach (LoadingStage stage in Weighted)
            {
                LoadingStage next = stage + 1;
                Assert.AreEqual(
                    LoadingProgressMath.Combine(stage, 1f),
                    LoadingProgressMath.Combine(next, 0f),
                    Tolerance,
                    $"{stage}의 끝과 {next}의 시작이 어긋난다");
            }
        }

        [Test]
        public void 진행률은_단계_순서대로만_커진다()
        {
            float previous = -1f;
            foreach (LoadingStage stage in Weighted)
            {
                float start = LoadingProgressMath.Combine(stage, 0f);
                Assert.Greater(start, previous, $"{stage}의 시작이 앞 단계보다 앞서 있다");
                previous = start;
            }
        }

        [Test]
        public void 시작과_끝은_0과_1이다()
        {
            Assert.AreEqual(0f, LoadingProgressMath.Combine(LoadingStage.Idle, 1f), Tolerance);
            Assert.AreEqual(0f, LoadingProgressMath.Combine(LoadingStage.Prepare, 0f), Tolerance);
            Assert.AreEqual(1f, LoadingProgressMath.Combine(LoadingStage.Depart, 1f), Tolerance);
            Assert.AreEqual(1f, LoadingProgressMath.Combine(LoadingStage.Done, 0f), Tolerance);
        }

        [Test]
        public void 단계_진행도가_범위를_벗어나도_접어_넣는다()
        {
            float start = LoadingProgressMath.Combine(LoadingStage.LoadScene, 0f);
            float end = LoadingProgressMath.Combine(LoadingStage.LoadScene, 1f);

            Assert.AreEqual(start, LoadingProgressMath.Combine(LoadingStage.LoadScene, -5f), Tolerance);
            Assert.AreEqual(end, LoadingProgressMath.Combine(LoadingStage.LoadScene, 5f), Tolerance);
        }

        [Test]
        public void 전원_대기는_100퍼센트에서_멈추지_않는다()
        {
            // §4.3 — 100 %가 뜬 채 몇 초 서 있는 화면이 제일 나쁘다. 두 대기 단계 모두 상한이 1 미만이어야 한다.
            Assert.Less(LoadingProgressMath.Combine(LoadingStage.WaitPrepare, 1f), 1f);
            Assert.Less(LoadingProgressMath.Combine(LoadingStage.WaitSettle, 1f), 1f);
        }

        [Test]
        public void 대기_단계는_자기_상한을_넘지_않는다()
        {
            Assert.AreEqual(
                LoadingProgressMath.Combine(LoadingStage.LoadScene, 0f),
                LoadingProgressMath.Combine(LoadingStage.WaitPrepare, 2f),
                Tolerance);

            Assert.AreEqual(
                LoadingProgressMath.Combine(LoadingStage.Depart, 0f),
                LoadingProgressMath.Combine(LoadingStage.WaitSettle, 2f),
                Tolerance);
        }

        [Test]
        public void 표시값은_내려가지_않는다()
        {
            Assert.AreEqual(0.6f, LoadingProgressMath.Monotonic(0.6f, 0.2f), Tolerance);
            Assert.AreEqual(0.7f, LoadingProgressMath.Monotonic(0.6f, 0.7f), Tolerance);
            Assert.AreEqual(0.6f, LoadingProgressMath.Monotonic(0.6f, 0.6f), Tolerance);
        }

        [Test]
        public void 표시값은_0과_1_사이에_머문다()
        {
            Assert.AreEqual(0f, LoadingProgressMath.Monotonic(0f, -3f), Tolerance);
            Assert.AreEqual(1f, LoadingProgressMath.Monotonic(0.5f, 3f), Tolerance);
        }

        [Test]
        public void 모든_단계가_0과_1_사이를_돌려준다()
        {
            foreach (LoadingStage stage in (LoadingStage[])Enum.GetValues(typeof(LoadingStage)))
            {
                foreach (float t in new[] { -1f, 0f, 0.5f, 1f, 2f })
                {
                    float value = LoadingProgressMath.Combine(stage, t);
                    Assert.GreaterOrEqual(value, 0f, $"{stage} @ {t}");
                    Assert.LessOrEqual(value, 1f, $"{stage} @ {t}");
                }
            }
        }
    }
}
