using Game.Core.Services;
using Game.Gameplay.Region;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 원경 시차 레이어 (사막 지역 구현 계획 §4.3 · 결정 ④).
    ///
    /// <para><b>한 일만 한다</b> — 자식 전체를 스크롤 이동량 × 시차계수만큼 −Z로 밀고,
    /// <see cref="WrapDistance"/>를 넘으면 되감는다. 그래서 이 레이어에 담기는 것은
    /// <b>되감기 간격마다 되풀이되도록</b> 배치돼 있어야 한다 — 되감는 순간 화면이 같아야
    /// 이음매가 안 보인다.</para>
    ///
    /// <para><b>왜 타일에 얹지 않는가.</b> 활성 구간은 9장 = 360 m뿐이고 6 m/s로 흐르므로
    /// 타일에 얹은 원경은 <b>60초 만에 옆을 지나간 물체</b>가 된다(§3.3). 대자연의 정체는
    /// 크기가 아니라 <b>각속도</b>라서, 거리대마다 다른 속도로 흐르게 하는 것이 본체다.</para>
    ///
    /// <para><b>복제가 없다.</b> 위치가 <see cref="IWorldScrollService.TraveledDistance"/>
    /// 하나의 순수 함수라 전 피어가 같은 화면을 본다 — 지역·낮밤 연출과 같은 규약이고
    /// <c>NetworkObject</c>를 만들지 않는다. 연료 감속·모래폭풍 감속도 스크롤 값에 이미
    /// 반영돼 있어 <b>자동으로 따라온다.</b></para>
    ///
    /// <para>지역 판정은 <b>발밑 지형</b>으로 한다(<see cref="WaterSurfaceQuery.ResolveLocalRegion"/>) —
    /// "현재 지역"으로 켜고 끄면 Day가 넘어간 순간 <b>산만 먼저 사라지고 사막 타일이 40초 더
    /// 남는다.</b> 물이 지형보다 먼저 사라졌던 것과 같은 함정이다.</para>
    /// </summary>
    public sealed class DistantSceneryLayer : MonoBehaviour
    {
        [Tooltip("이 지역에서만 자식을 보여 준다. 비우면 항상 보인다 — 회귀 방어선(RegionSkyController 와 같은 규약).")]
        [SerializeField] private RegionDefinition _region;

        [Tooltip("스크롤 이동량에 곱해 −Z로 미는 비율. 0 = 정지(원경 지면판) / 1 = 근경과 같은 속도. " +
                 "사막 as-built: 중경 0.35 · 유적군 0.10 · 산 능선 0.03.")]
        [SerializeField, Range(0f, 1f)] private float _parallaxFactor = 0.1f;

        [Tooltip("되감기 간격(m). 자식 배치가 이 간격으로 되풀이돼야 이음매가 안 보인다. " +
                 "0 이하면 되감지 않는다(정지 레이어).")]
        [SerializeField, Min(0f)] private float _wrapDistance = 400f;

        /// <summary>자식이 켜져 있는가 — 켜고 끄는 것은 자식이다. 이 오브젝트를 끄면 다시 켤 주체가 없다.</summary>
        private bool _shown = true;

        /// <summary>이 레이어가 보일 지역. null = 모든 지역.</summary>
        public RegionDefinition Region => _region;

        public float ParallaxFactor => _parallaxFactor;

        public float WrapDistance => _wrapDistance;

        /// <summary>
        /// 누적 주행 거리에서 레이어의 Z 오프셋을 낸다 — <b>상태가 없는 순수 함수</b>다.
        /// 되감기 간격이 있으면 <c>[-wrap, 0]</c> 구간을 돌고, 없으면 계속 밀린다.
        /// </summary>
        public static float ResolveOffsetZ(float traveledDistance, float parallaxFactor, float wrapDistance)
        {
            float shifted = traveledDistance * parallaxFactor;
            if (wrapDistance <= 0f)
            {
                return -shifted;
            }

            return -Mathf.Repeat(shifted, wrapDistance);
        }

        /// <summary>
        /// 시차계수의 실효 흐름 속도(m/s) — 계수가 맞는지는 눈이 정하지만, 눈으로 보기 전에
        /// 무엇을 볼지는 이 값이 말해 준다 (§4.1 속도비 6 : 2.1 : 0.6 : 0.18 : 0).
        /// </summary>
        public static float EffectiveSpeed(float scrollSpeed, float parallaxFactor)
        {
            return scrollSpeed * parallaxFactor;
        }

        /// <summary>
        /// 되감기 한 바퀴에 걸리는 시간(초). 반복이 눈에 띄는지를 재는 자다 —
        /// 사막 as-built: 중경 143 s · 유적군 667 s · 산 능선 6,667 s(4일 주행 1,560 s 안에 0회).
        /// </summary>
        public static float WrapPeriodSeconds(float scrollSpeed, float parallaxFactor, float wrapDistance)
        {
            float speed = EffectiveSpeed(scrollSpeed, parallaxFactor);
            if (speed <= 0f || wrapDistance <= 0f)
            {
                return float.PositiveInfinity;
            }

            return wrapDistance / speed;
        }

        private void LateUpdate()
        {
            bool show = ShouldShow();
            if (show != _shown)
            {
                SetChildrenActive(show);
                _shown = show;
            }

            if (!show || !ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                return;
            }

            float z = ResolveOffsetZ(scroll.TraveledDistance, _parallaxFactor, _wrapDistance);
            Vector3 local = transform.localPosition;
            if (!Mathf.Approximately(local.z, z))
            {
                transform.localPosition = new Vector3(local.x, local.y, z);
            }
        }

        private bool ShouldShow()
        {
            if (_region == null)
            {
                return true;
            }

            return ReferenceEquals(WaterSurfaceQuery.ResolveLocalRegion(), _region);
        }

        private void SetChildrenActive(bool active)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(active);
            }
        }
    }
}
