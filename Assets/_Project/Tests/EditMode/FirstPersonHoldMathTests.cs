using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 통합 1인칭 파지 계기 검증 (1인칭 통합 시점 전환 계획 §1.4 · 기술 확정 ⑪).
    /// 고정하는 계약은 넷이다: <b>화면 각도를 카메라 기준으로 낸다</b> ·
    /// <b>수평 반각은 화면 비율만큼 넓다</b> · <b>팔 사용률이 도달 가능 여부를 말한다</b> ·
    /// <b>조준 피벗을 눈높이에 두면 무기의 화면 위치가 피치와 무관해진다</b>(§3.3의 근거).
    /// </summary>
    public sealed class FirstPersonHoldMathTests
    {
        private const float Fov = 60f;
        private const float Aspect = 16f / 9f;
        private const float ArmLength = 0.475f;

        private static readonly Vector3 EyeLocal = new Vector3(0f, 1.6f, 0f);
        private static readonly Vector3 RightShoulderLocal = new Vector3(0.16f, 1.091f, 0.141f);

        // ── 화면 각도 ─────────────────────────────────────────────────────

        [Test]
        public void 정면은_수직_수평_모두_0도다()
        {
            Vector3 forward = new Vector3(0f, 0f, 1f);

            Assert.That(FirstPersonHoldMath.VerticalDownDegrees(forward), Is.EqualTo(0f).Within(0.001f));
            Assert.That(FirstPersonHoldMath.HorizontalDegrees(forward), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void 아래로_내려간_만큼_수직각이_양수다()
        {
            // 앞 1 m · 아래 1 m → 45° 아래.
            Assert.That(
                FirstPersonHoldMath.VerticalDownDegrees(new Vector3(0f, -1f, 1f)),
                Is.EqualTo(45f).Within(0.01f));
        }

        [Test]
        public void 오른쪽으로_벗어난_만큼_수평각이_양수다()
        {
            Assert.That(
                FirstPersonHoldMath.HorizontalDegrees(new Vector3(1f, 0f, 1f)),
                Is.EqualTo(45f).Within(0.01f));
        }

        // ── 시야 판정 ─────────────────────────────────────────────────────

        [Test]
        public void 수직_반각은_시야각의_절반이다()
        {
            Assert.That(FirstPersonHoldMath.VerticalHalfFovDegrees(Fov), Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void 수평_반각은_화면_비율만큼_넓다()
        {
            // 16:9에서 FOV 60이면 약 45.8° — 좌우 여유가 상하보다 훨씬 크다.
            float half = FirstPersonHoldMath.HorizontalHalfFovDegrees(Fov, Aspect);

            Assert.That(half, Is.EqualTo(45.8f).Within(0.2f));
            Assert.That(half, Is.GreaterThan(FirstPersonHoldMath.VerticalHalfFovDegrees(Fov)));
        }

        [Test]
        public void 하단_경계를_넘으면_화면_밖이다()
        {
            // 수직 31° 아래 — FOV 60의 하단 경계(30°) 바로 바깥.
            Vector3 outside = new Vector3(0f, -Mathf.Tan(31f * Mathf.Deg2Rad), 1f);

            Assert.That(FirstPersonHoldMath.IsWithinFov(outside, Fov, Aspect), Is.False);
        }

        [Test]
        public void 하단_경계_안쪽은_화면_안이다()
        {
            Vector3 inside = new Vector3(0f, -Mathf.Tan(29f * Mathf.Deg2Rad), 1f);

            Assert.That(FirstPersonHoldMath.IsWithinFov(inside, Fov, Aspect), Is.True);
        }

        [Test]
        public void 카메라_뒤는_각도와_무관하게_화면_밖이다()
        {
            Assert.That(FirstPersonHoldMath.IsWithinFov(new Vector3(0f, 0f, -0.5f), Fov, Aspect), Is.False);
        }

        // ── 팔 도달 ───────────────────────────────────────────────────────

        [Test]
        public void 팔_길이만큼_떨어지면_사용률이_1이다()
        {
            Vector3 hand = RightShoulderLocal + new Vector3(0f, 0f, ArmLength);

            Assert.That(
                FirstPersonHoldMath.ReachRatio(RightShoulderLocal, hand, ArmLength),
                Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 팔_길이를_넘으면_사용률이_1을_넘는다()
        {
            Vector3 tooFar = RightShoulderLocal + new Vector3(0f, 0f, ArmLength * 1.5f);

            Assert.That(
                FirstPersonHoldMath.ReachRatio(RightShoulderLocal, tooFar, ArmLength),
                Is.GreaterThan(1f));
        }

        // ── 계획 §1.3·§1.4의 실측 수치 회귀 ────────────────────────────────

        [Test]
        public void 분리_모드_자세는_화면_아래로_크게_벗어난다()
        {
            // 리볼버 TP 프로파일 — 조준 피벗 1.08(어깨 높이) 기준 (0.378, -0.107, 0.448).
            Vector3 hand = FirstPersonHoldMath.HoldTargetRootLocal(
                new Vector3(0f, 1.08f, 0f), 0f, new Vector3(0.378f, -0.107f, 0.448f));
            Vector3 cameraLocal = FirstPersonHoldMath.ToCameraLocal(hand, EyeLocal, 0f);

            // 계획 §1.3 표: 46.9° 아래 — FOV 60의 하단 경계 30°를 크게 넘는다.
            Assert.That(FirstPersonHoldMath.VerticalDownDegrees(cameraLocal), Is.EqualTo(46.9f).Within(0.3f));
            Assert.That(FirstPersonHoldMath.IsWithinFov(cameraLocal, Fov, Aspect), Is.False);
        }

        [Test]
        public void 통합_모드_초기값은_화면_안이면서_팔이_닿는다()
        {
            // 리볼버 FP 프로파일 — 조준 피벗 1.6(눈높이) 기준 (0.22, -0.24, 0.40).
            Vector3 hand = FirstPersonHoldMath.HoldTargetRootLocal(
                EyeLocal, 0f, new Vector3(0.22f, -0.24f, 0.4f));
            Vector3 cameraLocal = FirstPersonHoldMath.ToCameraLocal(hand, EyeLocal, 0f);

            Assert.That(FirstPersonHoldMath.IsWithinFov(cameraLocal, Fov, Aspect), Is.True,
                "FP 초기값이 화면 밖이면 §1.4의 산출이 깨진 것이다");

            float reach = FirstPersonHoldMath.ReachRatio(RightShoulderLocal, hand, ArmLength);
            Assert.That(reach, Is.LessThan(0.85f),
                "팔 사용률 85 %를 넘으면 팔꿈치가 펴져 부자연스럽다 (§1.4)");
        }

        // ── §3.3의 근거: 눈높이 피벗이면 화면 위치가 피치와 무관하다 ────────

        [Test]
        public void 조준_피벗이_눈높이면_무기의_화면_위치가_피치와_무관하다()
        {
            Vector3 handLocal = new Vector3(0.22f, -0.24f, 0.4f);
            Vector3 atZero = FirstPersonHoldMath.ToCameraLocal(
                FirstPersonHoldMath.HoldTargetRootLocal(EyeLocal, 0f, handLocal), EyeLocal, 0f);

            foreach (float pitch in new[] { -85f, -40f, 40f, 85f })
            {
                Vector3 camera = FirstPersonHoldMath.CameraRootLocal(EyeLocal, pitch, Vector3.zero);
                Vector3 atPitch = FirstPersonHoldMath.ToCameraLocal(
                    FirstPersonHoldMath.HoldTargetRootLocal(EyeLocal, pitch, handLocal), camera, pitch);

                Assert.That(Vector3.Distance(atZero, atPitch), Is.LessThan(0.001f),
                    $"피치 {pitch}°에서 화면 위치가 달라졌다 — §3.3의 눈높이 피벗 근거가 깨진다");
            }
        }

        [Test]
        public void 눈높이_피벗은_올려다볼때_팔_도달을_크게_넘는다()
        {
            // 화면 고정의 대가 — 회전 중심이 높을수록 위를 볼 때 손이 더 멀리 올라간다.
            // 피치 -85°에서 어깨~손이 0.90 m로 팔 길이(0.475)의 189 %다 (계획 R2).
            Vector3 hand = FirstPersonHoldMath.HoldTargetRootLocal(
                EyeLocal, -85f, new Vector3(0.22f, -0.24f, 0.4f));

            float reach = FirstPersonHoldMath.ReachRatio(RightShoulderLocal, hand, ArmLength);

            Assert.That(reach, Is.GreaterThan(1.5f),
                "극단 피치에서는 반경 클램프나 피치 추종 축소가 필요하다 (계획 R2)");
        }

        [Test]
        public void 조준_피벗이_어깨_높이면_위를_볼때_손이_카메라_옆으로_돌아간다()
        {
            Vector3 pivot = new Vector3(0f, 1.08f, 0f);
            Vector3 handLocal = new Vector3(0.378f, -0.107f, 0.448f);

            Vector3 atZero = FirstPersonHoldMath.ToCameraLocal(
                FirstPersonHoldMath.HoldTargetRootLocal(pivot, 0f, handLocal), EyeLocal, 0f);

            // 피벗(1.08)이 카메라(1.6)보다 낮아, 위를 보면 손이 카메라를 <b>스쳐 지나</b> 옆으로 온다.
            // 화면 아래로 밀리는 것이 아니라 깊이(z)가 무너져 각도와 무관하게 시야를 벗어난다.
            Vector3 cameraUp = FirstPersonHoldMath.CameraRootLocal(EyeLocal, -60f, Vector3.zero);
            Vector3 atUp = FirstPersonHoldMath.ToCameraLocal(
                FirstPersonHoldMath.HoldTargetRootLocal(pivot, -60f, handLocal), cameraUp, -60f);

            Assert.That(Vector3.Distance(atZero, atUp), Is.GreaterThan(0.1f),
                "어깨 높이 피벗은 피치에 따라 화면상 무기 위치가 흔들린다 — 그래서 통합 모드는 눈높이를 쓴다");
            Assert.That(atUp.z, Is.LessThan(atZero.z * 0.5f),
                "위를 보면 손의 화면 깊이가 무너진다");
            Assert.That(FirstPersonHoldMath.IsWithinFov(atUp, Fov, Aspect), Is.False);
        }
    }
}
