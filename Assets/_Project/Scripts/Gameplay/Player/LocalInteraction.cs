using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// "근처 + 시선, E키" 상호작용의 공통 판정 (M5 3차 공통화 — EngineFuelPort·CraftingStation이
    /// 복붙하던 로컬 플레이어 조회·시선 정렬 판정의 세 번째 사용처가 생겨 유틸로 추출).
    /// 로컬 표시·입력 판정 전용 — 서버 거리 재검증은 각 호출부가 자기 기준점으로 수행한다.
    /// </summary>
    public static class LocalInteraction
    {
        /// <summary>로컬 클라이언트의 플레이어 오브젝트 — 세션 미참여면 null.</summary>
        public static NetworkObject GetLocalPlayerObject()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.LocalClient == null)
            {
                return null;
            }

            return manager.LocalClient.PlayerObject;
        }

        /// <summary>플레이어가 지점의 반경 안에 있는지.</summary>
        public static bool IsWithinRange(NetworkObject player, Vector3 point, float radius)
        {
            return player != null
                && (player.transform.position - point).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// 플레이어 카메라가 지점을 향하고 있는지 — 카메라를 못 찾으면 거리만으로 폴백한다(true).
        /// </summary>
        public static bool IsLookingAt(NetworkObject player, Vector3 point, float dotThreshold)
        {
            if (player == null)
            {
                return false;
            }

            Camera camera = player.GetComponentInChildren<Camera>();
            if (camera == null)
            {
                return true;
            }

            Vector3 toPoint = point - camera.transform.position;
            if (toPoint.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            return Vector3.Dot(camera.transform.forward, toPoint.normalized) >= dotThreshold;
        }
    }
}
