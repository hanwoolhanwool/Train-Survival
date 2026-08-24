using Game.Core.Pooling;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 발사 연출의 단일 구현 (M5 8차 — 시드 중계, M7 4차에서 거치 무기와 공유하도록 추출).
    /// <b>판정 무변</b>: 시드에서 펠릿 궤적을 재계산해 트레이서와 탄착을 로컬 재생할 뿐이다.
    /// 쏜 사람과 원격 피어가 같은 함수·같은 시드를 쓰므로 같은 산탄 패턴이 보인다
    /// (좌표 배열 전송 없음 — 대역폭 불변).
    /// <para>
    /// 개인 화기(<see cref="GunController"/>)와 거치 무기가 이 함수를 함께 쓴다. 다른 것은
    /// 총구 위치와 무시할 root뿐이다 — 개인 화기는 자기 몸을, 거치 무기는 <b>열차 전체</b>를
    /// 무시한다(자기 열차를 쏘지 않는다).
    /// </para>
    /// </summary>
    public static class WeaponFireCosmetics
    {
        /// <summary>
        /// 한 발사의 연출을 재생한다.
        /// </summary>
        /// <param name="settings">트레이서·탄착·펠릿 수·산탄 각의 출처.</param>
        /// <param name="muzzlePosition">트레이서가 나가는 지점 (판정 원점과 다를 수 있다).</param>
        /// <param name="aimOrigin">궤적 재계산의 원점 — 판정에 쓴 값 그대로여야 궤적이 일치한다.</param>
        /// <param name="aimForward">조준 방향.</param>
        /// <param name="seed">발사 시드 — 판정과 연출이 같은 수열을 쓴다.</param>
        /// <param name="ignoreRoot">레이가 무시할 root (자기 몸 또는 자기 열차).</param>
        public static void Play(
            GunSettings settings, Vector3 muzzlePosition, Vector3 aimOrigin, Vector3 aimForward,
            uint seed, Transform ignoreRoot)
        {
            if (settings == null)
            {
                return;
            }

            int pellets = Mathf.Max(1, settings.PelletCount);
            uint state = seed;

            for (int p = 0; p < pellets; p++)
            {
                Vector3 direction = WeaponSpreadMath.ApplySpreadSeeded(
                    aimForward, settings.SpreadAngle, ref state);
                bool hit = WeaponRaycast.TryGetClosestHit(
                    aimOrigin, direction, settings.MaxRange, ignoreRoot, out RaycastHit hitInfo);
                Vector3 end = hit ? hitInfo.point : aimOrigin + direction * settings.MaxRange;

                if (settings.TracerPrefab != null)
                {
                    TracerView tracer = PoolManager.Spawn(
                        settings.TracerPrefab, muzzlePosition, Quaternion.identity);
                    tracer.Show(muzzlePosition, end, settings.TracerFadeSeconds);
                }

                if (hit && settings.ImpactEffectPrefab != null)
                {
                    ImpactEffectView impact = PoolManager.Spawn(
                        settings.ImpactEffectPrefab, end, Quaternion.identity);
                    impact.Play(end, hitInfo.normal);
                }
            }
        }
    }
}
