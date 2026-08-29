using Game.Gameplay.Region;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 대초원 세그먼트 팔레트 <b>에셋 배선</b>의 건전성 (대초원 지역 구현 계획 §6).
    /// 사막·북극이 세운 계기(<see cref="DesertSegmentPaletteAssetTests"/> ·
    /// <see cref="ArcticSegmentPaletteAssetTests"/>)와 같은 이유로 있다 —
    /// 팔레트가 끊기면 <b>오류·예외·로그 한 줄 없이</b> 지역이 통째로 빈다.
    ///
    /// <para><b>대초원에만 필요한 계기가 하나 더 있다.</b> 이 지역만 스탬피드 확률이 0이 아니라
    /// (0.5/day) 무리가 <b>측면 4~9 m</b>를 달린다(계획 §3.4). 클리어 존 검사기는 몬스터 대역을
    /// 4~24 m로 보고 <b>콜라이더만</b> 판정하는데, 지형 소품에는 콜라이더가 없다 —
    /// 즉 <b>검사기를 통과해도 무리가 갇히는 배치가 가능하다.</b> 그래서 이 테스트가
    /// <b>렌더러 기준</b>으로 4~9 m 대역의 연속 장벽을 따로 본다.</para>
    /// </summary>
    public sealed class GrasslandSegmentPaletteAssetTests
    {
        private const string PalettePath = "Assets/_Project/Data/TerrainSegmentPalette_Grassland.asset";
        private const string RegionPath = "Assets/_Project/Data/Region_Grassland.asset";
        private const string TilePrefix = "Assets/_Project/Prefabs/TerrainTile_Grassland_";

        /// <summary>가이드 §4.6 — 지역당 10종(기본 5 / 특징 3 / 이벤트 2).</summary>
        private const int ExpectedSegmentCount = 10;

        /// <summary>대초원 4일이 소비하는 타일 수 (계획 §2.1 — 9,360 m ÷ 40).</summary>
        private const int GrasslandTileCount = 234;

        /// <summary>스탬피드 주행 측면 대역 (`StampedeSettings._min/_maxLateralOffset`).</summary>
        private const float StampedeBandMinX = 4f;
        private const float StampedeBandMaxX = 9f;

        private static readonly string[] Letters = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" };

        private static readonly float[] ExpectedWeights =
        {
            0.13f, 0.13f, 0.13f, 0.13f, 0.13f, 0.0833f, 0.0833f, 0.0834f, 0.05f, 0.05f
        };

        private static TerrainSegmentPalette LoadPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TerrainSegmentPalette>(PalettePath);
            Assert.IsNotNull(palette, $"팔레트 에셋이 없다: {PalettePath}");
            return palette;
        }

        private static GameObject LoadTile(int index)
        {
            var tile = AssetDatabase.LoadAssetAtPath<GameObject>($"{TilePrefix}{Letters[index]}.prefab");
            Assert.IsNotNull(tile, $"세그먼트 {Letters[index]} 프리팹이 없다");
            return tile;
        }

        [Test]
        public void 대초원_팔레트는_10종이다()
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
        public void 가중치가_기본_5_특징_3_이벤트_2_구성이고_합이_1이다()
        {
            float[] weights = LoadPalette().GetWeights();
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                Assert.AreEqual(ExpectedWeights[i], weights[i], 1e-4f, $"{Letters[i]} 가중치");
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
            Assert.IsTrue(flags[8], "I 풍차 기단 폐허");
            Assert.IsTrue(flags[9], "J 짐승 떼 이동로");
        }

        [Test]
        public void 대초원은_구간_편성을_쓰지_않는다()
        {
            // 2단 추첨은 북극만 쓴다 — 편성을 비워야 타일마다 독립 추첨이 된다(팔레트 폴백과 같은 규약).
            Assert.IsNull(LoadPalette().GroupSchedule, "구간 편성이 있으면 가중치가 군 안에서만 산다");
        }

        [Test]
        public void 대초원_전_구간에서_빈_타일이_나오지_않는다()
        {
            // 이 테스트가 지키는 것 — "오류 없이 대초원이 통째로 비는" 상태.
            TerrainSegmentPalette palette = LoadPalette();
            float[] weights = palette.GetWeights();
            bool[] noRepeat = palette.GetNoRepeatFlags();
            var scratch = new float[weights.Length];

            for (int tileIndex = 0; tileIndex < GrasslandTileCount; tileIndex++)
            {
                int pick = SegmentPickLogic.PickForTile(
                    tileIndex, weights, noRepeat, palette.GetEntryGroups(), palette.GroupSchedule, scratch);
                Assert.GreaterOrEqual(pick, 0, $"타일 {tileIndex}에서 추첨이 무효(-1)다");
                Assert.IsNotNull(palette.GetPrefab(pick), $"타일 {tileIndex}가 빈 프리팹을 골랐다");
            }
        }

        [Test]
        public void 지역_데이터가_이_팔레트를_가리킨다()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            Assert.IsNotNull(region, $"지역 에셋이 없다: {RegionPath}");
            Assert.IsNotNull(region.SegmentPalette,
                "Region_Grassland 가 팔레트를 가리키지 않는다 — 폴백이 TerrainTileGrassland 한 장으로 떨어진다");
            Assert.AreEqual(ExpectedSegmentCount, region.SegmentPalette.Count);
        }

        [Test]
        public void 지역_하늘과_안개가_대초원_값으로_배선돼_있다()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            Assert.IsNotNull(region.SkyboxMaterial, "Region_Grassland 에 하늘이 없다");
            Assert.IsTrue(region.OverridesFog, "Region_Grassland 이 fog 를 소유하지 않는다");

            // 0.0012 — 다섯 지역 중 가장 옅다. far clip 1,000 m 에서 23.7 % 가 남아야
            // "지평선까지 이어진 황금 물결"(가이드 §7.4)이 문자 그대로 성립한다.
            Assert.AreEqual(0.0012f, region.DayFogDensity, 1e-6f);
            Assert.AreEqual(0.0012f, region.NightFogDensity, 1e-6f);
            Assert.AreNotEqual(region.DayFogColor, region.NightFogColor, "낮과 밤 안개 색이 같다");
        }

        [Test]
        public void 안개_0_0012는_far_clip_에서_23_7_퍼센트를_남긴다()
        {
            // ExponentialSquared 투과율 = exp(-(density × d)^2)
            Assert.AreEqual(23.7f, Transmittance(0.0012f, 1000f), 0.2f);
            Assert.AreEqual(39.8f, Transmittance(0.0012f, 800f), 0.2f);
            Assert.AreEqual(69.8f, Transmittance(0.0012f, 500f), 0.2f);
        }

        [Test]
        public void 앵커_구성이_자원_수요와_맞는다()
        {
            // 대초원 자원은 벼 0.40 · 식재료 0.35 · 목재 0.15(전부 Ground) · 돌 0.10(Rock)이다.
            // 가이드 §7.4 원안(Ground 65 · Water 20 · Wreck 15)은 Water·Wreck 35 %가 통째로 노는 값이었다
            // (계획 §3.3) — Water 앵커를 쓰는 원목·얼음도, Wreck 을 쓰는 고철·소금·유적 부품도 대초원엔 없다.
            int ground = 0, rock = 0, water = 0, wreck = 0;
            for (int i = 0; i < Letters.Length; i++)
            {
                ResourceAnchor[] anchors = LoadTile(i).GetComponentsInChildren<ResourceAnchor>(true);
                Assert.AreEqual(6, anchors.Length, $"{Letters[i]} 앵커 수 — 기준은 타일당 5~7개다");
                for (int k = 0; k < anchors.Length; k++)
                {
                    switch (anchors[k].Kind)
                    {
                        case ResourceAnchorKind.Rock: rock++; break;
                        case ResourceAnchorKind.Water: water++; break;
                        case ResourceAnchorKind.Wreck: wreck++; break;
                        default: ground++; break;
                    }
                }
            }

            Assert.AreEqual(60, ground + rock + water + wreck);
            Assert.AreEqual(51, ground, "Ground 85 % — 벼 + 식재료 + 목재");
            Assert.AreEqual(9, rock, "Rock 15 % — 돌");
            Assert.AreEqual(0, water, "대초원에 물가 앵커를 두지 않는다 — 관개 수로에서 벼가 솟는다");
            Assert.AreEqual(0, wreck, "대초원에 잔해 앵커를 두지 않는다 — 수요가 0이다");
        }

        [Test]
        public void 스탬피드_대역_4에서_9미터에_연속_장벽이_없다()
        {
            // 계획 §3.4 — 가이드가 적은 "24 m 안쪽"은 밤 웨이브 대역이고, 무리가 실제로 훑는 곳은
            // 클리어 존이 끝나는 4 m 부터 9 m 까지다. 들소 회피 프로브는 3 m 뿐이라 여기에
            // 8 m 초과 연속 장벽이 하나라도 있으면 열이 무너진다.
            for (int i = 0; i < Letters.Length; i++)
            {
                GameObject tile = LoadTile(i);
                Matrix4x4 toTile = tile.transform.worldToLocalMatrix;
                MeshFilter[] filters = tile.GetComponentsInChildren<MeshFilter>(true);
                for (int k = 0; k < filters.Length; k++)
                {
                    Mesh mesh = filters[k].sharedMesh;
                    if (mesh == null)
                    {
                        continue;
                    }

                    Bounds bounds = ClearZoneRules.TransformAabb(
                        mesh.bounds, toTile * filters[k].transform.localToWorldMatrix);

                    if (!ClearZoneRules.OverlapsBandX(bounds, StampedeBandMinX, StampedeBandMaxX)
                        || !ClearZoneRules.RisesAboveGround(bounds)
                        || bounds.size.z <= ClearZoneRules.MaxWallLengthZ
                        || bounds.size.y < ClearZoneRules.WallMinHeightY
                        || ClearZoneRules.IsMountableStep(bounds))
                    {
                        continue;
                    }

                    Assert.Fail(
                        $"{Letters[i]}/{filters[k].name} 가 4~9 m 대역을 {bounds.size.z:F1} m 막는다 " +
                        $"(높이 {bounds.size.y:F1} m) — 무리가 갇힌다");
                }
            }
        }

        [Test]
        public void 타일_예산이_목표_안에_있다()
        {
            // 가이드 §8.1 — 타일당 오브젝트 30 이하, 가중 평균 tris 30,000 이하(계획 §6.4).
            float[] weights = LoadPalette().GetWeights();
            float weightedTris = 0f;
            for (int i = 0; i < Letters.Length; i++)
            {
                GameObject tile = LoadTile(i);
                int renderers = tile.GetComponentsInChildren<MeshRenderer>(true).Length;
                Assert.LessOrEqual(renderers, 30, $"{Letters[i]} 렌더러 {renderers}개");

                int tris = 0;
                MeshFilter[] filters = tile.GetComponentsInChildren<MeshFilter>(true);
                for (int k = 0; k < filters.Length; k++)
                {
                    if (filters[k].sharedMesh != null)
                    {
                        tris += filters[k].sharedMesh.triangles.Length / 3;
                    }
                }

                Assert.LessOrEqual(tris, 30000, $"{Letters[i]} tris {tris}");
                weightedTris += tris * weights[i];
            }

            Assert.LessOrEqual(weightedTris, 30000f, "가중 평균 tris");
            // 활성 9장이 목표 270,000 의 절반을 넘지 않는다 (사막 46 % · 북극 34 %)
            Assert.LessOrEqual(weightedTris * 9f, 135000f, "활성 9장");
        }

        [Test]
        public void 스캐터_슬롯이_타일마다_기준_안에_있다()
        {
            for (int i = 0; i < Letters.Length; i++)
            {
                int slots = LoadTile(i).GetComponentsInChildren<ScatterSlot>(true).Length;
                Assert.IsTrue(ClearZoneRules.IsScatterSlotCountValid(slots),
                    $"{Letters[i]} 스캐터 슬롯 {slots}개 — 기준은 4~10개다");
            }
        }

        private static float Transmittance(float density, float distance)
        {
            float x = density * distance;
            return Mathf.Exp(-x * x) * 100f;
        }
    }
}
