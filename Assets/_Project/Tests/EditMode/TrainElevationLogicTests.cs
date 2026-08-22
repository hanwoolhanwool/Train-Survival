using Game.Gameplay.Train;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 열차·궤도 높이 QA 토글 검증 (열차 높이 스펙 — docs/specs/world/train-elevation.md).
    /// 단계는 "현재 → 아래 → 더 아래 → 현재" 순환이고, 각 단계 값은 기준 배치에 더할 오프셋이다.
    /// <b>핵심 불변</b>: 갑판·레일·바퀴가 같은 오프셋 하나를 쓰므로 어느 단계에서도 상대 높이가 보존된다 —
    /// 이것이 높이를 바꿔도 건설·콜라이더가 어긋나지 않는 근거다.
    /// </summary>
    public sealed class TrainElevationLogicTests
    {
        // 씬·에셋에 굳어 있는 기준 배치 (train-art-layout.md §7).
        private const float BaseDeckHeight = 3.566f;
        private const float BaseTrainRootY = 0.916f;
        private const float BaseRailRootY = -0.5f;
        private const float RailLocalTop = 1.476f;

        private static readonly float[] Steps = { 0f, -0.3f, -0.6f };

        [Test]
        public void 단계는_마지막_다음에_기준_높이로_돌아온다()
        {
            Assert.That(TrainElevationLogic.NextStep(0, 3), Is.EqualTo(1), "현재 → 아래");
            Assert.That(TrainElevationLogic.NextStep(1, 3), Is.EqualTo(2), "아래 → 더 아래");
            Assert.That(TrainElevationLogic.NextStep(2, 3), Is.EqualTo(0), "더 아래 → 현재(순환)");
        }

        [Test]
        public void 범위_밖_단계는_순환하지_않고_잘라_낸다()
        {
            Assert.That(TrainElevationLogic.NormalizeStep(-1, 3), Is.EqualTo(0), "음수는 기준 높이");
            Assert.That(TrainElevationLogic.NormalizeStep(7, 3), Is.EqualTo(2), "초과는 마지막 단계");
            Assert.That(TrainElevationLogic.NormalizeStep(1, 0), Is.EqualTo(0), "단계가 없으면 0");
            Assert.That(TrainElevationLogic.NextStep(0, 0), Is.EqualTo(0), "단계가 없으면 순환도 없다");
        }

        [Test]
        public void 단계_목록이_비면_기준_높이를_쓴다()
        {
            Assert.That(TrainElevationLogic.ResolveOffset(null, 1), Is.EqualTo(0f), "미배선 방어");
            Assert.That(TrainElevationLogic.ResolveOffset(new float[0], 1), Is.EqualTo(0f));
            Assert.That(TrainElevationLogic.ResolveOffset(Steps, 5), Is.EqualTo(-0.6f).Within(0.0001f), "범위 밖은 마지막 단계");
        }

        [Test]
        public void 기본_세_단계는_스펙_수치대로_내려간다()
        {
            // 갑판 3.566 → 3.266 → 2.966 / 레일 상면 0.976 → 0.676 → 0.376.
            float[] expectedDeck = { 3.566f, 3.266f, 2.966f };
            float[] expectedRailTop = { 0.976f, 0.676f, 0.376f };

            for (int step = 0; step < Steps.Length; step++)
            {
                float offset = TrainElevationLogic.ResolveOffset(Steps, step);

                Assert.That(
                    TrainElevationLogic.ResolveElevatedY(BaseDeckHeight, offset),
                    Is.EqualTo(expectedDeck[step]).Within(0.001f),
                    $"단계 {step} 갑판 높이");

                Assert.That(
                    TrainElevationLogic.ResolveElevatedY(BaseRailRootY, offset) + RailLocalTop,
                    Is.EqualTo(expectedRailTop[step]).Within(0.001f),
                    $"단계 {step} 레일 상면");
            }
        }

        [Test]
        public void 어느_단계에서도_바퀴는_레일_위에_얹히고_갑판까지의_거리도_그대로다()
        {
            // 기준 배치의 관계: 바퀴 접지(열차 루트 + 0.06)가 레일 상면과 같고, 갑판은 그보다 2.59 위다.
            const float wheelContactLocalY = 0.06f;
            float baseWheelY = BaseTrainRootY + wheelContactLocalY;
            float baseRailTop = BaseRailRootY + RailLocalTop;
            float baseDeckAboveRail = BaseDeckHeight - baseRailTop;

            foreach (float offset in Steps)
            {
                float wheelY = TrainElevationLogic.ResolveElevatedY(BaseTrainRootY, offset) + wheelContactLocalY;
                float railTop = TrainElevationLogic.ResolveElevatedY(BaseRailRootY, offset) + RailLocalTop;
                float deck = TrainElevationLogic.ResolveElevatedY(BaseDeckHeight, offset);

                Assert.That(wheelY, Is.EqualTo(railTop).Within(0.001f),
                    $"오프셋 {offset}: 바퀴가 레일에서 뜨거나 파묻히면 안 된다");
                Assert.That(deck - railTop, Is.EqualTo(baseDeckAboveRail).Within(0.001f),
                    $"오프셋 {offset}: 갑판까지의 거리가 변하면 건설·착지 판정이 어긋난다");
                Assert.That(baseWheelY - wheelY, Is.EqualTo(-offset).Within(0.001f),
                    $"오프셋 {offset}: 내려간 양이 오프셋과 같아야 한다");
            }
        }

        [Test]
        public void 갑판_기준선은_오프셋을_반영하고_되돌리면_원래대로다()
        {
            var settings = ScriptableObject.CreateInstance<TrainLayoutSettings>();
            try
            {
                float baseDeck = settings.BaseDeckHeight;
                float baseSpawnY = settings.GetSpawnPosition(0).y;

                settings.SetElevationOffset(-0.6f);
                Assert.That(settings.DeckHeight, Is.EqualTo(baseDeck - 0.6f).Within(0.0001f), "갑판 판정이 함께 내려간다");
                Assert.That(settings.ElevationOffset, Is.EqualTo(-0.6f).Within(0.0001f));
                Assert.That(settings.BaseDeckHeight, Is.EqualTo(baseDeck).Within(0.0001f), "에셋 기준값은 손대지 않는다");
                Assert.That(settings.GetSpawnPosition(0).y, Is.EqualTo(baseSpawnY - 0.6f).Within(0.0001f),
                    "스폰 지점도 갑판을 따라 내려간다 — 안 따라오면 공중에서 떨어진다");
                Assert.That(settings.RespawnPosition.y, Is.EqualTo(baseDeck - 0.6f + 1f).Within(0.0001f), "부활 지점도 같다");

                settings.SetElevationOffset(0f);
                Assert.That(settings.DeckHeight, Is.EqualTo(baseDeck).Within(0.0001f), "기준 단계로 돌아오면 원래 높이");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void 열차_하부_즉사_존도_함께_내려가되_지면_밑으로는_안_간다()
        {
            var settings = ScriptableObject.CreateInstance<TrainLayoutSettings>();
            try
            {
                float baseKill = settings.WheelKillHeight;
                Assume.That(baseKill, Is.GreaterThan(0f), "즉사 존이 켜져 있는 기본값 전제");

                settings.SetElevationOffset(-0.6f);
                Assert.That(settings.WheelKillHeight, Is.EqualTo(baseKill - 0.6f).Within(0.0001f),
                    "바퀴 밑 공간이 같이 내려온다");

                settings.SetElevationOffset(-99f);
                Assert.That(settings.WheelKillHeight, Is.EqualTo(0f), "0 밑으로 내려가 존이 뒤집히지 않는다");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
