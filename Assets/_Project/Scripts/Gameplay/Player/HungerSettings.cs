using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 허기 밸런스 데이터 (기획서 §3.4 상태 창, M5 4차 — 요리·허기).
    /// 얼마나 빨리 배고파지고 굶주림이 얼마나 아픈지는 이 에셋이 정한다 —
    /// 무엇을 먹어 얼마나 차는지는 <see cref="Game.Gameplay.Inventory.FoodCatalog"/> 소관.
    /// </summary>
    [CreateAssetMenu(fileName = "HungerSettings", menuName = "Game/Hunger Settings")]
    public sealed class HungerSettings : ScriptableObject
    {
        [Header("허기 범위")]
        [SerializeField, Min(1f)] private float _maxHunger = 100f;

        [Header("변화 속도")]
        [Tooltip("초당 자연 감소량 — 낮/밤 동일. 낮+밤 1사이클(390초)에 식사 1회쯤 되게 잡는다.")]
        [SerializeField, Min(0f)] private float _decayPerSecond = 0.25f;

        [Header("경고·피해 임계")]
        [Tooltip("이 밑으로 내려가면 HUD가 음식 섭취를 지시한다.")]
        [SerializeField, Min(0f)] private float _warnThreshold = 30f;

        [Tooltip("허기 0(기아) 상태의 초당 체력 피해.")]
        [SerializeField, Min(0f)] private float _starveDamagePerSecond = 2f;

        public float MaxHunger => _maxHunger;

        /// <summary>순수 로직(<see cref="HungerMath"/>)에 넘길 곡선으로 변환한다.</summary>
        public HungerCurve ToCurve()
        {
            return new HungerCurve(_maxHunger, _decayPerSecond, _warnThreshold, _starveDamagePerSecond);
        }
    }
}
