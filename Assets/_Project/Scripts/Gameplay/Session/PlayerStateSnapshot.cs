using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.Session
{
    /// <summary>
    /// 끊김 시점 플레이어 상태 스냅샷 — 순수 구조체 (M6 1차 §2.2, EditMode 검증 대상).
    /// 호스트가 despawn 순간 캡처해 식별 토큰으로 보관하고, 같은 토큰의 재접속 스폰에 재주입한다.
    /// 버프(재생·보온)는 의도적 제외(결정 ⑤ — 세기가 서버 전용 필드라 despawn과 함께 소실되고,
    /// 음식 버프는 지속이 짧아 캡처할 가치가 없다). 위치는 결정 ① 개정(2026-08-13 사용자 승인)으로
    /// 포함 — 적용 시점에 "편성에 붙어 있는 살아있는 칸의 갑판 위"일 때만 복원하고, 아니면
    /// 현행 스폰 지점 폴백이다 (위치는 소유자 권위라 서버 재주입이 아니라 소유자 배치 지시로 간다).
    /// </summary>
    public readonly struct PlayerStateSnapshot
    {
        /// <summary>핫바+가방 슬롯. 보따리(Bundle) 칸은 Count에 서버 보관소 id가 실리는데,
        /// IBundleItemStore가 서버 세션 상태라 세션 내 재접속에서는 그대로 유효하다.</summary>
        public readonly HotbarSlotView[] Slots;

        /// <summary>착용 장비 4부위 — 인덱스 = EquipSlot 값.</summary>
        public readonly HotbarSlotView[] Equipment;

        /// <summary>집게 등급 (1~).</summary>
        public readonly int HarpoonTier;

        public readonly float Health;

        public readonly float Hunger;

        public readonly float Temperature;

        /// <summary>끊김 시점에 부활 대기 중이었나 (전투 사망·이탈 사망 통합) —
        /// true면 스탯 복원은 생략된다 (결정 ② — 부활 시 기존 경로가 스탯을 초기화한다).</summary>
        public readonly bool RespawnPending;

        /// <summary>사망 확정 시각 (서버 시계, 초) — 잔여 대기 계산용 (결정 ⑦).</summary>
        public readonly double DeathServerTime;

        /// <summary>사망 확정 시 계산된 부활 대기 시간 (초).</summary>
        public readonly float RespawnDelaySeconds;

        /// <summary>끊김 시점 위치 (서버 복제 값) — 유효성 판정은 적용 시점(칸이 그 사이 이탈·파괴될
        /// 수 있다)에 하고, 부활 대기 중이었으면 쓰지 않는다 (부활 경로가 위치를 정한다).</summary>
        public readonly Vector3 Position;

        public PlayerStateSnapshot(
            HotbarSlotView[] slots, HotbarSlotView[] equipment, int harpoonTier,
            float health, float hunger, float temperature,
            bool respawnPending, double deathServerTime, float respawnDelaySeconds,
            Vector3 position = default)
        {
            Slots = slots;
            Equipment = equipment;
            HarpoonTier = harpoonTier;
            Health = health;
            Hunger = hunger;
            Temperature = temperature;
            RespawnPending = respawnPending;
            DeathServerTime = deathServerTime;
            RespawnDelaySeconds = respawnDelaySeconds;
            Position = position;
        }

        /// <summary>
        /// 잔여 부활 대기 시간(초) — 끊겨 있던 시간도 대기에 포함된다 (결정 ⑦: 접속을 유지한
        /// 경우와 같은 시점에 부활해 죽은 채 나갔다 들어오는 악용이 성립하지 않는다).
        /// 0 이하 = 즉시 부활.
        /// </summary>
        public float GetRemainingRespawnSeconds(double serverTimeNow)
        {
            return (float)(DeathServerTime + RespawnDelaySeconds - serverTimeNow);
        }
    }
}
