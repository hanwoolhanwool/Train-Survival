using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 월드 스크롤 좌표 계산 순수 로직. 열차 전방 = +Z이며, 스크롤은 월드 소속 오브젝트를 -Z로 밀어낸다.
    /// 네트워크·씬 무의존 — EditMode 테스트 대상.
    /// </summary>
    public static class WorldScrollMath
    {
        /// <summary>스폰 시점 (위치, 누적 거리) 바인딩으로부터 현재 누적 거리에서의 월드 위치를 유도한다.</summary>
        public static Vector3 GetScrolledPosition(Vector3 spawnPosition, float spawnDistance, float currentDistance)
        {
            float delta = currentDistance - spawnDistance;
            return new Vector3(spawnPosition.x, spawnPosition.y, spawnPosition.z - delta);
        }

        /// <summary>
        /// 표시 거리(current)를 호스트 수신 거리(target)로 스무딩 보정한다.
        /// 속도 적분으로 기본 진행을 유지하고, 오차는 지수 감쇠로 워프 없이 수렴시킨다.
        /// </summary>
        public static float SmoothToward(float current, float target, float scrollSpeed, float deltaTime, float correctionRate)
        {
            float advanced = current + scrollSpeed * deltaTime;
            float error = target - advanced;
            return advanced + error * (1f - Mathf.Exp(-correctionRate * deltaTime));
        }
    }
}
