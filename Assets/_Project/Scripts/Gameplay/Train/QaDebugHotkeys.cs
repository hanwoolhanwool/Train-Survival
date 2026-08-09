using Game.Core.Services;
using Game.Gameplay.Inventory;
using Game.Systems.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// QA 테스트용 디버그 핫키 (릴리스에서는 <see cref="_enableQaKeys"/>를 끈다).
    /// - 숫자패드 + : 게임 재시작(Game 씬 재로드, 호스트 권위로 편성·웨이브·사이클 초기화).
    /// - 숫자패드 7 : 현재 표적 가능한(후미) 연결부 1개 파괴(후방 연쇄 이탈 테스트).
    /// - 숫자패드 8 : 칸 1칸 무료 건설 — 빈 슬롯(파괴·소실) 재건 우선, 없으면 후미 증설(비용 경로는 건설 포트로 검증).
    /// - 숫자패드 9 : 요청자에게 자원 지급(건자재·제작 재료·식재료 — 증설 비용·연료 투입·요리 테스트).
    /// - 숫자패드 6 : 샘플 데미지 30 — <b>망치로 겨눈 부위</b>가 있으면 그 부위에, 없으면
    ///   표적 연결부·후미 칸·건축물 순의 폴백(특정 건축물을 골라 손상시킬 수 있게 한다, M5 4차 C7).
    /// - 숫자패드 5 : 몬스터 웨이브 스폰 토글(M5 4차 — 밤 노숙 체온 검증용).
    /// - 숫자패드 4 : 공유 창고 동시 경합 재현(M5 5차 — 검증 G2). 전 피어가 같은 프레임에
    ///   같은 이동(창고 0 → 개인 0)을 요청하고, 총량 보존 여부를 호스트 콘솔에 찍는다.
    /// - 숫자패드 0 : 피해 실측(M5 6차 — 검증 H7). 요청자에게 고정 피해 20을 물리 경로로 넣는다 —
    ///   장비 감산이 적용되므로 맨몸 20 vs 가죽 옷 17을 실측할 수 있다.
    /// - 숫자패드 − : 동시 그랩 경합 재현(M5 6차 — 검증 I1·I2). 호스트가 최근접 그랩 가능 자원을
    ///   골라 전 피어에 뿌리고, 각 피어가 수신 프레임에 자기 집게로 그랩을 요청한다.
    /// 클라이언트 입력도 ServerRpc 경유로 호스트가 확정한다. Train(씬 NetworkObject)에 배치한다.
    /// </summary>
    public sealed class QaDebugHotkeys : NetworkBehaviour
    {
        private const string GameplaySceneName = "Game";
        private const float SampleDamage = 30f;
        private const float SelfDamage = 20f;

        [Tooltip("켜면 숫자패드 + = 재시작, 7 = 연결부 파괴, 8 = 온실칸 증설, 9 = 자원·식재료 지급, 6 = 부위 데미지, 5 = 몬스터 스폰 토글, 4 = 창고 동시 경합, 0 = 피해 실측, − = 동시 그랩. QA 전용이므로 릴리스에서는 끈다.")]
        [SerializeField] private bool _enableQaKeys = true;

        // 로컬 망치가 마지막으로 알린 조준 부위 — 숫자패드 6의 데미지 대상 선택에 쓴다.
        private bool _hasHammerTarget;
        private TrainPartKind _hammerTargetKind;
        private int _hammerTargetIndex;

        private void OnEnable()
        {
            Core.Events.EventBus<HammerTargetLocalEvent>.Subscribe(OnHammerTarget);
        }

        private void OnDisable()
        {
            Core.Events.EventBus<HammerTargetLocalEvent>.Unsubscribe(OnHammerTarget);
        }

        private void OnHammerTarget(HammerTargetLocalEvent evt)
        {
            _hasHammerTarget = evt.HasTarget;
            _hammerTargetKind = evt.Kind;
            _hammerTargetIndex = evt.Index;
        }

        private void Update()
        {
            if (!_enableQaKeys || !IsSpawned)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.numpadPlusKey.wasPressedThisFrame)
            {
                RequestRestartServerRpc();
            }

            if (keyboard.numpad7Key.wasPressedThisFrame)
            {
                RequestBreakCouplingServerRpc();
            }

            if (keyboard.numpad8Key.wasPressedThisFrame)
            {
                RequestBuildCarServerRpc();
            }

            if (keyboard.numpad9Key.wasPressedThisFrame)
            {
                RequestGrantResourcesServerRpc();
            }

            if (keyboard.numpad6Key.wasPressedThisFrame)
            {
                // 망치로 겨눈 부위가 있으면 그것을 때린다 — 폴백은 "뒤에서 첫 부위"라
                // 특정 건축물(예: 앞 칸의 화덕)을 손상시킬 수 없었다 (M5 4차 C7).
                if (_hasHammerTarget)
                {
                    RequestTargetedDamageServerRpc(_hammerTargetKind, _hammerTargetIndex);
                }
                else
                {
                    RequestSampleDamageServerRpc();
                }
            }

            if (keyboard.numpad5Key.wasPressedThisFrame)
            {
                RequestToggleMonsterSpawnServerRpc();
            }

            if (keyboard.numpad4Key.wasPressedThisFrame)
            {
                RequestStorageContentionServerRpc();
            }

            if (keyboard.numpad0Key.wasPressedThisFrame)
            {
                RequestSelfDamageServerRpc();
            }

            if (keyboard.numpadMinusKey.wasPressedThisFrame)
            {
                RequestSimultaneousGrabServerRpc();
            }
        }

        /// <summary>
        /// 피해 실측 (M5 6차 — 검증 H7, 3~5차 연속 이월 해소). 요청자에게 고정 피해를
        /// <b>물리 경로</b>(<see cref="Player.PlayerHealth.ApplyDamage"/>)로 넣는다 — 장비 감산이
        /// 적용되므로 착용 전후를 같은 기준으로 실측할 수 있다. 결과는 호스트 콘솔에 찍힌다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSelfDamageServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null ||
                !manager.ConnectedClients.TryGetValue(senderId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            var health = client.PlayerObject.GetComponent<Player.PlayerHealth>();
            if (health == null || !health.IsAlive)
            {
                Debug.Log($"[QaDebugHotkeys] 피해 실측 무효: client={senderId} — 플레이어가 살아 있지 않다");
                return;
            }

            float before = health.Health;
            health.ApplyDamage(SelfDamage, senderId);
            Debug.Log($"[QaDebugHotkeys] 피해 실측: client={senderId} 기준 {SelfDamage} → " +
                $"체력 {before:F1} → {health.Health:F1} (적용 {before - health.Health:F1})");
        }

        /// <summary>
        /// 동시 그랩 경합 재현 (M5 6차 — 검증 I1·I2). 호스트가 요청자 최근접 그랩 가능 자원 노드를
        /// 골라 전 피어에 브로드캐스트하고, 각 피어가 수신 프레임에 자기 집게로 그랩 요청을 발행한다 —
        /// 서버 도착이 붙어 "한쪽 승인 + 한쪽 다른 사람이 잡고 있다"가 재현된다.
        /// 창고 경합의 교훈대로 전제(대상 존재)를 시작 단계에서 검사하고, 승인·거부는
        /// 집게의 호스트 콘솔 로그로 확인한다 (무효 통과 방지).
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSimultaneousGrabServerRpc(RpcParams rpcParams = default)
        {
            NetworkObject target = FindNearestGrabbableResource(rpcParams.Receive.SenderClientId);
            if (target == null)
            {
                Debug.Log("[QaDebugHotkeys] 동시 그랩 무효: 그랩 가능한 자원 노드가 없다");
                return;
            }

            Debug.Log($"[QaDebugHotkeys] 동시 그랩 트리거: 대상={target.name} — 전 피어가 같은 프레임에 그랩을 요청한다");
            TriggerSimultaneousGrabRpc(target);
        }

        private static NetworkObject FindNearestGrabbableResource(ulong requesterClientId)
        {
            NetworkManager manager = NetworkManager.Singleton;
            Vector3 origin = Vector3.zero;
            if (manager != null &&
                manager.ConnectedClients.TryGetValue(requesterClientId, out NetworkClient client) &&
                client.PlayerObject != null)
            {
                origin = client.PlayerObject.transform.position;
            }

            World.ResourceNode best = null;
            float bestSqr = float.MaxValue;
            foreach (World.ResourceNode node in FindObjectsByType<World.ResourceNode>(FindObjectsSortMode.None))
            {
                if (!node.IsAvailableForGrab)
                {
                    continue;
                }

                float sqr = (node.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = node;
                }
            }

            return best != null ? best.NetworkObject : null;
        }

        [Rpc(SendTo.Everyone)]
        private void TriggerSimultaneousGrabRpc(NetworkObjectReference targetRef)
        {
            if (!targetRef.TryGet(out NetworkObject target))
            {
                return;
            }

            NetworkObject player = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                ? NetworkManager.Singleton.LocalClient.PlayerObject
                : null;
            var harpoon = player != null ? player.GetComponent<Harpoon.HarpoonController>() : null;
            harpoon?.QaRequestGrab(target);
        }

        /// <summary>
        /// 공유 창고 동시 경합 재현 (M5 5차 — 검증 G2). 호스트가 전 피어에 트리거를 뿌리고
        /// 각 피어가 수신 프레임에 같은 이동을 요청한다 — 서버 도착이 붙어 경합이 재현된다.
        /// 총량 보존 여부는 호스트 콘솔에 (요청 전 / 확정 후) 두 줄로 찍힌다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestStorageContentionServerRpc()
        {
            if (ServiceLocator.TryGet(out ITrainStorage storage) && storage is TrainStorage concrete)
            {
                concrete.ServerTriggerContentionTest();
            }
        }

        /// <summary>
        /// 몬스터 웨이브 스폰 토글 (M5 4차 — 밤 노숙 체온 검증의 선결 수단). 끄면 진행 중인 웨이브를
        /// 회수하고 다음 밤 웨이브도 시작하지 않는다. 상태는 스포너 콘솔 로그로 확인한다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestToggleMonsterSpawnServerRpc()
        {
            if (ServiceLocator.TryGet(out Monsters.IWaveSpawnToggle toggle))
            {
                toggle.ServerSetSpawnEnabled(!toggle.SpawnEnabled);
            }
        }

        /// <summary>게임 재시작 — 호스트가 Game 씬을 단일 모드로 재로드해 모든 네트워크 상태를 초기화한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestRestartServerRpc()
        {
            if (ServiceLocator.TryGet(out INetworkSessionService session))
            {
                session.LoadGameplayScene(GameplaySceneName);
            }
        }

        /// <summary>지금 표적 가능한 연결부(살아 있는 것 중 가장 후미 — 순차 파괴 규칙)를 찾아 파괴한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestBreakCouplingServerRpc()
        {
            if (!ServiceLocator.TryGet(out ITrainState train) || !ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                return;
            }

            for (int i = train.CouplingCount - 1; i >= 0; i--)
            {
                if (train.IsCouplingTargetable(i))
                {
                    sink.ApplyCouplingDamage(i, float.MaxValue);
                    return;
                }
            }
        }

        /// <summary>칸 1칸을 무료 건설한다(빈 슬롯 재건 우선) — 비용 지불 경로는 망치 칸 건설이 따로 검증한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestBuildCarServerRpc()
        {
            if (ServiceLocator.TryGet(out ITrainExpansion expansion))
            {
                expansion.ServerTryBuildCar();
            }
        }

        /// <summary>요청한 플레이어에게 자원 10개를 지급한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestGrantResourcesServerRpc(RpcParams rpcParams = default)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null ||
                !manager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out NetworkClient client) ||
                client.PlayerObject == null)
            {
                return;
            }

            IResourceInventory inventory = client.PlayerObject.GetComponent<IResourceInventory>();
            if (inventory != null)
            {
                // 검증 편의 — 건자재 2종 + 제작 재료 2종 + 식재료를 함께 지급해
                // 건설·제작·요리 경로를 모두 시험할 수 있게 한다 (식재료 4 = 스튜 1회 또는 구운 식사 2회).
                inventory.ServerTryAdd(ResourceType.Wood, 4);
                inventory.ServerTryAdd(ResourceType.Stone, 2);
                inventory.ServerTryAdd(ResourceType.Scrap, 2);
                inventory.ServerTryAdd(ResourceType.Niter, 2);
                inventory.ServerTryAdd(ResourceType.RawFood, 4);
            }
        }

        /// <summary>
        /// 망치로 겨눈 부위 하나에 샘플 데미지를 넣는다 (M5 4차) — 겨눈 것만 맞으므로
        /// 특정 건축물(화덕 등)을 골라 손상·파괴시킬 수 있다. 부위 식별은 망치 RPC와 같은 (종류, 인덱스) 규약.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestTargetedDamageServerRpc(TrainPartKind kind, int index)
        {
            if (!ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                return;
            }

            switch (kind)
            {
                case TrainPartKind.Coupling:
                    sink.ApplyCouplingDamage(index, SampleDamage);
                    break;

                case TrainPartKind.Car:
                    sink.ApplyCarDamage(index, SampleDamage);
                    break;

                case TrainPartKind.Structure:
                    sink.ApplyStructureDamage(index, SampleDamage);
                    break;
            }
        }

        /// <summary>수리 대상을 만들기 위해 표적 연결부·최후미 칸·살아 있는 건축물에 샘플 데미지를 넣는다 (겨눈 부위가 없을 때의 폴백).</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSampleDamageServerRpc()
        {
            if (!ServiceLocator.TryGet(out ITrainState train) || !ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                return;
            }

            for (int i = train.CouplingCount - 1; i >= 0; i--)
            {
                if (train.IsCouplingTargetable(i))
                {
                    sink.ApplyCouplingDamage(i, SampleDamage);
                    break;
                }
            }

            for (int i = train.CarCount - 1; i > 0; i--)
            {
                if (train.TryGetCar(i, out CarState car) && TrainStateLogic.IsCarPresent(car))
                {
                    sink.ApplyCarDamage(i, SampleDamage);
                    break;
                }
            }

            for (int i = train.CarCount - 1; i >= 0; i--)
            {
                if (train.TryGetStructure(i, out StructureState structure)
                    && TrainStateLogic.IsStructureAlive(structure))
                {
                    sink.ApplyStructureDamage(i, SampleDamage);
                    break;
                }
            }
        }
    }
}
