using Game.Gameplay.Region;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 사막 세그먼트 팔레트 <b>에셋 배선</b>의 건전성 (사막 지역 구현 계획 §6.6).
    /// 순수 로직이 아니라 데이터를 본다 — 이 배선이 끊기면 <b>아무도 눈치채지 못한 채</b>
    /// 사막 지형이 통째로 사라지기 때문이다.
    ///
    /// <para><b>왜 조용히 사라지는가.</b> <see cref="TerrainSegmentPalette"/>의 캐시 재구성은
    /// *"프리팹이 비면 가중치 0 — 추첨에서 조용히 빠진다"* 로 짜여 있고(작업 중인 슬롯을 비워
    /// 둘 수 있게 한 의도적 설계), <see cref="SegmentPickLogic.WeightedPick"/>은 합이 0이면
    /// −1을 돌려준다. 열 칸이 모두 끊기면 <b>오류 로그 한 줄 없이</b> 사막이 빈 채로 흐른다.</para>
    ///
    /// <para>이 위험은 실제로 한 번 <b>오탐으로</b> 제기됐다 — 팔레트가 적어 둔 <c>fileID</c>가
    /// 타일 프리팹 파일 안에 문자열로 존재하지 않는다는 이유였다. 그런데 <b>숲·바다 팔레트도
    /// 똑같다</b>: 세 팔레트가 가리키는 타일이 전부 <c>TerrainTile_Rail.prefab</c>의
    /// <b>변종(Variant)</b>이라, 물려받은 루트는 변종 YAML에 앵커로 적히지 않고 임포트 때
    /// 유도된다. 그래서 <b>파일을 grep 해서는 판정할 수 없고</b>, 실제로 해석되는지를
    /// 이 테스트가 대신 본다.</para>
    /// </summary>
    public sealed class DesertSegmentPaletteAssetTests
    {
        private const string PalettePath = "Assets/_Project/Data/TerrainSegmentPalette_Desert.asset";
        private const string RegionPath = "Assets/_Project/Data/Region_Desert.asset";

        /// <summary>가이드 §4.6 — 지역당 10종(기본 5 / 특징 3 / 이벤트 2).</summary>
        private const int ExpectedSegmentCount = 10;

        /// <summary>사막 4일이 소비하는 타일 수 (계획 §2.1 — 9,360 m ÷ 40).</summary>
        private const int DesertTileCount = 234;

        private static TerrainSegmentPalette LoadPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TerrainSegmentPalette>(PalettePath);
            Assert.IsNotNull(palette, $"팔레트 에셋이 없다: {PalettePath}");
            return palette;
        }

        [Test]
        public void 사막_팔레트는_10종이다()
        {
            Assert.AreEqual(ExpectedSegmentCount, LoadPalette().Count);
        }

        [Test]
        public void 열_칸의_프리팹_참조가_전부_살아_있다()
        {
            // 한 칸만 끊겨도 그 세그먼트는 가중치 0으로 죽고, 화면에서는 "덜 나오는" 것으로만 보인다.
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
            // 기본형·특징형까지 막으면 추첨 후보가 좁아져 반복이 오히려 늘어난다.
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
        public void 사막_전_구간에서_빈_타일이_나오지_않는다()
        {
            // 이 테스트가 지키는 것 — "오류 없이 사막이 통째로 비는" 상태.
            TerrainSegmentPalette palette = LoadPalette();
            float[] weights = palette.GetWeights();
            bool[] noRepeat = palette.GetNoRepeatFlags();

            for (int tileIndex = 0; tileIndex < DesertTileCount; tileIndex++)
            {
                int pick = SegmentPickLogic.PickForTile(tileIndex, weights, noRepeat);
                Assert.GreaterOrEqual(pick, 0, $"타일 {tileIndex}에서 추첨이 무효(-1)다");
                Assert.IsNotNull(palette.GetPrefab(pick), $"타일 {tileIndex}가 빈 프리팹을 골랐다");
            }
        }

        [Test]
        public void 지역_데이터가_이_팔레트를_가리킨다()
        {
            // 팔레트가 멀쩡해도 지역이 안 가리키면 단일 타일 폴백으로 조용히 떨어진다
            // (ResolveTilePrefab 은 팔레트 → 단일 프리팹 → 씬 기본 3단 폴백이다).
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            Assert.IsNotNull(region, $"지역 에셋이 없다: {RegionPath}");
            Assert.IsNotNull(region.SegmentPalette, "Region_Desert 가 세그먼트 팔레트를 가리키지 않는다");
            Assert.AreEqual(ExpectedSegmentCount, region.SegmentPalette.Count);
        }

        [Test]
        public void 지역_하늘과_안개가_사막_값으로_배선돼_있다()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            Assert.IsNotNull(region.SkyboxMaterial, "Region_Desert 에 하늘이 없다");
            Assert.IsTrue(region.OverridesFog, "Region_Desert 가 fog 를 소유하지 않는다");

            // 0.0062(씬 값)이면 500 m 에서 투과율 0.007 % — 유적군이 화면에 남지 않는다.
            Assert.AreEqual(0.0015f, region.DayFogDensity, 1e-6f);
            Assert.AreEqual(0.0015f, region.NightFogDensity, 1e-6f);
            Assert.AreNotEqual(region.DayFogColor, region.NightFogColor, "낮과 밤 안개 색이 같다");
        }
    }
}
