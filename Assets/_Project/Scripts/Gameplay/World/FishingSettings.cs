using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 낚시 밸런스 (바다 지역 구현 계획 §7). 수치는 전부 여기에 있고
    /// <see cref="FishingLogic"/>은 계산만 한다 — 밸런싱에 코드 수정이 없어야 한다는 규약.
    /// </summary>
    [CreateAssetMenu(fileName = "FishingSettings", menuName = "Game/Fishing Settings")]
    public sealed class FishingSettings : ScriptableObject
    {
        [Header("입질")]
        [Tooltip("가장 빨리 물리는 시간 (초).")]
        [SerializeField, Min(0.1f)] private float _minBiteDelaySeconds = 2.5f;

        [Tooltip("가장 오래 걸리는 시간 (초). 정지 중에는 이 값이 상한 그대로다.")]
        [SerializeField, Min(0.2f)] private float _maxBiteDelaySeconds = 12f;

        [Tooltip("이 속도에서 속도 보정이 최대가 된다 (m/s). 기본 스크롤 속도와 맞춘다.")]
        [SerializeField, Min(0.1f)] private float _referenceScrollSpeed = 6f;

        [Tooltip("속도가 대기 상한을 얼마나 당기는가. 끌낚시의 세기다.")]
        [SerializeField, Range(0f, 1f)] private float _speedInfluence = 0.8f;

        [Header("챔질")]
        [Tooltip("입질 후 이 시간 안에 당겨야 걸린다 (초).")]
        [SerializeField, Min(0.1f)] private float _hookWindowSeconds = 1.2f;

        [Header("어획")]
        [Tooltip("찌가 닿을 수 있는 최대 거리 (m).")]
        [SerializeField, Min(1f)] private float _castRange = 25f;

        [Tooltip("두 마리가 한 번에 올라올 확률.")]
        [SerializeField, Range(0f, 1f)] private float _doubleCatchChance = 0.15f;

        [Tooltip("잡히는 자원. 종류 분화는 별도 카탈로그로 풀어야 한다(원자재 대역이 거의 찼다).")]
        [SerializeField] private ResourceType _catchType = ResourceType.Fish;

        public float MinBiteDelaySeconds => _minBiteDelaySeconds;

        public float MaxBiteDelaySeconds => _maxBiteDelaySeconds;

        public float ReferenceScrollSpeed => _referenceScrollSpeed;

        public float SpeedInfluence => _speedInfluence;

        public float HookWindowSeconds => _hookWindowSeconds;

        public float CastRange => _castRange;

        public float DoubleCatchChance => _doubleCatchChance;

        public ResourceType CatchType => _catchType;
    }
}
