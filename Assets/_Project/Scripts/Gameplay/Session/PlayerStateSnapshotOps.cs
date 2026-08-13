using Game.Gameplay.Harpoon;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;

namespace Game.Gameplay.Session
{
    /// <summary>
    /// 스냅샷 캡처·적용 — 서버 전용 (M6 1차 §2.2·§2.3). 캡처는 despawn 순간(OnNetworkDespawn 서버
    /// 분기), 적용은 재접속 스폰 완료 후 호출된다. 컴포넌트별 서버 API로만 읽고 쓴다.
    /// </summary>
    public static class PlayerStateSnapshotOps
    {
        public static PlayerStateSnapshot Capture(
            PlayerInventory inventory, HarpoonController harpoon, PlayerHealth health,
            PlayerHunger hunger, PlayerTemperature temperature, NetworkPlayerController controller)
        {
            // 부활 대기 = 두 사망 경로의 통합 (M5 4차 D5·D10과 같은 계약) — 전투 사망(_serverDead)과
            // 체력을 거치지 않는 이탈 사망(IsRespawnPending)을 모두 본다.
            bool respawnPending = (health != null && (health.ServerIsDead || health.Health <= 0f))
                || (controller != null && controller.IsRespawnPending);

            return new PlayerStateSnapshot(
                inventory != null ? inventory.ServerCopySlotViews() : System.Array.Empty<HotbarSlotView>(),
                inventory != null ? inventory.ServerCopyEquipmentViews() : System.Array.Empty<HotbarSlotView>(),
                harpoon != null ? harpoon.Tier : 1,
                health != null ? health.Health : 0f,
                hunger != null ? hunger.ServerHunger : 0f,
                temperature != null ? temperature.ServerTemperature : 0f,
                respawnPending,
                health != null ? health.ServerDeathTime : 0d,
                health != null ? health.ServerRespawnDelaySeconds : 0f,
                controller != null ? controller.transform.position : default);
        }

        /// <summary>
        /// 적용 순서 = 슬롯·장비·티어 → 부활 대기 분기 (§2.3): 부활 대기 중이었으면 스탯·위치
        /// 복원을 생략하고(사망 중 스탯은 매 프레임 정상치로 강제 리셋되고 세터도 IsAlive 게이트에
        /// 막혀 복원해도 즉시 무효 — 결정 ②와 정합, 위치는 부활 경로가 정한다) 잔여 시간으로
        /// 부활 재개를 지시한다. 생존 중이었으면 스탯 + 끊김 위치(유효 시 — 결정 ① 개정)를 복원한다.
        /// </summary>
        public static void Apply(
            in PlayerStateSnapshot snapshot,
            PlayerInventory inventory, HarpoonController harpoon, PlayerHealth health,
            PlayerHunger hunger, PlayerTemperature temperature, NetworkPlayerController controller,
            double serverTimeNow)
        {
            if (inventory != null)
            {
                inventory.ServerApplySlotViews(snapshot.Slots);
                inventory.ServerApplyEquipmentViews(snapshot.Equipment);
            }

            if (harpoon != null)
            {
                harpoon.ServerSetTier(snapshot.HarpoonTier);
            }

            if (snapshot.RespawnPending)
            {
                if (health != null)
                {
                    health.ServerResumeRespawn(snapshot.GetRemainingRespawnSeconds(serverTimeNow));
                }

                return;
            }

            if (health != null)
            {
                health.ServerSetHealth(snapshot.Health);
            }

            if (hunger != null)
            {
                hunger.ServerSetHunger(snapshot.Hunger);
            }

            if (temperature != null)
            {
                temperature.ServerSetTemperature(snapshot.Temperature);
            }

            if (controller != null)
            {
                controller.ServerRestorePosition(snapshot.Position);
            }
        }
    }
}
