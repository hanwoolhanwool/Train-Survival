using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 파지 설정 <b>에셋 값</b>의 건전성 (1인칭 통합 시점 전환 계획 §1.4.1 · R2 · R3).
    /// 순수 로직이 아니라 데이터를 본다 — 자세는 사람이 눈으로 맞추지만, 눈으로는
    /// "팔이 닿는가"와 "팔꿈치가 어디로 굽는가"를 가늠하기 어려워 계기가 필요하다.
    ///
    /// <para>고정하는 계약 둘: <b>팔을 다 펴지 않고 닿는다</b> ·
    /// <b>팔꿈치 힌트가 어깨보다 아래에 있다</b>.</para>
    ///
    /// <para>둘째 항목은 1차 검증에서 실제로 터진 결함이다 — FP 힌트를 어깨보다 7 cm 위에
    /// 두는 바람에 IK가 팔꿈치를 들어올려 <b>팔이 말려 접혔다</b>. 원격 화면은 TP 프로파일을
    /// 쓰므로 멀쩡해 보여, 두 화면을 비교해도 원인이 드러나지 않았다.</para>
    ///
    /// <para><b>시야(화면 안에 보이는가)는 여기서 강제하지 않는다.</b> 통합 프로파일의 기준선은
    /// "원격 화면과 똑같은 자세"이고(§3.3 결정 재정립), 무기를 화면 쪽으로 올릴지는 그 다음
    /// 문제다. 시야 각도 계산은 <see cref="FirstPersonHoldMath"/>가 제공하며 뷰랩 계기가 쓴다.</para>
    /// </summary>
    public sealed class WeaponHoldSettingsAssetTests
    {
        private const string HoldAssetPath = "Assets/_Project/Data/WeaponHoldSettings.asset";
        private const string ViewAssetPath = "Assets/_Project/Data/PlayerViewSettings.asset";

        /// <summary>Girl 0.475 · Man 0.478 실측 (상완 0.261/0.245 + 전완 0.214/0.232).</summary>
        private const float ArmLength = 0.475f;

        /// <summary>팔 사용률 상한 — 넘으면 팔꿈치가 펴져 뻣뻣해진다.</summary>
        private const float MaxReachRatio = 0.9f;

        /// <summary>카메라 피벗의 루트 로컬 위치 — Player.prefab의 CameraRig/CameraPivot.</summary>
        private static readonly Vector3 CameraPivotLocal = new Vector3(0f, 1.6f, 0f);

        private static readonly HotbarItemType[] AimWeapons =
        {
            HotbarItemType.Revolver,
            HotbarItemType.Shotgun,
            HotbarItemType.Harpoon,
        };

        private static WeaponHoldSettings LoadHold()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WeaponHoldSettings>(HoldAssetPath);
            Assert.That(asset, Is.Not.Null, HoldAssetPath + " 를 찾지 못했다");
            return asset;
        }

        private static PlayerViewSettings LoadView()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PlayerViewSettings>(ViewAssetPath);
            Assert.That(asset, Is.Not.Null, ViewAssetPath + " 를 찾지 못했다");
            return asset;
        }

        /// <summary>피치 0에서 손이 카메라에게 어떻게 보이는지 — 계기 계산의 공통 앞부분.</summary>
        private static Vector3 CameraLocalOf(
            WeaponHoldSettings hold, PlayerViewSettings view, Vector3 handLocal)
        {
            Vector3 pivot = hold.GetAimPivotLocalPosition(PlayerViewMode.UnifiedFirstPerson);
            Vector3 hand = FirstPersonHoldMath.HoldTargetRootLocal(pivot, 0f, handLocal);
            Vector3 camera = FirstPersonHoldMath.CameraRootLocal(
                CameraPivotLocal, 0f, view.GetCameraLocalOffset(PlayerViewMode.UnifiedFirstPerson));
            return FirstPersonHoldMath.ToCameraLocal(hand, camera, 0f);
        }

        [Test]
        public void 조준_무기의_손은_카메라_앞쪽에_있다()
        {
            // 뒤(z ≤ 0)로 넘어가면 어느 각도로도 화면에 담을 수 없다 — 자세가 뒤집힌 신호다.
            WeaponHoldSettings hold = LoadHold();
            PlayerViewSettings view = LoadView();

            foreach (HotbarItemType item in AimWeapons)
            {
                WeaponHoldSettings.Entry entry;
                Assert.That(hold.TryGetEntry(item, out entry), Is.True, item + " 엔트리가 없다");

                WeaponHoldSettings.HoldProfile fp = entry.GetProfile(PlayerViewMode.UnifiedFirstPerson);
                Vector3 right = CameraLocalOf(hold, view, fp.RightHandLocalPosition);

                Assert.That(right.z, Is.GreaterThan(0f), item + " 오른손이 카메라 뒤에 있다");
            }
        }

        [Test]
        public void 통합_프로파일의_조준_무기는_팔을_다_펴지_않고_닿는다()
        {
            WeaponHoldSettings hold = LoadHold();
            Vector3 pivot = hold.GetAimPivotLocalPosition(PlayerViewMode.UnifiedFirstPerson);

            foreach (HotbarItemType item in AimWeapons)
            {
                WeaponHoldSettings.Entry entry;
                Assert.That(hold.TryGetEntry(item, out entry), Is.True);

                WeaponHoldSettings.HoldProfile fp = entry.GetProfile(PlayerViewMode.UnifiedFirstPerson);
                Vector3 rightHand = FirstPersonHoldMath.HoldTargetRootLocal(
                    pivot, 0f, fp.RightHandLocalPosition);

                Assert.That(
                    FirstPersonHoldMath.ReachRatio(hold.RightShoulderRestLocalPosition, rightHand, ArmLength),
                    Is.LessThan(MaxReachRatio),
                    item + " 오른팔이 너무 펴진다");

                if (!entry.TwoHanded)
                {
                    continue;
                }

                Vector3 leftHand = FirstPersonHoldMath.HoldTargetRootLocal(
                    pivot, 0f, fp.LeftHandLocalPosition);

                // 양손 무기의 왼손이 가장 빠듯하다 (계획 R3).
                Assert.That(
                    FirstPersonHoldMath.ReachRatio(hold.LeftShoulderRestLocalPosition, leftHand, ArmLength),
                    Is.LessThan(MaxReachRatio),
                    item + " 왼팔이 너무 펴진다");
            }
        }

        [Test]
        public void 팔꿈치_힌트는_두_모드_모두_어깨보다_아래에_있다()
        {
            WeaponHoldSettings hold = LoadHold();

            foreach (PlayerViewMode mode in new[] { PlayerViewMode.SplitFpTp, PlayerViewMode.UnifiedFirstPerson })
            {
                Vector3 pivot = hold.GetAimPivotLocalPosition(mode);

                foreach (HotbarItemType item in AimWeapons)
                {
                    WeaponHoldSettings.Entry entry;
                    Assert.That(hold.TryGetEntry(item, out entry), Is.True);

                    WeaponHoldSettings.HoldProfile profile = entry.GetProfile(mode);
                    if (profile.RightElbowHintWeight > 0f)
                    {
                        Vector3 hint = FirstPersonHoldMath.HoldTargetRootLocal(
                            pivot, 0f, profile.RightElbowHintLocalPosition);
                        Assert.That(hint.y, Is.LessThan(hold.RightShoulderRestLocalPosition.y),
                            mode + " · " + item + " 오른 팔꿈치 힌트가 어깨보다 위다 — 팔이 말려 접힌다");
                    }

                    if (profile.LeftElbowHintWeight > 0f)
                    {
                        Vector3 hint = FirstPersonHoldMath.HoldTargetRootLocal(
                            pivot, 0f, profile.LeftElbowHintLocalPosition);
                        Assert.That(hint.y, Is.LessThan(hold.LeftShoulderRestLocalPosition.y),
                            mode + " · " + item + " 왼 팔꿈치 힌트가 어깨보다 위다");
                    }
                }
            }
        }
    }
}
