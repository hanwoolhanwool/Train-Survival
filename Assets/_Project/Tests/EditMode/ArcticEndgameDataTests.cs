using Game.Gameplay.Inventory;
using Game.Gameplay.Monsters;
using Game.Gameplay.Player;
using Game.Gameplay.Region;
using Game.Gameplay.Train;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// M7 3차 검증 잔여 36건 중 <b>플레이 없이 판정되는 것</b>의 계기 (북극 지역 구현 계획 §9).
    ///
    /// <para><b>왜 뒤늦게 계기를 다는가.</b> 이 항목들은 1회차 검증(2026-08-14)에서 <b>화면을 기다리다</b>
    /// 미판정으로 남았다. 그런데 실제로 물어보는 것의 상당수는 <b>에셋 값과 순수 계산</b>이다 —
    /// 강화 난방로가 연료를 얼마나 태우는가 · 보스 핵이 몇 개 떨어지는가 · 유적 부품이 어느 지역에서
    /// 나오는가. 그 몫을 여기서 닫고, <b>남는 것만</b> 플레이로 넘긴다.</para>
    ///
    /// <para><b>여기 없는 것이 곧 Play 몫이다</b> — 체감(K1~K4) · 화면(C5) · 복제·재접속(C8~C10) ·
    /// 상호작용(F1·F4·F5·F7 · I1·I4·I7 · J2). 검증 문서가 그 목록을 소유한다.</para>
    /// </summary>
    public sealed class ArcticEndgameDataTests
    {
        private const string Data = "Assets/_Project/Data/";

        private static T Load<T>(string name) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Data + name + ".asset");
            Assert.IsNotNull(asset, $"에셋이 없다: {Data}{name}.asset");
            return asset;
        }

        // ── A 구역 · W5 — 지역 등재와 재순환 ─────────────────────────

        [Test]
        public void A9_북극_등재는_에셋만으로_성립한다()
        {
            // 판정의 실체 — 지역 배열에 한 칸 늘고 그 칸의 에셋이 채워져 있으면 끝인가.
            var timeline = Load<RegionTimelineSettings>("RegionTimelineSettings");
            Assert.AreEqual(5, timeline.RegionCount, "숲 · 사막 · 바다 · 대초원 · 북극");

            var arctic = timeline.GetRegion(4);
            Assert.IsNotNull(arctic);
            Assert.AreEqual("북극", arctic.DisplayName);
            Assert.AreEqual(3, arctic.DayCount);

            // 진입 Day — 검증 문서의 "Day 14"는 바다가 끼기 전 값이다 (§3.4).
            int firstDay = 1;
            for (int i = 0; i < 4; i++)
            {
                firstDay += timeline.GetRegion(i).DayCount;
            }

            Assert.AreEqual(17, firstDay, "북극 첫날");
        }

        [Test]
        public void W5_재순환은_난이도에_정확히_1_5배를_건다()
        {
            // 검증 방법: 같은 지역·같은 일차의 밤 웨이브 로그를 사이클 0과 1에서 비교한다.
            var timeline = Load<RegionTimelineSettings>("RegionTimelineSettings");
            Assert.AreEqual(0.5f, timeline.CycleDifficultyBonus, 1e-4f);

            float cycle0 = 1f + 0 * timeline.CycleDifficultyBonus;
            float cycle1 = 1f + 1 * timeline.CycleDifficultyBonus;
            Assert.AreEqual(1.5f, cycle1 / cycle0, 1e-4f);
        }

        [Test]
        public void W4_북극_중간_강화_밤은_2일차_하나뿐이다()
        {
            // 1회차에서 A7이 X 였고 "데이터·로직은 정상"으로 확인된 뒤 재현 조건을 바꿔 재검하기로 한 항목.
            // 로직 몫은 여기서 닫는다 — 남는 것은 "웨이브가 켜진 상태에서 로그가 뜨는가"뿐이다.
            var timeline = Load<RegionTimelineSettings>("RegionTimelineSettings");
            int[] counts = timeline.GetDayCounts();

            Assert.IsFalse(RegionTimelineMath.Evaluate(17, counts, 2, true).IsReinforcedNight, "진입 당일");
            Assert.IsTrue(RegionTimelineMath.Evaluate(18, counts, 2, true).IsReinforcedNight, "북극 2일차");
            Assert.IsFalse(RegionTimelineMath.Evaluate(19, counts, 2, true).IsReinforcedNight, "보스 밤과 배타적");
        }

        // ── F 구역 — 난방 2단 ─────────────────────────

        [Test]
        public void F1_강화_난방로는_일반_난방기의_두_배_이상_든다()
        {
            var catalog = Load<StructureCatalog>("StructureCatalog");
            int heater = catalog.GetBuildCost(StructureKind.Heater, -1);
            int furnace = catalog.GetBuildCost(StructureKind.Furnace, -1);

            Assert.AreEqual(3, heater);
            Assert.AreEqual(7, furnace);
            Assert.GreaterOrEqual(furnace, heater * 2);
        }

        [Test]
        public void F2_연료가_있는_동안은_완전한_안전지대다()
        {
            var settings = Load<TemperatureSettings>("TemperatureSettings");
            var arctic = Load<RegionDefinition>("Region_Arctic");
            TemperatureCurve curve = settings.ToCurve();

            // 북극 밤 −32 ℃ · 맨몸(단열 0) · 연료가 살아 있는 강화 난방로.
            float furnaceTarget = TemperatureMath.ResolveHeaterTarget(
                settings.HeaterTargetNight, arctic.NightAmbientTemperature, 0f,
                settings.HeaterColdPenaltyPerDegree, negatesColdPenalty: true, curve);

            Assert.AreEqual(36f, furnaceTarget, 1e-4f, "페널티가 0이 된다");
            Assert.Greater(furnaceTarget, 35f, "저온 경고가 뜨지 않는다");
        }

        [Test]
        public void F4_연료가_떨어지면_일반_난방기와_같아진다()
        {
            var settings = Load<TemperatureSettings>("TemperatureSettings");
            var arctic = Load<RegionDefinition>("Region_Arctic");
            TemperatureCurve curve = settings.ToCurve();

            float dry = TemperatureMath.ResolveHeaterTarget(
                settings.HeaterTargetNight, arctic.NightAmbientTemperature, 0f,
                settings.HeaterColdPenaltyPerDegree, negatesColdPenalty: false, curve);

            // 결핍 = 쾌적 하한 10 − (−32) = 42 → 36 − 42 × 0.04 = 34.32 (경고대, 피해는 면한다).
            Assert.AreEqual(34.32f, dry, 0.01f);
            Assert.Less(dry, 35f, "저온 경고가 켜진다");
            Assert.Greater(dry, settings.ColdDamageThreshold, "체력은 깎이지 않는다");
        }

        [Test]
        public void F3_강화_난방로만_주행_연료를_태운다()
        {
            var catalog = Load<StructureCatalog>("StructureCatalog");

            Assert.AreEqual(0.6f, catalog.GetHeaterFuelPerSecond(StructureKind.Furnace), 1e-4f);

            // F6(무회귀) — 다른 종류는 전부 0이라, 강화 난방로를 짓지 않으면 소모율이 예전 그대로다.
            for (int i = 0; i < catalog.EntryCount; i++)
            {
                StructureKind kind;
                if (!catalog.TryGetKindAt(i, out kind) || kind == StructureKind.Furnace)
                {
                    continue;
                }

                Assert.AreEqual(0f, catalog.GetHeaterFuelPerSecond(kind), 1e-4f,
                    $"{kind} 가 연료를 태우면 F6 무회귀가 깨진다");
            }
        }

        [Test]
        public void F_강화_난방로는_난방을_제공하고_정수기는_아니다()
        {
            var catalog = Load<StructureCatalog>("StructureCatalog");

            Assert.IsTrue(catalog.ProvidesHeat(StructureKind.Furnace));
            Assert.IsTrue(catalog.ProvidesHeat(StructureKind.Heater));
            Assert.IsFalse(catalog.ProvidesHeat(StructureKind.Purifier), "정수기는 난방이 없다 (E1 규격)");
            Assert.AreEqual(4, catalog.GetBuildCost(StructureKind.Purifier, -1));
        }

        // ── I 구역 — 북극 보스 ─────────────────────────

        [Test]
        public void I3_페이즈가_3단이다()
        {
            var boss = Load<BossDefinition>("BossDefinition_Arctic");
            Assert.AreEqual(3, boss.PhaseCount, "임계 2개 = 페이즈 3단");
            Assert.AreEqual(0.7f, boss.PhaseHealthThresholds[0], 1e-4f);
            Assert.AreEqual(0.35f, boss.PhaseHealthThresholds[1], 1e-4f);
            Assert.Less(boss.PhaseCooldownScalePerPhase, 1f, "단계가 오를수록 주기가 짧아진다");
            Assert.Greater(boss.PhaseSpeedBonusPerPhase, 0f, "단계가 오를수록 빨라진다");
        }

        [Test]
        public void I5_보스_핵이_지금까지_최다다()
        {
            var arctic = Load<BossDefinition>("BossDefinition_Arctic");
            string[] others = { "BossDefinition_Forest", "BossDefinition_Desert", "BossDefinition_Grassland", "BossDefinition_Sea" };

            int arcticCores = CountDrops(arctic, ResourceType.BossCore);
            Assert.AreEqual(3, arcticCores);

            for (int i = 0; i < others.Length; i++)
            {
                Assert.Less(CountDrops(Load<BossDefinition>(others[i]), ResourceType.BossCore), arcticCores,
                    $"{others[i]} 보다 많아야 '지금까지 최다'다");
            }
        }

        [Test]
        public void I6_보스_한_쌍_추가는_에셋_두_개다()
        {
            // 2차가 세운 골격의 검증 — 지역이 보스 정의를 가리키고, 정의가 프리팹을 가리키면 끝인가.
            var arctic = Load<RegionDefinition>("Region_Arctic");
            Assert.IsNotNull(arctic.BossDefinition, "지역이 보스를 가리키지 않는다");
            Assert.AreEqual("설원의 파수꾼", arctic.BossDefinition.DisplayName);
            Assert.AreEqual(1300f, arctic.BossDefinition.MaxHealth, 1e-3f);
            Assert.IsNotNull(arctic.BossDefinition.Prefab, "보스 프리팹 참조가 끊겼다");
        }

        [Test]
        public void I2_수치는_사막_보스와_다르다()
        {
            // "같은 골격, 다른 수치·외형" 중 <b>수치</b> 몫. 외형은 아래 별도 항목이 잡는다.
            var arctic = Load<BossDefinition>("BossDefinition_Arctic");
            var desert = Load<BossDefinition>("BossDefinition_Desert");

            Assert.AreEqual(arctic.SignaturePattern, desert.SignaturePattern, "같은 골격 — 둘 다 투사체");
            Assert.AreNotEqual(arctic.MaxHealth, desert.MaxHealth, "1300 vs 1100");
            Assert.AreNotEqual(arctic.ContactDamage, desert.ContactDamage, "20 vs 18");
            Assert.AreNotEqual(arctic.ChargeDamage, desert.ChargeDamage, "32 vs 30");
            Assert.Less(arctic.SignatureIntervalSeconds, desert.SignatureIntervalSeconds, "얼음 파편이 더 잦다 (5 s)");
        }

        [Test]
        public void I2_외형은_아직_사막_보스와_같다()
        {
            // ⚠ 이것은 <b>결함의 기록</b>이다 (계획 §11 리스크 7 — 결정 ⑪이 계획 밖으로 뒀다).
            // 투사체 뷰 프리팹을 공유하므로 "백색·서리 계열"이 아직 성립하지 않는다.
            // 고쳐지면 이 테스트가 실패하고, 그때 검증 문서의 I2 를 통과로 바꾼다.
            var arctic = Load<BossDefinition>("BossDefinition_Arctic");
            var desert = Load<BossDefinition>("BossDefinition_Desert");

            Assert.AreSame(arctic.ProjectileViewPrefab, desert.ProjectileViewPrefab,
                "투사체 외형이 갈렸다면 I2 를 통과로 바꾸고 이 테스트를 지운다");
        }

        // ── J 구역 — 유적 자원 ─────────────────────────

        [Test]
        public void J1_유적_부품은_북극에서만_나온다()
        {
            var timeline = Load<RegionTimelineSettings>("RegionTimelineSettings");
            for (int i = 0; i < timeline.RegionCount; i++)
            {
                RegionDefinition region = timeline.GetRegion(i);
                float weight = FindSpawnWeight(region, ResourceType.RelicPart);
                if (i == 4)
                {
                    Assert.AreEqual(0.15f, weight, 1e-4f, "북극 유적 부품");
                }
                else
                {
                    Assert.AreEqual(0f, weight, 1e-4f, $"{region.DisplayName} 에서 유적 부품이 나온다");
                }
            }
        }

        [Test]
        public void J1_가장_드문_북극_자원은_유적_부품이_아니라_고철이다()
        {
            // 검증 문서는 유적 부품(0.15)을 "가장 드물다"로 적었으나 고철이 0.10 이다.
            // 판정 기준이 되는 문장이라 여기서 못박는다.
            var arctic = Load<RegionDefinition>("Region_Arctic");
            Assert.AreEqual(0.10f, FindSpawnWeight(arctic, ResourceType.Scrap), 1e-4f);
            Assert.Greater(FindSpawnWeight(arctic, ResourceType.RelicPart), FindSpawnWeight(arctic, ResourceType.Scrap));
        }

        [Test]
        public void J3_유적_부품과_희귀_금속은_연료도_건자재도_아니다()
        {
            var catalog = Load<ResourceCatalog>("ResourceCatalog");
            ResourceType[] types = { ResourceType.RelicPart, ResourceType.RareMetal };

            for (int i = 0; i < types.Length; i++)
            {
                Assert.AreEqual(0f, catalog.GetFuelValue(types[i]), 1e-4f, $"{types[i]} 를 엔진에 넣을 수 있다");
                Assert.IsFalse(catalog.IsBuildMaterial(types[i]), $"{types[i]} 가 건설 비용으로 쓰인다");
            }

            Assert.AreEqual(5, catalog.GetMaxStack(ResourceType.RelicPart, -1), "스택 5");
        }

        [Test]
        public void J4_얼음과_소금은_같은_지역에_나오지_않는다()
        {
            // 두 색(#59B8E0 · #ADE6F7)은 둘 다 하늘색 계열이라 나란히 두면 헷갈린다.
            // 그런데 <b>같은 화면에 나올 수 없다</b> — 이것이 판정의 실체다.
            var timeline = Load<RegionTimelineSettings>("RegionTimelineSettings");
            for (int i = 0; i < timeline.RegionCount; i++)
            {
                RegionDefinition region = timeline.GetRegion(i);
                bool ice = FindSpawnWeight(region, ResourceType.Ice) > 0f;
                bool salt = FindSpawnWeight(region, ResourceType.Salt) > 0f;
                Assert.IsFalse(ice && salt, $"{region.DisplayName} 에 얼음과 소금이 함께 난다");
            }
        }

        [Test]
        public void J4_북극_신규_자원_다섯의_색이_서로_구분된다()
        {
            var catalog = Load<ResourceCatalog>("ResourceCatalog");
            ResourceType[] types =
            {
                ResourceType.Ice, ResourceType.RareMetal, ResourceType.RelicPart,
                ResourceType.PurifiedWater, ResourceType.WarmingTea,
            };

            for (int a = 0; a < types.Length; a++)
            {
                for (int b = a + 1; b < types.Length; b++)
                {
                    // 아래 별도 항목이 결함으로 기록한 한 쌍은 여기서 뺀다.
                    if (types[a] == ResourceType.Ice && types[b] == ResourceType.PurifiedWater)
                    {
                        continue;
                    }

                    float distance = ColorDistance(catalog, types[a], types[b]);
                    Assert.Greater(distance, 0.25f, $"{types[a]} 와 {types[b]} 색이 너무 가깝다 ({distance:0.00})");
                }
            }
        }

        [Test]
        public void J4_얼음과_식수의_색이_너무_가깝다_결함()
        {
            // ⚠ <b>결함의 기록</b> (2026-08-30 검증에서 발견). 얼음 <c>#59B8E0</c> 와
            // 식수 <c>#4C8CF2</c> 의 RGB 거리가 <b>0.19</b> 로, 다섯 신규 자원 중 유일하게 기준(0.25) 아래다.
            //
            // <para>지상 노드에서는 부딪히지 않는다 — 식수는 정수기 제작물이라 땅에 나지 않는다.
            // 문제가 되는 자리는 <b>인벤토리·창고 아이콘</b>이고, 얼음 3 → 식수 2 정수는 그 둘을
            // <b>나란히 놓는 유일한 작업</b>이다.</para>
            //
            // <para><b>고치지 않았다</b> — 색은 아트 판단이고, 이 차수는 검증이지 재설계가 아니다
            // (계획 §11 리스크 8). 고치면 이 테스트가 실패하고, 그때 검증 문서의 J4 를 통과로 바꾼다.</para>
            var catalog = Load<ResourceCatalog>("ResourceCatalog");
            float distance = ColorDistance(catalog, ResourceType.Ice, ResourceType.PurifiedWater);

            Assert.AreEqual(0.19f, distance, 0.01f,
                "색이 갈렸다면 J4 를 통과로 바꾸고 이 테스트를 지운다");
        }

        private static float ColorDistance(ResourceCatalog catalog, ResourceType a, ResourceType b)
        {
            Color ca = catalog.GetColor(a, Color.magenta);
            Color cb = catalog.GetColor(b, Color.magenta);
            return Mathf.Sqrt(
                (ca.r - cb.r) * (ca.r - cb.r) + (ca.g - cb.g) * (ca.g - cb.g) + (ca.b - cb.b) * (ca.b - cb.b));
        }

        // ── K 구역 — 에셋 조정 가능성 (K5) ─────────────────────────

        [Test]
        public void K5_체감_조정_다섯_축이_전부_에셋에_있다()
        {
            // M4 규약 — "밸런싱은 코드가 아니라 에셋". 다섯 축이 실제로 데이터인지만 확인한다
            // (값이 맞는지는 K1~K4 의 체감 판정 몫이다).
            var arctic = Load<RegionDefinition>("Region_Arctic");
            Assert.AreEqual(-32f, arctic.NightAmbientTemperature, 1e-4f, "① 북극 밤 온도");

            var temperature = Load<TemperatureSettings>("TemperatureSettings");
            FrostbiteCurve frostbite = temperature.ToFrostbiteCurve();
            Assert.Greater(frostbite.ProgressPerSecond, 0f, "② 동상 진행 속도");
            Assert.AreEqual(0.04f, temperature.HeaterColdPenaltyPerDegree, 1e-4f, "③ 난방기 페널티");

            var catalog = Load<StructureCatalog>("StructureCatalog");
            Assert.Greater(catalog.GetHeaterFuelPerSecond(StructureKind.Furnace), 0f, "④ 강화 난방로 연료");

            var blizzard = Load<WeatherDefinition>("Weather_Blizzard");
            Assert.AreEqual(0.55f, blizzard.HarpoonRangeMultiplier, 1e-4f, "⑤ 폭설 집게 배율");
        }

        // ── §3.3 — 서리 오버레이 ↔ 동상 결빙 ─────────────────────────

        [Test]
        public void 날씨는_화면_서리를_그리지_않는다()
        {
            // 계획 §3.3 은 날씨 서리와 동상 결빙이 <b>같은 자리</b>를 써서 플레이어가 동상 경고를
            // 날씨로 오인할 위험을 적었다. 1차 as-built 에서 날씨 연출은 fog + 입자뿐이고
            // <b>화면 서리 오버레이를 만들지 않았으므로</b> 그 충돌이 발생하지 않는다.
            // 나중에 서리를 넣으면 이 테스트가 실패하고, 그때 §3.3 의 구분 규칙을 적용한다.
            var blizzard = Load<WeatherDefinition>("Weather_Blizzard");
            var coldSnap = Load<WeatherDefinition>("Weather_ColdSnap");

            Assert.Greater(blizzard.FogDensity, 0f, "폭설의 화면 개입은 fog 다");
            Assert.AreEqual(0f, blizzard.AmbientTemperatureOffset, 1e-4f, "폭설은 온도에 개입하지 않는다");
            Assert.AreEqual(-10f, coldSnap.AmbientTemperatureOffset, 1e-4f, "혹한파 = 온도");
            Assert.AreEqual(1f, coldSnap.HarpoonRangeMultiplier, 1e-4f, "혹한파는 집게에 개입하지 않는다");
        }

        // ── 보조 ─────────────────────────

        private static int CountDrops(BossDefinition boss, ResourceType type)
        {
            int total = 0;
            for (int i = 0; i < boss.DropCount; i++)
            {
                BossDefinition.DropEntry drop = boss.GetDrop(i);
                if (drop != null && drop.Type == type)
                {
                    total += drop.Count;
                }
            }

            return total;
        }

        private static float FindSpawnWeight(RegionDefinition region, ResourceType type)
        {
            for (int i = 0; i < region.ResourceSpawnCount; i++)
            {
                RegionDefinition.ResourceSpawnEntry entry = region.GetResourceSpawn(i);
                if (entry != null && entry.Type == type)
                {
                    return entry.Weight;
                }
            }

            return 0f;
        }
    }
}
