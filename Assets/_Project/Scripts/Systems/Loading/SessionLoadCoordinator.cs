using System;
using System.Collections.Generic;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Systems.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Game.Systems.Loading
{
    /// <summary>
    /// 인게임 진입 로딩 흐름의 주인 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §3 · §6.
    ///
    /// <para><b>Boot 씬에 상주한다</b>(§2). 씬 전환을 넘어 살아 있는 유일한 자리이기 때문이다 —
    /// NGO 씬 동기화가 <c>LoadSceneMode.Single</c>이라 대기실 씬은 로딩 한복판에 통째로 사라진다.</para>
    ///
    /// <para><b>씬 이벤트를 듣는 첫 구독처다.</b> 이 계획 전까지 <c>OnSceneEvent</c>를 듣는 코드가
    /// 프로젝트에 하나도 없었다(§0.1) — "씬 로드가 지금 몇 %인가"를 아는 곳이 여기 말고 없다.</para>
    ///
    /// <para><b>무엇을 미리 만드는지는 모른다</b>(§6.2). 등록된
    /// <see cref="ISessionPreloadStep"/>을 묶음(A/B) 순서대로 돌릴 뿐이라, 새 프리로드가 생겨도
    /// 이 파일은 그대로다(OCP). 1차에는 등록된 스텝이 하나도 없다 — 뼈대만 선다.</para>
    ///
    /// <para><b>1차에 없는 것</b>: 전원 대기(4차)와 프리로드(2·3차)다. 대기 단계
    /// (<see cref="LoadingStage.WaitPrepare"/> · <see cref="LoadingStage.WaitSettle"/>)는
    /// <b>자리를 지킨 채 즉시 통과</b>한다 — 단계를 나중에 끼워 넣으면 가중치가 통째로 흔들리므로
    /// 처음부터 자리를 비워 둔다.</para>
    ///
    /// <para><b>게스트는 씬 이벤트로 깨어난다.</b> 4차의 "준비하라" 신호가 없는 동안은
    /// <see cref="SceneEventType.Load"/>가 게스트가 받는 가장 이른 신호다. 계획 §3.3이 지적한 대로
    /// 이건 <b>늦은 신호</b>라 대기실 UI가 걷히는 한 프레임이 비칠 수 있다 — 4차가 고칠 자리다.</para>
    /// </summary>
    public sealed class SessionLoadCoordinator : MonoBehaviour, ISessionLoadFlow, ISessionPreloadRegistry
    {
        /// <summary>
        /// 씬 로드가 이 시간 안에 끝나지 않으면 로딩 화면을 걷는다(§3.5 — 무한 대기는 방을 죽인다).
        /// 단계별 타임아웃 전반은 4차 몫이고, 여기서는 <b>화면이 영영 안 걷히는 것</b>만 막는다.
        /// </summary>
        public const float SceneLoadTimeoutSeconds = 20f;

        /// <summary>돌고 있는 스텝 하나 — 총량을 단계 시작 시점에 붙잡아 둔다.</summary>
        private sealed class RunningStep
        {
            public ISessionPreloadStep Step;
            public int Total;

            /// <summary>예외를 던진 스텝. 끝난 것으로 세고 더 밀지 않는다(§3.5).</summary>
            public bool Failed;
        }

        private readonly List<ISessionPreloadStep> _steps = new List<ISessionPreloadStep>();
        private readonly List<RunningStep> _running = new List<RunningStep>();

        private LoadingStage _stage = LoadingStage.Idle;
        private float _progress;
        private float _stageEnteredAt;

        private Func<bool> _startSceneLoad;
        private Action<string> _onAborted;

        private AsyncOperation _sceneOp;
        private bool _sceneLoadRequested;
        private bool _sceneLoadedLocally;

        /// <summary>지금 구독 중인 씬 매니저. 세션이 서고 죽을 때마다 바뀌므로 매 프레임 맞춘다.</summary>
        private NetworkSceneManager _subscribed;

        public LoadingStage Stage => _stage;

        public float Progress => _progress;

        public string Status => LoadingStageText.For(_stage);

        public bool IsActive => _stage != LoadingStage.Idle;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        // 등록을 OnEnable에서 하는 이유는 GameBootstrapper와 같다 — 플레이 중 도메인 리로드가
        // ServiceLocator의 정적 상태를 비워도 이 오브젝트가 살아 있으면 다시 등록된다.
        private void OnEnable()
        {
            if (!ServiceLocator.IsRegistered<ISessionLoadFlow>())
            {
                ServiceLocator.Register<ISessionLoadFlow>(this);
            }

            if (!ServiceLocator.IsRegistered<ISessionPreloadRegistry>())
            {
                ServiceLocator.Register<ISessionPreloadRegistry>(this);
            }
        }

        private void OnDestroy()
        {
            if (_subscribed != null)
            {
                _subscribed.OnSceneEvent -= HandleSceneEvent;
                _subscribed = null;
            }

            // 내가 등록한 것만 거둔다 — 도메인 리로드로 다른 인스턴스가 자리를 잡았을 수 있다.
            if (ServiceLocator.TryGet(out ISessionLoadFlow flow) && ReferenceEquals(flow, this))
            {
                ServiceLocator.Unregister<ISessionLoadFlow>();
            }

            if (ServiceLocator.TryGet(out ISessionPreloadRegistry registry) && ReferenceEquals(registry, this))
            {
                ServiceLocator.Unregister<ISessionPreloadRegistry>();
            }
        }

        // ── ISessionPreloadRegistry ──────────────────────────────────────

        public void Register(ISessionPreloadStep step)
        {
            if (step == null || _steps.Contains(step))
            {
                return;
            }

            _steps.Add(step);
        }

        public void Unregister(ISessionPreloadStep step)
        {
            if (step == null)
            {
                return;
            }

            _steps.Remove(step);

            // 돌고 있는 중이면 목록에서도 빼야 한다 — 인게임 씬의 스텝은 씬과 함께 사라진다.
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_running[i].Step, step))
                {
                    _running.RemoveAt(i);
                }
            }
        }

        // ── ISessionLoadFlow ─────────────────────────────────────────────

        public bool Begin(Func<bool> startSceneLoad, Action<string> onAborted)
        {
            if (startSceneLoad == null)
            {
                GameLog.Error(LogCategory.Session, "씬 전환 요청이 비어 있어 로딩을 시작할 수 없습니다.");
                return false;
            }

            if (_stage != LoadingStage.Idle)
            {
                return false;
            }

            ResetRun();
            _startSceneLoad = startSceneLoad;
            _onAborted = onAborted;
            _progress = 0f;
            Enter(LoadingStage.Prepare);
            return true;
        }

        // ── 흐름 ─────────────────────────────────────────────────────────

        private void Update()
        {
            SyncSceneEventSubscription();

            switch (_stage)
            {
                case LoadingStage.Prepare:
                    TickPhase(LoadingStage.WaitPrepare);
                    break;

                // 전원 대기는 4차가 채운다. 그때까지는 자리만 지키고 곧바로 통과한다(§9 1차).
                case LoadingStage.WaitPrepare:
                    SetStageProgress(1f);
                    Enter(LoadingStage.LoadScene);
                    break;

                case LoadingStage.LoadScene:
                    TickSceneLoad();
                    break;

                case LoadingStage.Settle:
                    TickPhase(LoadingStage.WaitSettle);
                    break;

                case LoadingStage.WaitSettle:
                    SetStageProgress(1f);
                    Enter(LoadingStage.Depart);
                    break;

                // 최소 표시 시간과 페이드 아웃은 5차 몫이다.
                case LoadingStage.Depart:
                    SetStageProgress(1f);
                    Enter(LoadingStage.Done);
                    break;

                case LoadingStage.Done:
                    Finish();
                    break;
            }
        }

        /// <summary>단계를 넘긴다. 프리로드 묶음이 걸린 단계면 여기서 돌 목록을 붙잡는다.</summary>
        private void Enter(LoadingStage stage)
        {
            _stage = stage;
            _stageEnteredAt = Time.unscaledTime;

            if (stage == LoadingStage.Prepare)
            {
                BeginPhase(PreloadPhase.BeforeSceneLoad);
            }
            else if (stage == LoadingStage.Settle)
            {
                BeginPhase(PreloadPhase.AfterSceneLoad);
            }

            SetStageProgress(0f);
        }

        /// <summary>
        /// 이 묶음에서 돌 스텝을 고르고 <b>총량을 지금 확정한다</b>. 총량이 매 프레임 바뀌면
        /// 진행바가 앞뒤로 흔들린다 — <see cref="ISessionPreloadStep.Total"/>을 한 번만 읽는 이유다.
        /// </summary>
        private void BeginPhase(PreloadPhase phase)
        {
            _running.Clear();

            for (int i = 0; i < _steps.Count; i++)
            {
                ISessionPreloadStep step = _steps[i];
                if (step == null || step.Phase != phase)
                {
                    continue;
                }

                int total;
                try
                {
                    total = Mathf.Max(0, step.Total);
                }
                catch (Exception e)
                {
                    GameLog.Warn(LogCategory.Session, $"프리로드 스텝의 총량을 읽지 못해 건너뜁니다: {e.Message}");
                    continue;
                }

                if (total <= 0)
                {
                    continue;
                }

                _running.Add(new RunningStep { Step = step, Total = total });
            }
        }

        /// <summary>한 프레임 몫을 민 뒤 진행률을 갱신한다. 다 끝났으면 다음 단계로 넘어간다.</summary>
        private void TickPhase(LoadingStage next)
        {
            int total = 0;
            int done = 0;

            for (int i = 0; i < _running.Count; i++)
            {
                RunningStep running = _running[i];
                total += running.Total;

                if (running.Failed)
                {
                    done += running.Total;
                    continue;
                }

                if (ReadDone(running) < running.Total)
                {
                    TryAdvance(running);
                }

                done += ReadDone(running);
            }

            if (total <= 0 || done >= total)
            {
                SetStageProgress(1f);
                Enter(next);
                return;
            }

            SetStageProgress((float)done / total);
        }

        private static int ReadDone(RunningStep running)
        {
            if (running.Failed)
            {
                return running.Total;
            }

            try
            {
                return Mathf.Clamp(running.Step.Done, 0, running.Total);
            }
            catch (Exception e)
            {
                GameLog.Warn(LogCategory.Session, $"프리로드 스텝의 진행량을 읽지 못해 건너뜁니다: {e.Message}");
                running.Failed = true;
                return running.Total;
            }
        }

        // 프리로드 실패는 렉이지 게임 중단 사유가 아니다(§3.5) — 그 스텝만 끝난 것으로 치고 간다.
        private static void TryAdvance(RunningStep running)
        {
            try
            {
                running.Step.Advance();
            }
            catch (Exception e)
            {
                GameLog.Warn(LogCategory.Session, $"프리로드 스텝이 실패해 건너뜁니다: {e.Message}");
                running.Failed = true;
            }
        }

        private void TickSceneLoad()
        {
            // 호스트만 씬 전환을 요청한다. 게스트는 콜백이 없고 씬 이벤트로 끌려온다.
            if (_startSceneLoad != null && !_sceneLoadRequested)
            {
                _sceneLoadRequested = true;

                bool started;
                try
                {
                    started = _startSceneLoad();
                }
                catch (Exception e)
                {
                    started = false;
                    GameLog.Error(LogCategory.Session, $"씬 전환 요청이 예외로 실패했습니다: {e.Message}");
                }

                if (!started)
                {
                    Abort("출발하지 못했습니다. 잠시 뒤 다시 시도해 주세요.");
                    return;
                }
            }

            if (_sceneLoadedLocally)
            {
                SetStageProgress(1f);
                Enter(LoadingStage.Settle);
                return;
            }

            // AsyncOperation.progress는 활성화 직전 0.9에서 멈춘다 — 0~0.9를 0~1로 편다.
            // NGO는 allowSceneActivation을 열어 주지 않으므로 0.9에 오래 머무르지는 않는다.
            SetStageProgress(_sceneOp == null ? 0f : Mathf.Clamp01(_sceneOp.progress / 0.9f));

            if (Time.unscaledTime - _stageEnteredAt > SceneLoadTimeoutSeconds)
            {
                GameLog.Warn(
                    LogCategory.Session,
                    $"씬 로드가 {SceneLoadTimeoutSeconds:0}초 안에 끝나지 않아 로딩 화면을 걷습니다.");
                SetStageProgress(1f);
                Enter(LoadingStage.Done);
            }
        }

        /// <summary>①로 되돌린다(§3.5) — 아직 대기실 씬이 살아 있으므로 부르는 쪽이 화면을 되살린다.</summary>
        private void Abort(string reason)
        {
            GameLog.Warn(LogCategory.Session, $"인게임 진입 로딩을 되돌립니다: {reason}");

            Action<string> aborted = _onAborted;
            ResetRun();
            _stage = LoadingStage.Idle;
            _progress = 0f;
            aborted?.Invoke(reason);
        }

        private void Finish()
        {
            ResetRun();
            _stage = LoadingStage.Idle;
            _progress = 0f;
        }

        private void ResetRun()
        {
            _running.Clear();
            _startSceneLoad = null;
            _onAborted = null;
            _sceneOp = null;
            _sceneLoadRequested = false;
            _sceneLoadedLocally = false;
        }

        private void SetStageProgress(float stageProgress)
        {
            _progress = LoadingProgressMath.Monotonic(
                _progress, LoadingProgressMath.Combine(_stage, stageProgress));
        }

        // ── 씬 이벤트 ────────────────────────────────────────────────────

        /// <summary>
        /// 구독 대상을 지금 세션에 맞춘다. <see cref="NetworkSceneManager"/>는 세션이 설 때 생기고
        /// 끝나면 사라지므로, 한 번 붙여 두는 것으로는 재시작을 못 따라간다.
        /// </summary>
        private void SyncSceneEventSubscription()
        {
            NetworkManager manager = NetworkManager.Singleton;
            NetworkSceneManager current = manager == null ? null : manager.SceneManager;

            if (ReferenceEquals(current, _subscribed))
            {
                return;
            }

            if (_subscribed != null)
            {
                _subscribed.OnSceneEvent -= HandleSceneEvent;
            }

            _subscribed = current;

            if (_subscribed != null)
            {
                _subscribed.OnSceneEvent += HandleSceneEvent;
            }
        }

        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            if (!GameplaySceneRoute.IsGameplayScene(sceneEvent.SceneName))
            {
                return;
            }

            switch (sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                {
                    // 게스트는 여기서 처음 안다 — 호스트는 이미 LoadScene 단계라 이 가지를 타지 않는다.
                    if (_stage == LoadingStage.Idle)
                    {
                        ResetRun();
                        _sceneLoadRequested = true;
                        _progress = 0f;
                        Enter(LoadingStage.LoadScene);
                    }

                    _sceneOp = sceneEvent.AsyncOperation;
                    break;
                }

                case SceneEventType.LoadComplete:
                {
                    // 서버는 클라이언트마다 하나씩 받는다 — 내 것만 센다(전원 대기는 4차).
                    NetworkManager manager = NetworkManager.Singleton;
                    if (manager != null && sceneEvent.ClientId != manager.LocalClientId)
                    {
                        return;
                    }

                    _sceneLoadedLocally = true;
                    break;
                }
            }
        }
    }
}
