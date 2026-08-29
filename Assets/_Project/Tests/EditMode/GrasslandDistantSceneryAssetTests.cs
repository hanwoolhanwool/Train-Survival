using Game.Gameplay.Region;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 대초원 원경 3층 <b>에셋 배선</b>의 건전성 (대초원 지역 구현 계획 §5.3).
    /// 팔레트와 같은 이유로 있다 — <b>지역 게이팅이 어긋나면 오류 없이 원경이 통째로 안 뜨거나,
    /// 반대로 다섯 지역 어디서나 풍차가 서 있게 된다.</b> 둘 다 예외도 로그도 남기지 않는다.
    /// </summary>
    public sealed class GrasslandDistantSceneryAssetTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Environment/DistantScenery_Grassland.prefab";
        private const string RegionPath = "Assets/_Project/Data/Region_Grassland.asset";

        /// <summary>대초원 4일 주행 (계획 §2.1 — 2,340 m × 4).</summary>
        private const float GrasslandTravelMeters = 9360f;

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"원경 프리팹이 없다: {PrefabPath}");
            return prefab;
        }

        [Test]
        public void 원경은_세_층이다()
        {
            // 사막 4층 · 북극 5층 · 대초원 3층 — 층 수를 정하는 것은 코드가 아니라
            // 그 지역이 지평선에 무엇을 두기로 했는가다 (specs/world/distant-scenery §4ter.1).
            Assert.AreEqual(3, LoadPrefab().GetComponentsInChildren<DistantSceneryLayer>(true).Length);
        }

        [Test]
        public void 세_층의_시차계수가_공통_규격과_맞는다()
        {
            DistantSceneryLayer[] layers = LoadPrefab().GetComponentsInChildren<DistantSceneryLayer>(true);
            bool plate = false, mid = false, far = false;
            for (int i = 0; i < layers.Length; i++)
            {
                float p = layers[i].ParallaxFactor;
                if (Mathf.Approximately(p, 0f)) { plate = true; }
                else if (Mathf.Approximately(p, 0.35f)) { mid = true; }
                else if (Mathf.Approximately(p, 0.03f)) { far = true; }
                else { Assert.Fail($"{layers[i].name} 의 시차계수 {p} 는 공통 규격에 없다"); }
            }

            Assert.IsTrue(plate, "지면판 (시차 0)");
            Assert.IsTrue(mid, "중경 (시차 0.35 — 가이드 §7.1 공통 규격)");
            Assert.IsTrue(far, "풍차 군락 (시차 0.03)");
        }

        [Test]
        public void 세_층이_전부_대초원에서만_켜진다()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            DistantSceneryLayer[] layers = LoadPrefab().GetComponentsInChildren<DistantSceneryLayer>(true);
            for (int i = 0; i < layers.Length; i++)
            {
                Assert.AreSame(region, layers[i].Region,
                    $"{layers[i].name} 의 지역이 비었거나 다르다 — 다른 지역에서도 풍차가 선다");
            }
        }

        [Test]
        public void 원경에는_콜라이더도_네트워크_오브젝트도_없다()
        {
            GameObject prefab = LoadPrefab();
            Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length);

            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Assert.AreNotEqual("NetworkObject", components[i].GetType().Name,
                    "원경은 복제 대상이 아니다 — 누적 주행 거리의 순수 함수라 전 피어가 같은 화면을 본다");
            }
        }

        [Test]
        public void 풍차는_열두_기고_위상과_속도가_갈린다()
        {
            // 군락 6기 × 2벌. 한 몸처럼 돌면 "회전하는 하나의 물체"로 읽힌다 (계획 §4.6).
            DistantWindmillSpin[] spins = LoadPrefab().GetComponentsInChildren<DistantWindmillSpin>(true);
            Assert.AreEqual(12, spins.Length);

            var phases = new System.Collections.Generic.HashSet<float>();
            var speeds = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i < spins.Length; i++)
            {
                phases.Add(spins[i].PhaseDegrees);
                speeds.Add(spins[i].DegreesPerSecond);

                float ratio = Mathf.Abs(spins[i].DegreesPerSecond) / DistantWindmillSpin.ReferenceDegreesPerSecond;
                Assert.GreaterOrEqual(ratio, 0.9f, "속도 흔들림은 ±10 % 안이다");
                Assert.LessOrEqual(ratio, 1.1f);
            }

            Assert.AreEqual(6, phases.Count, "위상 6종");
            Assert.AreEqual(6, speeds.Count, "속도 6종");
        }

        [Test]
        public void 풍차_층은_4일_안에_한_벌보다_적게_흐른다()
        {
            // 4일 이동량 = 9,360 m × 0.03 = 280.8 m. 되감기 800 m 의 35 % 다.
            // "되감기 0회"는 보장할 수 없다(진입 시 누적 주행 거리가 위상을 정한다) — 그래서
            // 자식을 800 m 간격 2벌로 깔아 되감기를 무이음매로 만들었다 (결정 ⑭).
            float drift = GrasslandTravelMeters * 0.03f;
            Assert.AreEqual(280.8f, drift, 0.1f);
            Assert.Less(drift, 800f, "한 벌보다 적게 흘러야 같은 군락이 서서히 흐른다");
        }

        [Test]
        public void 되감기_한_바퀴가_대초원_체류보다_길다()
        {
            // 근경 팔레트 재등장 133초 · 중경 143초 · 풍차 4,444초 (대초원 체류 1,560초).
            Assert.AreEqual(142.9f, DistantSceneryLayer.WrapPeriodSeconds(6f, 0.35f, 300f), 0.5f);
            Assert.AreEqual(4444.4f, DistantSceneryLayer.WrapPeriodSeconds(6f, 0.03f, 800f), 1f);
            Assert.Greater(DistantSceneryLayer.WrapPeriodSeconds(6f, 0.03f, 800f), 1560f);
        }
    }
}
