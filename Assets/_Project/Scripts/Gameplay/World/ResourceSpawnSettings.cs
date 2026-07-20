using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>지상 자원(돌·나뭇가지 더미) 주기 스폰 밸런스 데이터 (슬라이스 §1.1).</summary>
    [CreateAssetMenu(fileName = "ResourceSpawnSettings", menuName = "Game/Resource Spawn Settings")]
    public sealed class ResourceSpawnSettings : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _spawnIntervalMeters = 12f;
        [SerializeField, Min(0f)] private float _spawnAheadMeters = 60f;
        [SerializeField, Min(1f)] private float _despawnBehindMeters = 80f;

        [Header("선로변 배치 (트랙 중심 제외 구간)")]
        [SerializeField, Min(0f)] private float _minLateralOffset = 4f;
        [SerializeField, Min(0f)] private float _maxLateralOffset = 16f;
        [SerializeField] private float _spawnHeight = 0.4f;

        /// <summary>이 주행 거리마다 자원 1개를 스폰한다.</summary>
        public float SpawnIntervalMeters => _spawnIntervalMeters;

        /// <summary>열차 전방 몇 m 지점에 미리 심어 두는가.</summary>
        public float SpawnAheadMeters => _spawnAheadMeters;

        /// <summary>스폰 지점 기준 이만큼 뒤로 밀려나면 회수한다.</summary>
        public float DespawnBehindMeters => _despawnBehindMeters;

        public float MinLateralOffset => _minLateralOffset;

        public float MaxLateralOffset => _maxLateralOffset;

        public float SpawnHeight => _spawnHeight;
    }
}
