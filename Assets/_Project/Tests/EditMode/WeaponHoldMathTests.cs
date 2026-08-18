using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 무기 손 파지 순수 로직 검증 (무기 손 파지 계획 §6).
    /// 고정하는 계약은 셋이다: <b>조준 자세 무기만 IK 가중치가 켜진다</b> ·
    /// <b>가중치 블렌드는 오버슈트 없이 수렴한다</b> ·
    /// <b>홀드 타깃은 조준 피벗을 중심으로 피치를 따라 오르내린다</b>.
    /// </summary>
    public sealed class WeaponHoldMathTests
    {
        private const float HalfLife = 0.1f;

        private static readonly Vector3 PivotLocal = new Vector3(0f, 1.25f, 0f);

        // ── HeldItem → 가중치 매핑 표 (계획 §2.3 가중치 규칙) ─────────────

        [Test]
        public void 조준_자세_무기를_들면_가중치가_켜진다()
        {
            Assert.That(WeaponHoldMath.TargetWeight(held: true, aimPose: true, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void 소켓_전용_무기는_가중치_0이다()
        {
            // 망치·근접 — 손에 쥐되 팔은 내린 자세.
            Assert.That(WeaponHoldMath.TargetWeight(held: true, aimPose: false, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void 빈손이면_조준_자세_설정과_무관하게_가중치_0이다()
        {
            // HeldItem = None (UI 열림·사망 포함 — 이미 None으로 복제됨).
            Assert.That(WeaponHoldMath.TargetWeight(held: false, aimPose: true, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void 데이터_가중치는_0과_1_사이로_잘린다()
        {
            Assert.That(WeaponHoldMath.TargetWeight(held: true, aimPose: true, 1.5f), Is.EqualTo(1f));
            Assert.That(WeaponHoldMath.TargetWeight(held: true, aimPose: true, -0.5f), Is.EqualTo(0f));
        }

        // ── 가중치 블렌드 (수렴·오버슈트 없음) ───────────────────────────

        [Test]
        public void 블렌드는_목표로_수렴한다()
        {
            float weight = 0f;
            for (int i = 0; i < 120; i++)
            {
                weight = WeaponHoldMath.StepWeight(weight, 1f, HalfLife, 1f / 60f);
            }

            Assert.That(weight, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 블렌드는_전환_중_오버슈트가_없다()
        {
            float weight = 0f;
            for (int i = 0; i < 300; i++)
            {
                weight = WeaponHoldMath.StepWeight(weight, 1f, HalfLife, 1f / 60f);
                Assert.That(weight, Is.LessThanOrEqualTo(1f));
            }

            for (int i = 0; i < 300; i++)
            {
                weight = WeaponHoldMath.StepWeight(weight, 0f, HalfLife, 1f / 60f);
                Assert.That(weight, Is.GreaterThanOrEqualTo(0f));
            }
        }

        [Test]
        public void 반감기_한_번에_목표까지_절반을_간다()
        {
            float weight = WeaponHoldMath.StepWeight(0f, 1f, HalfLife, HalfLife);

            Assert.That(weight, Is.EqualTo(0.5f).Within(0.001f));
        }

        // ── Hold 레이어·포즈 합성 (품질 업그레이드 계획 C축 §6) ──────────

        [Test]
        public void 포즈_카테고리가_있는_무기를_들면_레이어_가중치가_켜진다()
        {
            Assert.That(
                WeaponHoldMath.TargetLayerWeight(held: true, WeaponHoldPose.OneHandAim, 1f),
                Is.EqualTo(1f));
        }

        [Test]
        public void 카테고리_없는_무기와_빈손은_레이어_가중치_0이다()
        {
            Assert.That(WeaponHoldMath.TargetLayerWeight(true, WeaponHoldPose.None, 1f), Is.EqualTo(0f));
            Assert.That(WeaponHoldMath.TargetLayerWeight(false, WeaponHoldPose.TwoHandAim, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void 클립_반입_전에는_레이어_가중치가_0으로_유지된다()
        {
            // 설정의 레이어 가중치가 0이면 카테고리가 있어도 상체를 덮어쓰지 않는다
            // (모션 없는 스테이트가 바인드 포즈로 굳는 것을 막는 안전장치).
            Assert.That(WeaponHoldMath.TargetLayerWeight(true, WeaponHoldPose.OneHandAim, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void 포즈_잔여_가중치는_IK를_그만큼_비운다()
        {
            Assert.That(WeaponHoldMath.BlendIkWithPose(1f, 0.4f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(WeaponHoldMath.BlendIkWithPose(0.5f, 0.4f), Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void 잔여_가중치_1이면_IK가_단독으로_동작한다()
        {
            // 클립 반입 전 기본값 — A안과 동일한 IK 단독 경로임을 고정한다.
            Assert.That(WeaponHoldMath.BlendIkWithPose(1f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void 합성_가중치는_각_항의_상한을_넘지_않는다()
        {
            for (float ik = 0f; ik <= 1f; ik += 0.1f)
            {
                for (float residual = 0f; residual <= 1f; residual += 0.1f)
                {
                    float blended = WeaponHoldMath.BlendIkWithPose(ik, residual);
                    Assert.That(blended, Is.LessThanOrEqualTo(Mathf.Min(ik, residual) + 0.0001f));
                    Assert.That(blended, Is.GreaterThanOrEqualTo(0f));
                }
            }
        }

        // ── 홀드 타깃 산출 (피치 경계 포함) ──────────────────────────────

        [Test]
        public void 피치_0에서는_타깃이_피벗_정면_로컬_좌표_그대로다()
        {
            Vector3 handLocal = new Vector3(0.2f, 0f, 0.4f);

            WeaponHoldMath.ComputeHoldPose(
                Vector3.zero, Quaternion.identity, PivotLocal, 0f,
                handLocal, Quaternion.identity,
                out Vector3 position, out Quaternion rotation);

            Assert.That(position.x, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.LessThan(0.001f));
        }

        [Test]
        public void 내려다보면_타깃이_피벗보다_내려간다()
        {
            // +피치 = 내려다봄 (PlayerAnimationMath.SignedPitch 규약).
            Vector3 handLocal = new Vector3(0f, 0f, 0.4f);

            WeaponHoldMath.ComputeHoldPose(
                Vector3.zero, Quaternion.identity, PivotLocal, 89f,
                handLocal, Quaternion.identity,
                out Vector3 lowered, out _);
            WeaponHoldMath.ComputeHoldPose(
                Vector3.zero, Quaternion.identity, PivotLocal, -89f,
                handLocal, Quaternion.identity,
                out Vector3 raised, out _);

            Assert.That(lowered.y, Is.LessThan(PivotLocal.y));
            Assert.That(raised.y, Is.GreaterThan(PivotLocal.y));
        }

        [Test]
        public void 피치_최대_경계에서_타깃은_피벗_수직선상에_있다()
        {
            // ±90°에서 전방 오프셋이 온전히 수직으로 눕는다 — 피벗 중심 회전의 경계값.
            Vector3 handLocal = new Vector3(0f, 0f, 0.4f);

            WeaponHoldMath.ComputeHoldPose(
                Vector3.zero, Quaternion.identity, PivotLocal, 90f,
                handLocal, Quaternion.identity,
                out Vector3 down, out _);

            Assert.That(down.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(down.y, Is.EqualTo(PivotLocal.y - 0.4f).Within(0.0001f));
        }

        [Test]
        public void 루트_요_회전을_따라_타깃이_함께_돈다()
        {
            // 몸이 +X를 보면 전방 오프셋도 +X로 돈다 — 루트 회전 합성 검증.
            Vector3 handLocal = new Vector3(0f, 0f, 0.4f);
            Quaternion faceRight = Quaternion.Euler(0f, 90f, 0f);

            WeaponHoldMath.ComputeHoldPose(
                Vector3.zero, faceRight, PivotLocal, 0f,
                handLocal, Quaternion.identity,
                out Vector3 position, out Quaternion rotation);

            Assert.That(position.x, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(position.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Quaternion.Angle(rotation, faceRight), Is.LessThan(0.001f));
        }

        // ── 손목 펴기 (포즈 편집 계획 E5) ───────────────────────────────

        [Test]
        public void 손목_펴기는_손끝을_전완_방향에_정렬한다()
        {
            // 이 계약이 깨지면 손목이 꺾인다 — 롤·굽힘과 무관하게 손끝은 전완을 따라간다.
            Vector3 forearm = new Vector3(0.299f, 0.196f, 0.934f).normalized;

            for (float roll = -180f; roll <= 180f; roll += 45f)
            {
                Quaternion wrist = WeaponHoldMath.StraightWristRotation(forearm, roll, 0f, mirrored: false);

                Assert.That(Vector3.Angle(wrist * Vector3.up, forearm), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void 롤_0이면_손등이_하늘을_본다()
        {
            Vector3 forearm = Vector3.forward;

            Quaternion wrist = WeaponHoldMath.StraightWristRotation(forearm, 0f, 0f, mirrored: false);

            Assert.That(Vector3.Angle(wrist * Vector3.forward, Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void 왼손은_롤_부호가_뒤집힌다()
        {
            // 좌우 데이터를 한 벌로 쓰기 위한 미러 규약 (LSP — 모델별 분기 금지).
            Vector3 forearm = new Vector3(0.3f, 0.1f, 0.95f).normalized;

            Quaternion right = WeaponHoldMath.StraightWristRotation(forearm, 90f, 0f, mirrored: false);
            Quaternion left = WeaponHoldMath.StraightWristRotation(forearm, -90f, 0f, mirrored: true);

            Assert.That(Quaternion.Angle(right, left), Is.LessThan(0.01f));
        }

        [Test]
        public void 굽힘은_손끝을_그만큼_젖힌다()
        {
            Vector3 forearm = Vector3.forward;

            Quaternion bent = WeaponHoldMath.StraightWristRotation(forearm, 90f, 25f, mirrored: false);

            Assert.That(Vector3.Angle(bent * Vector3.up, forearm), Is.EqualTo(25f).Within(0.01f));
        }

        [Test]
        public void 전완이_수직이거나_0이어도_회전이_무너지지_않는다()
        {
            // 위를 겨눠 전완이 세로로 서면 '하늘' 기준이 사라진다 — 옆 축으로 갈아타야 한다.
            Quaternion vertical = WeaponHoldMath.StraightWristRotation(Vector3.up, 90f, 0f, mirrored: false);
            Quaternion zero = WeaponHoldMath.StraightWristRotation(Vector3.zero, 90f, 0f, mirrored: false);

            Assert.That(Vector3.Angle(vertical * Vector3.up, Vector3.up), Is.LessThan(0.01f));
            Assert.That(float.IsNaN(zero.x) || float.IsNaN(zero.y) || float.IsNaN(zero.z), Is.False);
        }

        // ── 팔꿈치 힌트 (포즈 편집 계획 E3) ──────────────────────────────

        [Test]
        public void 엘보_힌트는_손_타깃과_같은_좌표계를_쓴다()
        {
            // 힌트가 다른 변환을 타면 피치 구간에서 팔꿈치만 따로 논다 — 동일 변환임을 고정한다.
            Vector3 local = new Vector3(0.38f, -0.3f, 0.2f);
            Vector3 rootPosition = new Vector3(1f, 0f, -2f);
            Quaternion rootRotation = Quaternion.Euler(0f, 35f, 0f);

            for (float pitch = -80f; pitch <= 80f; pitch += 20f)
            {
                WeaponHoldMath.ComputeHoldPose(
                    rootPosition, rootRotation, PivotLocal, pitch, local, Quaternion.identity,
                    out Vector3 handPosition, out _);
                Vector3 hintPosition = WeaponHoldMath.ComputeHoldPosition(
                    rootPosition, rootRotation, PivotLocal, pitch, local);

                Assert.That(Vector3.Distance(hintPosition, handPosition), Is.LessThan(0.0001f));
            }
        }

        [Test]
        public void 엘보_힌트는_피벗을_중심으로_피치를_따라_돈다()
        {
            // 팔꿈치가 몸에 고정되지 않고 겨눔과 함께 오르내리는지 — 손과 같은 규약.
            Vector3 local = new Vector3(0.38f, -0.3f, 0.2f);

            Vector3 level = WeaponHoldMath.ComputeHoldPosition(
                Vector3.zero, Quaternion.identity, PivotLocal, 0f, local);
            Vector3 raised = WeaponHoldMath.ComputeHoldPosition(
                Vector3.zero, Quaternion.identity, PivotLocal, -60f, local);

            Assert.That(level.y, Is.EqualTo(PivotLocal.y - 0.3f).Within(0.0001f));
            Assert.That(raised.y, Is.GreaterThan(level.y));
            Assert.That(raised.x, Is.EqualTo(local.x).Within(0.0001f));
        }

        [Test]
        public void 힌트_가중치도_포즈_잔여_비율과_같은_규칙으로_합성된다()
        {
            // 파지 블렌드 아웃 중 힌트만 남아 팔꿈치가 굳는 일이 없도록 손 가중치와 곱해 쓴다.
            Assert.That(WeaponHoldMath.BlendIkWithPose(0f, 1f), Is.EqualTo(0f));
            Assert.That(WeaponHoldMath.BlendIkWithPose(0.5f, 1f), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void 루트_위치_이동은_타깃을_평행이동시킨다()
        {
            Vector3 rootPosition = new Vector3(3f, 4f, 13.5f);

            WeaponHoldMath.ComputeHoldPose(
                rootPosition, Quaternion.identity, PivotLocal, 0f,
                Vector3.zero, Quaternion.identity,
                out Vector3 position, out _);

            Assert.That(position, Is.EqualTo(rootPosition + PivotLocal));
        }
    }
}
