using Game.Gameplay.Region;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 북극 세그먼트 팔레트 <b>에셋 배선</b>의 건전성 (북극 지역 구현 계획 §7).
    /// 사막이 세운 계기(<see cref="DesertSegmentPaletteAssetTests"/>)와 같은 이유로 있다 —
    /// 팔레트가 끊기면 <b>오류·예외·로그 한 줄 없이</b> 지역이 통째로 빈다.
    ///
    /// <para><b>북극에는 계기가 하나 더 필요하다.</b> 이 지역만 <b>2단 추첨</b>(구간 군 → 세그먼트)을
    /// 쓰므로, 군 배정이나 편성이 어긋나도 지형은 나온다 — 다만 <b>얼음 우세 ↔ 바다 우세의 교차가
    /// 사라진다.</b> 그것은 화면에서 "조금 밋밋하다"로만 보이지 결함으로 보이지 않는다.</para>
    /// </summary>
    public sealed class ArcticSegmentPaletteAssetTests
    {
        private const string PalettePath = "Assets/_Project/Data/TerrainSegmentPalette_Arctic.asset";
        private const string RegionPath = "Assets/_Project/Data/Region_Arctic.asset";

        /// <summary>가이드 §4.6 — 지역당 10종(기본 5 / 특징 3 / 이벤트 2).</summary>
        private const int ExpectedSegmentCount = 10;

        /// <summary>북극 3일이 소비하는 타일 수 (계획 §2.1 — 7,020 m ÷ 40).</summary>
        private const int ArcticTileCount = 176;

        /// <summary>한 바퀴 = 얼음 6 + 전이 1 + 바다 5 + 전이 1 (계획 §5.3 기준안 · 520 m · 87초).</summary>
        private const int CycleLength = 13;

        private const int GroupIce = 0;
        private const int GroupTransition = 1;
        private const int GroupSea = 2;

        private static TerrainSegmentPalette LoadPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TerrainSegmentPalette>(PalettePath);
            Assert.IsNotNull(palette, $"팔레트 에셋이 없다: {PalettePath}");
            return palette;
        }

        [Test]
        public void 북극_팔레트는_10종이다()
        {
            Assert.AreEqual(ExpectedSegmentCount, LoadPalette().Count);
        }

        [Test]
        public void 열_칸의_프리팹_참조가_전부_살아_있다()
        {
            TerrainSegmentPalette palette = LoadPalette();
            for (int i = 0; i < palette.Count; i++)
            {
                Assert.IsNotNull(palette.GetPrefab(i), $"팔레트 {i}번 칸의 프리팹 참조가 끊겼다");
            }
        }

        [Test]
        public void 가중치_합이_1이다()
        {
            float[] weights = LoadPalette().GetWeights();
            Assert.IsNotNull(weights);

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
            }

            Assert.AreEqual(1f, total, 0.0005f);
        }

        [Test]
        public void 이벤트형_둘만_인접_반복을_막는다()
        {
            bool[] flags = LoadPalette().GetNoRepeatFlags();
            int blocked = 0;
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i])
                {
                    blocked++;
                }
            }

            Assert.AreEqual(2, blocked);
        }

        [Test]
        public void 군_배정은_얼음_5_전이_2_바다_3이다()
        {
            int[] groups = LoadPalette().GetEntryGroups();
            Assert.IsNotNull(groups, "군 배정이 없다 — 2단 추첨이 통째로 폴백된다");

            int ice = 0, transition = 0, sea = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == GroupIce) ice++;
                else if (groups[i] == GroupTransition) transition++;
                else if (groups[i] == GroupSea) sea++;
                else Assert.Fail($"{i}번 칸의 군 번호 {groups[i]}는 편성에 없다");
            }

            Assert.AreEqual(5, ice, "얼음 군 (A~E)");
            Assert.AreEqual(2, transition, "전이 군 (F·G)");
            Assert.AreEqual(3, sea, "바다 군 (H·I·J)");
        }

        [Test]
        public void 구간_편성은_13장_한_바퀴다()
        {
            int[] schedule = LoadPalette().GroupSchedule;
            Assert.IsNotNull(schedule, "구간 편성이 비었다 — 교차가 사라지고 타일마다 독립 추첨이 된다");
            Assert.AreEqual(CycleLength, schedule.Length);

            int ice = 0, transition = 0, sea = 0;
            for (int i = 0; i < schedule.Length; i++)
            {
                if (schedule[i] == GroupIce) ice++;
                else if (schedule[i] == GroupTransition) transition++;
                else sea++;
            }

            Assert.AreEqual(6, ice, "얼음 우세 6장");
            Assert.AreEqual(2, transition, "전이 2장 — 얼음→바다와 바다→얼음 양쪽");
            Assert.AreEqual(5, sea, "바다 우세 5장");
        }

        [Test]
        public void 전이는_얼음과_바다_사이에만_온다()
        {
            // 전이가 같은 군 안에 끼면 "판이 갈라지는데 그 앞뒤가 같은 지형"이 된다.
            int[] schedule = LoadPalette().GroupSchedule;
            for (int i = 0; i < schedule.Length; i++)
            {
                if (schedule[i] != GroupTransition)
                {
                    continue;
                }

                int before = schedule[(i - 1 + schedule.Length) % schedule.Length];
                int after = schedule[(i + 1) % schedule.Length];
                Assert.AreNotEqual(before, after, $"편성 {i}번 전이의 앞뒤가 같은 군이다");
            }
        }

        [Test]
        public void 북극_전_구간에서_빈_타일이_나오지_않는다()
        {
            // 이 테스트가 지키는 것 — "오류 없이 북극이 통째로 비는" 상태.
            TerrainSegmentPalette palette = LoadPalette();
            float[] weights = palette.GetWeights();
            bool[] noRepeat = palette.GetNoRepeatFlags();
            int[] groups = palette.GetEntryGroups();
            int[] schedule = palette.GroupSchedule;
            var scratch = new float[weights.Length];

            for (int tileIndex = 0; tileIndex < ArcticTileCount; tileIndex++)
            {
                int pick = SegmentPickLogic.PickForTile(tileIndex, weights, noRepeat, groups, schedule, scratch);
                Assert.GreaterOrEqual(pick, 0, $"타일 {tileIndex}에서 추첨이 무효(-1)다");
                Assert.IsNotNull(palette.GetPrefab(pick), $"타일 {tileIndex}가 빈 프리팹을 골랐다");
                Assert.AreEqual(SegmentPickLogic.GroupAtTile(tileIndex, schedule), groups[pick],
                    $"타일 {tileIndex}이 다른 군의 세그먼트를 골랐다");
            }
        }

        [Test]
        public void 북극_3일에_교차가_열세_바퀴_돈다()
        {
            // 176장 ÷ 13장 = 13.5바퀴. 밤 150초(1.7바퀴) 하나가 두 지형을 다 거친다(§5.3).
            Assert.AreEqual(13, ArcticTileCount / CycleLength);
        }

        [Test]
        public void 지역_데이터가_이_팔레트를_가리킨다()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            Assert.IsNotNull(region, $"지역 에셋이 없다: {RegionPath}");
            Assert.IsNotNull(region.SegmentPalette, "Region_Arctic 이 세그먼트 팔레트를 가리키지 않는다");
            Assert.AreEqual(ExpectedSegmentCount, region.SegmentPalette.Count);
        }

        [Test]
        public void 지역_하늘과_안개가_북극_값으로_배선돼_있다()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            Assert.IsNotNull(region.SkyboxMaterial, "Region_Arctic 에 하늘이 없다");
            Assert.IsTrue(region.OverridesFog, "Region_Arctic 이 fog 를 소유하지 않는다");

            // 0.0062(씬 값)이면 300 m 에서 3 % — 빙하 산맥이 화면에 남지 않는다.
            Assert.AreEqual(0.0017f, region.DayFogDensity, 1e-6f);
            Assert.AreEqual(0.0017f, region.NightFogDensity, 1e-6f);
            Assert.AreNotEqual(region.DayFogColor, region.NightFogColor, "낮과 밤 안개 색이 같다");
        }

        [Test]
        public void 앵커_구성이_자원_수요와_맞는다()
        {
            // 북극 자원은 얼음 0.45(→Water) · 희귀 금속 0.30(→Rock) · 유적 부품 0.15 + 고철 0.10(→Wreck)이다.
            // 가이드 §7.5 의 원안(Rock 40 · Wreck 35 · Ground 25)은 물길이 생기기 전의 값이라
            // **수요 45 % 를 받을 Water 앵커가 하나도 없었다**.
            int water = 0, rock = 0, wreck = 0, ground = 0;
            string[] letters = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };
            for (int i = 0; i < letters.Length; i++)
            {
                var tile = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/_Project/Prefabs/TerrainTile_Arctic_{letters[i]}.prefab");
                Assert.IsNotNull(tile, $"세그먼트 {letters[i]} 프리팹이 없다");

                ResourceAnchor[] anchors = tile.GetComponentsInChildren<ResourceAnchor>(true);
                Assert.AreEqual(6, anchors.Length, $"{letters[i]} 앵커 수 — 기준은 타일당 5~7개다");

                for (int k = 0; k < anchors.Length; k++)
                {
                    switch (anchors[k].Kind)
                    {
                        case ResourceAnchorKind.Water: water++; break;
                        case ResourceAnchorKind.Rock: rock++; break;
                        case ResourceAnchorKind.Wreck: wreck++; break;
                        default: ground++; break;
                    }
                }
            }

            Assert.AreEqual(60, water + rock + wreck + ground);
            Assert.AreEqual(27, water, "Water 45 % — 얼음");
            Assert.AreEqual(18, rock, "Rock 30 % — 희귀 금속");
            Assert.AreEqual(15, wreck, "Wreck 25 % — 유적 부품 + 고철");
            Assert.AreEqual(0, ground, "북극에는 Ground 앵커가 없다 — 수요가 0이다");
        }
    }
}
