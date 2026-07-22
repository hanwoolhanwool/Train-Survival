using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 플레이어 체력·부활 밸런스 데이터 (기획서 §9.1 — 팀 생존 시 사망자는 n초 후 부활).
    /// 부활 대기 n값의 Day 비례 증가 검토(M2 미결)는 <see cref="RespawnDelayPerDaySeconds"/>로 데이터화한다.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerHealthSettings", menuName = "Game/Player Health Settings")]
    public sealed class PlayerHealthSettings : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _maxHealth = 100f;

        [Header("부활 대기 (기획서 §9.1 n값)")]
        [SerializeField, Min(0f)] private float _respawnDelaySeconds = 5f;
        [SerializeField, Min(0f)] private float _respawnDelayPerDaySeconds = 1f;
        [SerializeField, Min(0f)] private float _respawnDelayCapSeconds = 20f;

        public float MaxHealth => _maxHealth;

        /// <summary>Day 번호에 따른 부활 대기 시간 — 기본 n + Day 비례 증가 (상한 적용).</summary>
        public float GetRespawnDelaySeconds(int dayNumber)
        {
            float delay = _respawnDelaySeconds + _respawnDelayPerDaySeconds * Mathf.Max(0, dayNumber - 1);
            return Mathf.Min(delay, _respawnDelayCapSeconds);
        }
    }
}
