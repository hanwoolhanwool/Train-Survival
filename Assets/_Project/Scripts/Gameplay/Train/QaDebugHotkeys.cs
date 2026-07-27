using Game.Core.Services;
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
    /// 클라이언트 입력도 ServerRpc 경유로 호스트가 확정한다. Train(씬 NetworkObject)에 배치한다.
    /// </summary>
    public sealed class QaDebugHotkeys : NetworkBehaviour
    {
        private const string GameplaySceneName = "Game";

        [Tooltip("켜면 숫자패드 + = 재시작, 숫자패드 7 = 연결부 파괴. QA 전용이므로 릴리스에서는 끈다.")]
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
    }
}
