using System.Collections;
using Game.Core.Services;
using Game.Gameplay.Harpoon;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using Game.Systems.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Session
{
    /// <summary>
    /// 플레이어 재접속 세션 훅 — 호스트 전용 동작, Player 프리팹에 부착 (M6 1차 §2.2·§2.3).
    /// 끊김(despawn) 순간 상태를 캡처해 <see cref="PlayerSessionRegistry"/>에 보관하고,
    /// 같은 식별 토큰의 재접속 스폰에 꺼내 재주입한다.
    /// 캡처 훅이 OnNetworkDespawn인 이유: NGO의 끊김 처리는 ① 플레이어 오브젝트 despawn →
    /// ③ ConnectedClients 제거 → ④ OnClientDisconnectCallback 순서라, 콜백 시점에는 컴포넌트를
    /// 이미 읽을 수 없다 (§2.2 실측). despawn(①)에서만 상태가 온전하다.
    /// </summary>
    public sealed class PlayerSessionAgent : NetworkBehaviour
    {
        // 복원 시도(스폰 다음 프레임)를 마치기 전에 다시 despawn되면 캡처를 건너뛴다 —
        // 초기 지급 상태로 보관 중인 진짜 스냅샷을 덮어쓰지 않기 위해.
        private bool _serverRestoreChecked;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            // 호스트 자신은 재접속 대상이 아니다 (호스트 종료 = 세션 종료 — §0 비범위).
            if (OwnerClientId == NetworkManager.ServerClientId)
            {
                _serverRestoreChecked = true;
                return;
            }

            StartCoroutine(ServerRestoreAfterInitialGive());
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer || OwnerClientId == NetworkManager.ServerClientId || !_serverRestoreChecked)
            {
                return;
            }

            // 세션 종료·씬 전환 teardown의 despawn은 끊김이 아니다 — 캡처 제외 (§2.2 가드).
            if (NetworkManager == null || NetworkManager.ShutdownInProgress)
            {
                return;
            }

            if (!TryGetToken(out string token))
            {
                return;
            }

            PlayerStateSnapshot snapshot = PlayerStateSnapshotOps.Capture(
                GetComponent<PlayerInventory>(),
                GetComponentInChildren<HarpoonController>(),
                GetComponent<PlayerHealth>(),
                GetComponent<PlayerHunger>(),
                GetComponent<PlayerTemperature>(),
                GetComponent<NetworkPlayerController>());
            PlayerSessionRegistry.GetOrRegister().Store(token, snapshot);
            Debug.Log($"[PlayerSessionAgent] 끊김 스냅샷 캡처: client={OwnerClientId}");
        }

        private IEnumerator ServerRestoreAfterInitialGive()
        {
            // PlayerInventory.OnNetworkSpawn이 같은 스폰 흐름에서 무조건 초기 지급(_slots 재구성)을
            // 실행한다 — 한 프레임 미뤄 적용이 항상 그 이후가 되게 한다 (§2.3 순서 주의).
            yield return null;

            _serverRestoreChecked = true;

            if (!TryGetToken(out string token)
                || !PlayerSessionRegistry.GetOrRegister().TryTake(token, out PlayerStateSnapshot snapshot))
            {
                // 처음 보는 식별자 = 현행 초기 지급 그대로 (신규 drop-in 특별 취급 없음 — §0 비범위).
                yield break;
            }

            PlayerStateSnapshotOps.Apply(
                snapshot,
                GetComponent<PlayerInventory>(),
                GetComponentInChildren<HarpoonController>(),
                GetComponent<PlayerHealth>(),
                GetComponent<PlayerHunger>(),
                GetComponent<PlayerTemperature>(),
                GetComponent<NetworkPlayerController>(),
                NetworkManager.ServerTime.Time);
            Debug.Log($"[PlayerSessionAgent] 재접속 복원 적용: client={OwnerClientId} "
                + $"respawnPending={snapshot.RespawnPending}");

            // 복원으로 부활 대기가 재개됐을 수 있다 — 전멸 재평가 (M6 3차 §2.2 트리거 ⓓ,
            // 스폰~복원 1프레임 창 동안 초기 지급 상태로 보이던 것의 보정).
            if (ServiceLocator.TryGet(out GameOverMonitor gameOver))
            {
                gameOver.ServerReevaluate();
            }
        }

        private bool TryGetToken(out string token)
        {
            token = null;
            return ServiceLocator.TryGet(out IConnectionIdentityRegistry identityRegistry)
                && identityRegistry.TryGetToken(OwnerClientId, out token)
                && !string.IsNullOrEmpty(token);
        }
    }
}
