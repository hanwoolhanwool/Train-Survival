using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using Game.Gameplay.Player;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Session
{
    /// <summary>
    /// 게임오버(전멸) 판정의 호스트 권위 소유자 (M6 3차 결정 ②). Game 씬에 1개 배치한다.
    /// 폴링 없이 죽음·끊김·재접속 복원 순간에만 재평가하고, 확정은 NetworkVariable로 복제해
    /// 게임오버 후 재접속한 피어도 후발 동기화로 같은 결과를 받는다.
    /// 판정 자체는 <see cref="GameOverLogic"/>(순수 함수)에 있다.
    /// 소속이 Systems가 아닌 Gameplay인 이유: 판정이 <see cref="PlayerHealth"/> 등 Gameplay
    /// 타입을 직접 읽어야 한다 (어셈블리 단방향 — 1차 PlayerSessionRegistry와 같은 사유).
    /// </summary>
    public sealed class GameOverMonitor : NetworkBehaviour
    {
        // 결과 값을 _gameOver보다 먼저 선언한다 — NGO는 선언 순서대로 델타를 적용하므로
        // 플래그의 OnValueChanged 시점에 결과 값이 이미 도착해 있다.
        private readonly NetworkVariable<int> _dayReached = new NetworkVariable<int>();
        private readonly NetworkVariable<float> _elapsedSeconds = new NetworkVariable<float>();
        private readonly NetworkVariable<bool> _gameOver = new NetworkVariable<bool>();

        private readonly List<GameOverLogic.PlayerLifeState> _evaluationBuffer =
            new List<GameOverLogic.PlayerLifeState>(4);

        /// <summary>게임오버가 확정됐는가 — 종단 가드(부활 무시)가 조회한다. 전 피어 복제 값.</summary>
        public bool IsGameOver => _gameOver.Value;

        public override void OnNetworkSpawn()
        {
            if (!ServiceLocator.IsRegistered<GameOverMonitor>())
            {
                ServiceLocator.Register<GameOverMonitor>(this);
            }

            _gameOver.OnValueChanged += OnGameOverChanged;

            // 게임오버 후 재접속(후발 동기화) — 스폰 시점에 이미 확정돼 있으면 즉시 발행.
            if (_gameOver.Value)
            {
                PublishGameOver();
            }

            if (IsServer)
            {
                EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
                EventBus<PlayerFellBehindEvent>.Subscribe(OnPlayerFellBehind);
                // 끊김 콜백 시점엔 ConnectedClientsList에서 이미 제거돼 있어(NGO 2.13 실측 순서)
                // "끊긴 자 제외" 판정이 자동이다.
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ServiceLocator.TryGet(out GameOverMonitor registered) && ReferenceEquals(registered, this))
            {
                ServiceLocator.Unregister<GameOverMonitor>();
            }

            _gameOver.OnValueChanged -= OnGameOverChanged;

            if (IsServer)
            {
                EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
                EventBus<PlayerFellBehindEvent>.Unsubscribe(OnPlayerFellBehind);
                if (NetworkManager != null)
                {
                    NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                }
            }
        }

        /// <summary>
        /// 재평가 요청 — 재접속 복원(부활 대기 재개) 적용 직후에 <see cref="PlayerSessionAgent"/>가
        /// 부른다 (복원 전 1프레임은 초기 지급 상태로 보여 스폰 시점 판정이 어긋나는 창의 보정).
        /// </summary>
        public void ServerReevaluate()
        {
            ServerEvaluate();
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            ServerEvaluate();
        }

        private void OnPlayerFellBehind(PlayerFellBehindEvent evt)
        {
            ServerEvaluate();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            ServerEvaluate();
        }

        private void ServerEvaluate()
        {
            if (!IsServer || !IsSpawned || _gameOver.Value)
            {
                return;
            }

            _evaluationBuffer.Clear();
            var clients = NetworkManager.ConnectedClientsList;
            for (int i = 0; i < clients.Count; i++)
            {
                NetworkObject playerObject = clients[i].PlayerObject;
                if (playerObject == null)
                {
                    // 접속 승인~스폰 사이 — 생사를 알 수 없으므로 판정 보류 입력.
                    _evaluationBuffer.Add(new GameOverLogic.PlayerLifeState(false, false));
                    continue;
                }

                PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
                // 컴포넌트가 없으면(비정상 구성) 생존으로 간주 — 전멸 오탐보다 미탐이 낫다.
                bool alive = health == null || health.IsAlive;
                _evaluationBuffer.Add(new GameOverLogic.PlayerLifeState(true, alive));
            }

            if (!GameOverLogic.IsWipe(_evaluationBuffer))
            {
                return;
            }

            _dayReached.Value = ServiceLocator.TryGet(out IDayCycleService cycle) ? cycle.DayNumber : 1;
            _elapsedSeconds.Value = (float)NetworkManager.ServerTime.Time;
            _gameOver.Value = true;
            Debug.Log($"[GameOverMonitor] 전멸 확정: day={_dayReached.Value} "
                + $"elapsed={_elapsedSeconds.Value:F0}s players={_evaluationBuffer.Count}");
        }

        private void OnGameOverChanged(bool previous, bool current)
        {
            if (!previous && current)
            {
                PublishGameOver();
            }
        }

        private void PublishGameOver()
        {
            EventBus<GameOverEvent>.Publish(new GameOverEvent(_dayReached.Value, _elapsedSeconds.Value));

            // 메타 진행 기록 (M6 3차 결정 ③) — 각 피어가 자기 로컬에 기록한다 (네트워크 §2.3).
            // 이 컴포넌트는 씬 네트워크 오브젝트라 모든 피어에서 스폰돼 있다.
            if (ServiceLocator.TryGet(out Game.Systems.Meta.IMetaProgressService meta))
            {
                meta.RecordGameOver(_dayReached.Value);
            }

            if (ServiceLocator.TryGet(out Game.Systems.Meta.IAchievementService achievements))
            {
                achievements.Unlock(Game.Systems.Meta.AchievementIds.FirstGameOver);
                if (_dayReached.Value >= 3)
                {
                    achievements.Unlock(Game.Systems.Meta.AchievementIds.ReachDay3);
                }
            }
        }
    }
}
