using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지상(월드 소속) 표면 마커. 이 컴포넌트가 붙은 콜라이더 위에 선 플레이어는
    /// 스크롤 속도만큼 컨베이어 밀림을 로컬 적용받는다 (네트워크 문서 §4.2 상시 외력형).
    /// </summary>
    public sealed class WorldFrameSurface : MonoBehaviour
    {
    }
}
