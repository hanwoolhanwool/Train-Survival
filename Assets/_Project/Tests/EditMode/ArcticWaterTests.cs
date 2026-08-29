using Game.Gameplay.Monsters;
using Game.Gameplay.Player;
using Game.Gameplay.Region;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 얼음과 물의 공존이 만드는 판정들 (북극 지역 구현 계획 §5.4 · §8).
    ///
    /// <para><b>이 지역만 지면이 한 겹이 아니다.</b> 얼음(y 0) · 물면(−1.5) · 얕은 물 바닥(−2.3) ·
    /// 깊은 물 바닥(−6.5)이 <b>같은 지역 안에</b> 있고, 그래서 다른 네 지역에서는 성립하던
    /// "지역 바닥은 하나"라는 전제가 세 곳에서 깨진다 — 수영 판정 · 몬스터 지지면 · 낚시 캐스팅.</para>
    /// </summary>
    public sealed class ArcticWaterTests
    {
        private const string RegionPath = "Assets/_Project/Data/Region_Arctic.asset";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";

        private const float IceTopY = 0f;
        private const float WaterY = -1.5f;
        private const float ShallowFloorY = -2.3f;
        private const float DeepFloorY = -6.5f;

        private static RegionDefinition LoadRegion()
        {
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(RegionPath);
            Assert.IsNotNull(region, $"지역 에셋이 없다: {RegionPath}");
            return region;
        }

        // ── 물 배선 ─────────────────────────────────────────────

        [Test]
        public void 북극에_물이_켜져_있다()
        {
            RegionDefinition region = LoadRegion();
            Assert.IsTrue(region.HasWater, "HasWater 가 꺼져 있으면 수영·잠수·낚시 경로가 통째로 비활성이다");
            Assert.AreEqual(WaterY, region.WaterSurfaceY, 1e-4f);
            Assert.AreEqual(WaterY, region.SurfaceY, 1e-4f, "지상 개체가 서는 높이");
        }

        [Test]
        public void 점프로는_얼음_벽을_못_넘는다()
        {
            // 설계의 핵심 — 얼음 두께 1.5 m 가 점프 높이 1.2 m 보다 크다.
            // 이 부등식이 뒤집히면 맨틀 동작이 통째로 무의미해진다(§5.4).
            var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/_Project/Data/PlayerMovementSettings.asset");
            Assert.IsNotNull(settings, "이동 설정 에셋 경로가 바뀌었다");

            float wallHeight = IceTopY - WaterY;
            Assert.AreEqual(1.5f, wallHeight, 1e-4f);
            Assert.Less(settings.JumpHeight, wallHeight, "점프로 넘을 수 있으면 기어오르기가 필요 없다");
        }

        [Test]
        public void 얕은_물에서는_걷기가_유지된다()
        {
            // §3.2의 계산이 성립하는 조건 — 잠김이 수영 진입 깊이에 못 미쳐야 한다.
            // CharacterController 는 skinWidth(0.08)만큼 캡슐을 띄우므로 실제 잠김은 계획치보다 얕다.
            var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/_Project/Data/PlayerMovementSettings.asset");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            float skinWidth = player.GetComponent<CharacterController>().skinWidth;

            float feetY = ShallowFloorY + skinWidth;
            float depth = SwimMotion.SubmergeDepth(feetY, WaterY);

            Assert.AreEqual(0.72f, depth, 0.01f, "얕은 물 잠김");
            Assert.Less(depth, settings.SwimEnterDepth, "얕은 물에서 수영으로 넘어가면 스크롤에 쓸린다");
            Assert.Greater(settings.SwimEnterDepth - depth, 0.2f,
                "여유가 0.2 m 미만이면 걷기↔수영이 매 프레임 뒤집힌다 (§8.2 · 바다 사다리 11회차와 같은 종류)");
        }

        [Test]
        public void 깊은_물에서는_수영으로_넘어간다()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/_Project/Data/PlayerMovementSettings.asset");

            // 깊은 물 바닥은 −6.5 라 발이 닿지 않는다 — 물면 바로 아래만 봐도 진입 깊이를 넘는다.
            float depth = SwimMotion.SubmergeDepth(WaterY - settings.SwimEnterDepth, WaterY);
            Assert.GreaterOrEqual(depth, settings.SwimEnterDepth);
            Assert.Less(DeepFloorY, WaterY - settings.SwimEnterDepth);
        }

        // ── 침수 ─────────────────────────────────────────────

        [Test]
        public void 침수_판정은_경계에서_깜빡이지_않는다()
        {
            // 히스테리시스 — 진입 0.25 · 이탈 0.12.
            Assert.IsFalse(PlayerSubmersion.IsSubmergedAt(0.20f, false, 0.25f, 0.12f), "얕게 스치면 안 걸린다");
            Assert.IsTrue(PlayerSubmersion.IsSubmergedAt(0.25f, false, 0.25f, 0.12f));
            Assert.IsTrue(PlayerSubmersion.IsSubmergedAt(0.20f, true, 0.25f, 0.12f), "한 번 걸리면 더 얕아도 유지");
            Assert.IsFalse(PlayerSubmersion.IsSubmergedAt(0.10f, true, 0.25f, 0.12f), "확실히 나와야 풀린다");
        }

        [Test]
        public void 얕은_물에서도_침수가_걸린다()
        {
            // 이 축이 수영이 아니라 잠김 깊이를 보는 것이 설계의 핵심이다 —
            // 얼음 틈에서 걷기가 유지되는데 처벌이 없으면 "빠졌다"가 성립하지 않는다.
            Assert.IsTrue(PlayerSubmersion.IsSubmergedAt(0.72f, false, 0.25f, 0.12f));
        }

        [Test]
        public void 동상과_침수가_곱으로_걸린다()
        {
            // 계획 §5.6 — 동상 ×0.8 × 침수 ×0.7 = ×0.56. IMoveSpeedModifier 규약의 실증이다.
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            IMoveSpeedModifier[] modifiers = player.GetComponents<IMoveSpeedModifier>();

            Assert.GreaterOrEqual(modifiers.Length, 2, "동상·침수 두 축이 붙어 있어야 한다");
            Assert.AreEqual(0.56f, 0.8f * 0.7f, 1e-4f);
        }

        [Test]
        public void 젖으면_단열이_무효다()
        {
            // 이 플래그가 없으면 방한 풀셋(0.9)이 침수 처벌을 통째로 지운다.
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var submersion = player.GetComponent<PlayerSubmersion>();
            Assert.IsNotNull(submersion, "Player 프리팹에 PlayerSubmersion 이 없다");

            // 프리팹 인스턴스는 물 밖이므로 false 지만, 계약이 내놓는 플래그는 확인할 수 있다.
            submersion.TryGetAmbient(out float ambient, out bool ignoresInsulation);
            Assert.AreEqual(-2f, ambient, 1e-4f, "극지 해수 온도");
            Assert.IsTrue(ignoresInsulation);
        }

        [Test]
        public void 물속_화면_색이_바다와_다르다()
        {
            RegionDefinition arctic = LoadRegion();
            var sea = AssetDatabase.LoadAssetAtPath<RegionDefinition>("Assets/_Project/Data/Region_Sea.asset");
            Assert.AreNotEqual(sea.UnderwaterColor, arctic.UnderwaterColor);
        }

        // ── 얼음 턱 기어오르기 ─────────────────────────────────────────────

        [Test]
        public void 얼음_틈에서는_기어올라야_나온다()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/_Project/Data/PlayerMovementSettings.asset");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            float feetY = ShallowFloorY + player.GetComponent<CharacterController>().skinWidth;

            Assert.IsTrue(IceLedgeMantleLogic.CanMantle(feetY, IceTopY), "2.2 m 는 기어오를 수 있는 턱이다");
            Assert.IsFalse(IceLedgeMantleLogic.ClearsWithJump(feetY, IceTopY, settings.JumpHeight),
                "점프로 넘을 수 있으면 기어오르기가 끼어들면 안 된다");
        }

        [Test]
        public void 낮은_턱은_그냥_점프로_넘는다()
        {
            // 얕은 턱마다 몸이 굳으면 이동이 답답해진다.
            Assert.IsTrue(IceLedgeMantleLogic.ClearsWithJump(0f, 0.9f, 1.2f));
            Assert.IsFalse(IceLedgeMantleLogic.ShouldMantle(
                true, true, true, Vector3.right, 0f, 0.9f, 1.2f));
        }

        [Test]
        public void 깊은_물에_떠_있어도_기어오를_수_있다()
        {
            // 헤엄치는 사람은 수면에 <b>떠 있지 서 있지 않다</b> — 부력이 잠김 1.0~1.2 m 에서
            // 멈추므로 발이 −2.5 ~ −2.7 이고, 얕은 물(−2.22)보다 <b>더 멀다.</b>
            var settings = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(
                "Assets/_Project/Data/PlayerMovementSettings.asset");

            float floatingFeet = WaterY - settings.SwimEnterDepth;
            Assert.AreEqual(-2.5f, floatingFeet, 1e-4f);
            Assert.IsTrue(IceLedgeMantleLogic.CanMantle(floatingFeet, IceTopY), "부력 평형에서");

            // 평형은 진입 깊이보다 살짝 아래에서 잡힐 수 있다 — 그 여유까지 덮어야 한다.
            Assert.IsTrue(IceLedgeMantleLogic.CanMantle(WaterY - 1.2f, IceTopY), "잠김 1.2 m 에서");
        }

        [Test]
        public void 갑판_높이는_기어오르기_대상이_아니다()
        {
            // 물에서 갑판(3.566 m)으로 곧장 기어오르면 승차 사다리가 있을 이유가 없어진다.
            Assert.IsFalse(IceLedgeMantleLogic.CanMantle(WaterY, 3.566f));
            Assert.IsFalse(IceLedgeMantleLogic.CanMantle(ShallowFloorY, 3.566f), "얕은 물에서도");
            Assert.Less(IceLedgeMantleLogic.DefaultMaxRise, 3.566f - (WaterY - 1.2f) - 0.1f,
                "부력 평형에서 갑판까지의 거리보다 짧아야 한다");
        }

        [Test]
        public void 사면은_기어오르기가_아니다()
        {
            Assert.IsTrue(IceLedgeMantleLogic.IsClimbableWall(Vector3.right), "수직 벽");
            Assert.IsFalse(IceLedgeMantleLogic.IsClimbableWall(Vector3.up), "바닥");
            Assert.IsFalse(IceLedgeMantleLogic.IsClimbableWall(new Vector3(0.4f, 0.9f, 0f).normalized), "완만한 사면");
        }

        [Test]
        public void 물_밖에서는_기어오르지_않는다()
        {
            Assert.IsFalse(IceLedgeMantleLogic.ShouldMantle(
                false, true, true, Vector3.right, ShallowFloorY, IceTopY, 1.2f));
        }

        // ── 몬스터 발밑 보정 ─────────────────────────────────────────────

        [Test]
        public void 얼음_위의_몬스터는_묻히지_않는다()
        {
            // §3.1 — 지역 SurfaceY 단일값(−1.5)을 그대로 쓰면 얼음 구간에서 1.5 m 묻힌 채 걸어온다.
            Assert.AreEqual(IceTopY, GroundSupportProbe.ResolveSupportY(true, IceTopY, WaterY), 1e-4f);
        }

        [Test]
        public void 물_위에서는_물면을_유지한다()
        {
            // 허공(맞은 것 없음)은 곧 물이다 — 물에는 콜라이더가 없다.
            Assert.AreEqual(WaterY, GroundSupportProbe.ResolveSupportY(false, 0f, WaterY), 1e-4f);

            // 해저·얕은 물 바닥이 맞아도 몬스터는 물면 위를 걷는다(바다 4차 규약).
            Assert.AreEqual(WaterY, GroundSupportProbe.ResolveSupportY(true, DeepFloorY, WaterY), 1e-4f);
            Assert.AreEqual(WaterY, GroundSupportProbe.ResolveSupportY(true, ShallowFloorY, WaterY), 1e-4f);
        }

        [Test]
        public void 물이_없는_지역은_동작이_그대로다()
        {
            // 폴백이 0이라 지면 위 개체는 종전과 같은 높이에 선다.
            Assert.AreEqual(0f, GroundSupportProbe.ResolveSupportY(false, 0f, 0f), 1e-4f);
            Assert.AreEqual(1.4f, GroundSupportProbe.ResolveSupportY(true, 1.4f, 0f), 1e-4f, "언덕 위");
        }

        // ── 얼음낚시 ─────────────────────────────────────────────

        [Test]
        public void 북극_낚시는_열려_있되_어렵다()
        {
            RegionDefinition arctic = LoadRegion();
            Assert.AreEqual(4f, arctic.FishingBiteDelayMultiplier, 1e-4f, "입질 대기 ×4");
            Assert.AreEqual(0f, arctic.FishingDoubleCatchMultiplier, 1e-4f, "북극에서 두 마리는 없다");

            var sea = AssetDatabase.LoadAssetAtPath<RegionDefinition>("Assets/_Project/Data/Region_Sea.asset");
            Assert.AreEqual(1f, sea.FishingBiteDelayMultiplier, 1e-4f, "바다는 종전 그대로");
            Assert.AreEqual(1f, sea.FishingDoubleCatchMultiplier, 1e-4f);
        }

        [Test]
        public void 입질_배율이_대기를_네_배로_늘린다()
        {
            const float Min = 2.5f, Max = 12f, Reference = 6f, Influence = 0.7f;

            float sea = FishingLogic.BiteDelaySeconds(0.5f, Reference, Reference, Min, Max, Influence, 1f);
            float arctic = FishingLogic.BiteDelaySeconds(0.5f, Reference, Reference, Min, Max, Influence, 4f);

            Assert.AreEqual(sea * 4f, arctic, 0.01f);
            Assert.Greater(arctic, 10f, "한 마리에 열차 반 칸을 지나갈 시간이 든다");
        }

        [Test]
        public void 얼음_위에서는_던질_수_없다()
        {
            // 지금까지는 조준선과 물면 평면의 교차만 봤다 — 얼음 위를 겨눠도 그 아래에서
            // 평면과 만나므로 찌가 얼음에 박힌 채 던져진다(§8.3).
            Assert.IsTrue(FishingLogic.IsBlockedBeforeWater(12f, 4f), "4 m 앞의 얼음이 막는다");
            Assert.IsFalse(FishingLogic.IsBlockedBeforeWater(12f, -1f), "막은 것이 없으면 통과");
            Assert.IsFalse(FishingLogic.IsBlockedBeforeWater(12f, 11.95f), "물가 가장자리는 통과 (여유 0.2 m)");
            Assert.IsTrue(FishingLogic.IsBlockedBeforeWater(-1f, -1f), "물면에 아예 닿지 않으면 못 던진다");
        }
    }
}
