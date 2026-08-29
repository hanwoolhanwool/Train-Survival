using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 세그먼트 추첨 — 미결 ① 확정(인덱스 시드·전 피어 동일)이 실제로 성립하는지 검증한다.
    /// 결정론이 깨지면 피어마다 콜라이더가 갈려 몬스터가 없는 벽을 도는 것처럼 보인다.
    /// </summary>
    public sealed class SegmentPickLogicTests
    {
        private static readonly float[] Even = { 1f, 1f, 1f, 1f };

        [Test]
        public void 같은_타일_인덱스는_항상_같은_세그먼트를_준다()
        {
            for (int i = -5; i < 50; i++)
            {
                int first = SegmentPickLogic.Pick(i, Even, -1, null);
                int second = SegmentPickLogic.Pick(i, Even, -1, null);
                Assert.AreEqual(first, second, "인덱스 " + i + " 의 추첨이 흔들렸다");
            }
        }

        [Test]
        public void 인접_인덱스가_같은_값으로_뭉치지_않는다()
        {
            // 해시가 인덱스에 선형이면 연속 구간이 같은 값으로 몰린다 — 반복 인지의 최악 형태다.
            int changes = 0;
            int previous = SegmentPickLogic.Pick(0, Even, -1, null);
            for (int i = 1; i < 40; i++)
            {
                int picked = SegmentPickLogic.Pick(i, Even, -1, null);
                if (picked != previous)
                {
                    changes++;
                }

                previous = picked;
            }

            Assert.Greater(changes, 15, "40장 중 전환이 너무 적다 — 해시 분포가 뭉쳤다");
        }

        [Test]
        public void 가중치가_0인_후보는_뽑히지_않는다()
        {
            var weights = new float[] { 0f, 1f, 0f };
            for (int i = 0; i < 30; i++)
            {
                Assert.AreEqual(1, SegmentPickLogic.Pick(i, weights, -1, null));
            }
        }

        [Test]
        public void 인접_반복_금지_후보는_연달아_나오지_않는다()
        {
            var weights = new float[] { 1f, 1f };
            var noRepeat = new bool[] { true, true };
            for (int i = 0; i < 40; i++)
            {
                int previous = SegmentPickLogic.Pick(i - 1, weights, -1, noRepeat);
                int picked = SegmentPickLogic.Pick(i, weights, previous, noRepeat);
                Assert.AreNotEqual(previous, picked, "인덱스 " + i + " 에서 금지 후보가 연달아 나왔다");
            }
        }

        [Test]
        public void 금지_때문에_후보가_없어지면_제외를_포기한다()
        {
            // 후보가 하나뿐인데 그것이 인접 금지면 스폰이 멈추는 편이 더 나쁘다 — 같은 것을 다시 준다.
            var weights = new float[] { 1f };
            var noRepeat = new bool[] { true };
            Assert.AreEqual(0, SegmentPickLogic.Pick(7, weights, 0, noRepeat));
        }

        [Test]
        public void 빈_팔레트는_무효를_돌려준다()
        {
            Assert.AreEqual(-1, SegmentPickLogic.Pick(3, null, -1, null));
            Assert.AreEqual(-1, SegmentPickLogic.Pick(3, new float[0], -1, null));
            Assert.AreEqual(-1, SegmentPickLogic.Pick(3, new float[] { 0f, 0f }, -1, null));
        }

        [Test]
        public void 해시는_0과_1_사이에_있다()
        {
            for (int i = -100; i < 100; i++)
            {
                float h = SegmentPickLogic.Hash01(i, 1);
                Assert.GreaterOrEqual(h, 0f);
                Assert.Less(h, 1f);
            }
        }

        // ── 2단 추첨: 구간 군 → 세그먼트 (북극 계획 §5.3) ─────────────────────────

        /// <summary>북극 편성 — 얼음 6 + 전이 1 + 바다 5 + 전이 1 = 13장(520 m · 87초).</summary>
        private static readonly int[] ArcticSchedule = { 0, 0, 0, 0, 0, 0, 1, 2, 2, 2, 2, 2, 1 };

        /// <summary>A~E 얼음(0) · F·G 전이(1) · H·I·J 바다(2).</summary>
        private static readonly int[] ArcticGroups = { 0, 0, 0, 0, 0, 1, 1, 2, 2, 2 };

        private static readonly float[] ArcticWeights =
        {
            0.13f, 0.13f, 0.13f, 0.13f, 0.13f, 0.085f, 0.085f, 0.134f, 0.023f, 0.023f,
        };

        [Test]
        public void 구간_군은_바퀴로_되풀이된다()
        {
            Assert.AreEqual(0, SegmentPickLogic.GroupAtTile(0, ArcticSchedule));
            Assert.AreEqual(0, SegmentPickLogic.GroupAtTile(5, ArcticSchedule));
            Assert.AreEqual(1, SegmentPickLogic.GroupAtTile(6, ArcticSchedule), "전이");
            Assert.AreEqual(2, SegmentPickLogic.GroupAtTile(7, ArcticSchedule), "바다");
            Assert.AreEqual(1, SegmentPickLogic.GroupAtTile(12, ArcticSchedule));
            Assert.AreEqual(0, SegmentPickLogic.GroupAtTile(13, ArcticSchedule), "두 번째 바퀴 시작");
        }

        [Test]
        public void 음수_인덱스도_같은_바퀴_위에_있다()
        {
            // 프리웜은 0 앞 인덱스를 들여다본다 — 여기서 음수 나머지가 나오면 배열 밖을 짚는다.
            Assert.AreEqual(SegmentPickLogic.GroupAtTile(12, ArcticSchedule),
                SegmentPickLogic.GroupAtTile(-1, ArcticSchedule));
            Assert.AreEqual(SegmentPickLogic.GroupAtTile(0, ArcticSchedule),
                SegmentPickLogic.GroupAtTile(-13, ArcticSchedule));
        }

        [Test]
        public void 편성이_비면_구간_군이_없다()
        {
            Assert.AreEqual(-1, SegmentPickLogic.GroupAtTile(3, null));
            Assert.AreEqual(-1, SegmentPickLogic.GroupAtTile(3, new int[0]));
        }

        [Test]
        public void 군_마스크는_다른_군을_0으로_지운다()
        {
            var masked = new float[ArcticWeights.Length];
            SegmentPickLogic.ApplyGroupMask(ArcticWeights, ArcticGroups, 2, masked);

            for (int i = 0; i < 7; i++)
            {
                Assert.AreEqual(0f, masked[i], $"{i}번은 바다 군이 아니다");
            }

            Assert.AreEqual(0.134f, masked[7], 1e-5f);
            Assert.AreEqual(0.023f, masked[8], 1e-5f);
            Assert.AreEqual(0.023f, masked[9], 1e-5f);
        }

        [Test]
        public void 군이_비면_마스크가_원래_가중치를_남긴다()
        {
            // 편성에 적힌 군에 세그먼트가 하나도 없으면 그 타일이 통째로 빈다 —
            // 오류 로그 한 줄 없이 지형이 사라지는 실패 방식이라 폴백을 둔다.
            var masked = new float[ArcticWeights.Length];
            SegmentPickLogic.ApplyGroupMask(ArcticWeights, ArcticGroups, 9, masked);

            for (int i = 0; i < ArcticWeights.Length; i++)
            {
                Assert.AreEqual(ArcticWeights[i], masked[i], 1e-5f);
            }
        }

        [Test]
        public void 뽑힌_세그먼트는_그_타일의_군에_속한다()
        {
            var scratch = new float[ArcticWeights.Length];
            for (int tile = -20; tile < 200; tile++)
            {
                int picked = SegmentPickLogic.PickForTile(
                    tile, ArcticWeights, null, ArcticGroups, ArcticSchedule, scratch);

                Assert.GreaterOrEqual(picked, 0, $"타일 {tile} 추첨이 무효다");
                Assert.AreEqual(SegmentPickLogic.GroupAtTile(tile, ArcticSchedule), ArcticGroups[picked],
                    $"타일 {tile}이 다른 군의 세그먼트를 골랐다");
            }
        }

        [Test]
        public void 한_바퀴가_얼음_전이_바다_전이의_리듬을_만든다()
        {
            // 이 리듬이 없으면 얼음 우세와 바다 우세가 6.67초마다 뒤바뀐다 — 교차가 아니라 뒤죽박죽이다.
            var scratch = new float[ArcticWeights.Length];
            int ice = 0, transition = 0, sea = 0;
            for (int tile = 0; tile < 13; tile++)
            {
                int picked = SegmentPickLogic.PickForTile(
                    tile, ArcticWeights, null, ArcticGroups, ArcticSchedule, scratch);

                if (ArcticGroups[picked] == 0) ice++;
                else if (ArcticGroups[picked] == 1) transition++;
                else sea++;
            }

            Assert.AreEqual(6, ice);
            Assert.AreEqual(2, transition);
            Assert.AreEqual(5, sea);
        }

        [Test]
        public void 편성이_없으면_현행_독립_추첨_그대로다()
        {
            // 리스크 6 — 2단 추첨이 다섯 지역 공유 경로를 건드린다. 다른 네 지역은 이 값을 비워 둔다.
            var scratch = new float[Even.Length];
            for (int tile = -10; tile < 300; tile++)
            {
                Assert.AreEqual(
                    SegmentPickLogic.PickForTile(tile, Even, null),
                    SegmentPickLogic.PickForTile(tile, Even, null, null, null, scratch),
                    $"타일 {tile}");

                Assert.AreEqual(
                    SegmentPickLogic.PickForTile(tile, Even, null),
                    SegmentPickLogic.PickForTile(tile, Even, null, new int[Even.Length], new int[0], scratch),
                    $"타일 {tile} (빈 편성)");
            }
        }

        [Test]
        public void 버퍼_길이가_다르면_독립_추첨으로_떨어진다()
        {
            Assert.AreEqual(
                SegmentPickLogic.PickForTile(7, ArcticWeights, null),
                SegmentPickLogic.PickForTile(7, ArcticWeights, null, ArcticGroups, ArcticSchedule, new float[3]));
        }
    }
}
