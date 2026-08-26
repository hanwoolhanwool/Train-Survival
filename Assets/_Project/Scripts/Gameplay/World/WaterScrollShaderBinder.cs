using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 물 셰이더(<c>S_StylizedWater</c>)의 월드 UV 모드에 누적 주행 거리를 전역으로 넘긴다
    /// (바다 지역 구현 계획 §5.2).
    ///
    /// <para><b>왜 필요한가.</b> 바다는 40 m 타일이 9장 이어지므로 잔물결 UV가 타일 로컬이면
    /// 이음매마다 무늬가 처음부터 다시 시작해 <b>경계선이 보인다.</b> 그래서 셰이더가 월드 XZ를
    /// 쓰는데, 열차 원점 고정 좌표계에서는 월드가 −Z로 흐르므로 월드 UV를 그대로 쓰면 무늬가
    /// 월드에 못박혀 <b>물만 정지해 보인다.</b> 누적 주행 거리를 더해야 무늬가 타일과 함께 흐른다.</para>
    ///
    /// <para>지형·자원이 쓰는 것과 <b>같은 기준값</b>(<see cref="IWorldScrollService.TraveledDistance"/>)을
    /// 쓰므로 물과 지형이 어긋나지 않는다. 전역 프로퍼티라 물 머티리얼이 몇 벌이든 한 번만 쓴다.</para>
    /// </summary>
    /// <remarks>
    /// 표현 전용이다 — 게임 상태를 읽기만 하고 아무것도 바꾸지 않으므로 네트워크 동기화 대상이 아니다.
    /// 각 피어가 자기 <see cref="IWorldScrollService"/>에서 읽으며, 그 값은 이미 호스트 권위로 수렴한다.
    /// </remarks>
    public sealed class WaterScrollShaderBinder : MonoBehaviour
    {
        private static readonly int WorldScrollDistanceId = Shader.PropertyToID("_WorldScrollDistance");

        private IWorldScrollService _scroll;

        private void OnEnable()
        {
            // 스크롤 서비스가 아직 없을 수도 있다(스폰 순서). LateUpdate가 재시도한다.
            ServiceLocator.TryGet(out _scroll);
        }

        private void OnDisable()
        {
            _scroll = null;

            // 바다를 벗어나거나 씬이 내려갈 때 마지막 값이 전역에 남지 않게 되돌린다.
            Shader.SetGlobalFloat(WorldScrollDistanceId, 0f);
        }

        // 타일 위치가 갱신된 뒤에 넘겨야 같은 프레임에서 물과 지형이 어긋나지 않는다.
        private void LateUpdate()
        {
            if (_scroll == null && !ServiceLocator.TryGet(out _scroll))
            {
                return;
            }

            Shader.SetGlobalFloat(WorldScrollDistanceId, _scroll.TraveledDistance);
        }
    }
}
