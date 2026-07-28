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
    /// - 숫자패드 8 : 후미에 온실칸 1칸 무료 증설(§M3 증설 테스트 — 비용 경로는 증설 포트로 검증).
    /// - 숫자패드 9 : 요청자에게 자원 10개 지급(증설 비용·연료 투입 테스트).
    /// - 숫자패드 6 : 표적 연결부·후미 칸·건축물에 샘플 데미지 30(수리 망치 테스트).
    /// 클라이언트 입력도 ServerRpc 경유로 호스트가 확정한다. Train(씬 NetworkObject)에 배치한다.
    /// </summary>
    public sealed class QaDebugHotkeys : NetworkBehaviour
    {
        private const string GameplaySceneName = "Game";
        private const float SampleDamage = 30f;

        [Tooltip("켜면 숫자패드 + = 재시작, 7 = 연결부 파괴, 8 = 온실칸 증설, 9 = 자원 지급, 6 = 부위 데미지. QA 전용이므로 릴리스에서는 끈다.")]
        [SerializeField] private bool _enableQaKeys = true;

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
                RequestAppendGreenhouseServerRpc();
            }

            if (keyboard.numpad9Key.wasPressedThisFrame)
            {
                RequestGrantResourcesServerRpc();
            }

            if (keyboard.numpad6Key.wasPressedThisFrame)
            {
                RequestSampleDamageServerRpc();
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

        /// <summary>후미에 온실칸 1칸을 무료 증설한다 — 비용 지불 경로는 증설 포트가 따로 검증한다.</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestAppendGreenhouseServerRpc()
        {
            if (ServiceLocator.TryGet(out ITrainExpansion expansion))
            {
                expansion.ServerTryAppendCar(CarType.Greenhouse);
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
            inventory?.ServerTryAdd(10);
        }

        /// <summary>수리 대상을 만들기 위해 표적 연결부·최후미 칸·살아 있는 건축물에 샘플 데미지를 넣는다.</summary>
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
