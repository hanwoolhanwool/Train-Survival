using System;
using System.Collections.Generic;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Systems.Networking;
using Game.Systems.Networking.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace Game.Systems.Loading
{
    /// <summary>
    /// 인게임 진입 로딩 흐름의 주인 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §3 · §6 · §7.
    ///
    /// <para><b>Boot 씬에 상주한다</b>(§2). 씬 전환을 넘어 살아 있는 유일한 자리이기 때문이다 —
    /// NGO 씬 동기화가 <c>LoadSceneMode.Single</c>이라 대기실 씬은 로딩 한복판에 통째로 사라진다.</para>
    ///
    /// <para><b>씬 이벤트를 듣는 첫 구독처다.</b> 이 계획 전까지 <c>OnSceneEvent</c>를 듣는 코드가
    /// 프로젝트에 하나도 없었다(§0.1).</para>
    ///
    /// <para><b>무엇을 미리 만드는지는 모른다</b>(§6.2). 등록된
    /// <see cref="ISessionPreloadStep"/>을 묶음(A/B) 순서대로 돌릴 뿐이라, 새 프리로드가 생겨도
    /// 이 파일은 그대로다(OCP).</para>
    ///
    /// <para><b>동기점이 셋이다</b>(§3.4). ① 프리웜 완료 · ② 전원이 씬에 섰다
    /// (<c>LoadEventCompleted</c> — NGO가 준다) · ③ 전원이 플레이 가능하다.
    /// 우리가 만드는 것은 ①과 ③이고, 신호는 <see cref="SessionLoadState"/>가 나른다.</para>
    ///
    /// <para><b>네트워크가 없어도 굴러간다.</b> <see cref="SessionLoadState"/>가 아직 없으면
    /// (Boot만 열어 본 경우, 대기실 상태가 안 선 경우) 대기 단계를 즉시 통과한다 —
    /// 로딩 화면은 그대로 뜨고 <b>기다릴 사람만 없다.</b></para>
    ///
    /// <para><b>어느 단계도 영영 멈추지 않는다</b>(§3.5). 대기에는
    /// <see cref="LoadingReadiness.DefaultTimeoutSeconds"/>, 씬 로드에는
    /// <see cref="SceneLoadTimeoutSeconds"/>가 걸려 있다 — 무한 대기는 방을 죽인다.</para>
    /// </summary>
    public sealed class SessionLoadCoordinator : MonoBehaviour, ISessionLoadFlow, ISessionPreloadRegistry
    {
        /// <summary>씬 로드가 이 시간 안에 끝나지 않으면 로딩 화면을 걷는다(§3.5).</summary>
        public const float SceneLoadTimeoutSeconds = 20f;

        /// <summary>
        /// 게스트가 서버 지시를 더 기다려 주는 여유. 서버의 상한과 같으면 <b>게스트가 먼저 포기해</b>
        /// 서버가 막 보낸 지시를 놓친다.
        /// </summary>
        public const float GuestGraceSeconds = 5f;

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

        /// <summary>동기점 2 — NGO가 "전원이 씬에 섰다"고 알려 줬다.</summary>
        private bool _allPeersLoaded;

        /// <summary>이번 회차에 이미 보고한 단계. 같은 단계를 두 번 보고하지 않는다.</summary>
        private LoadingStage _reportedStage = LoadingStage.Idle;

        /// <summary>지금 구독 중인 씬 매니저. 세션이 서고 죽을 때마다 바뀌므로 매 프레임 맞춘다.</summary>
        private NetworkSceneManager _subscribed;

        /// <summary>화면이 올라온 시각 — 최소 표시 시간과 페이드 인의 기준이다(§8.3).</summary>
        private float _visibleSince;

        /// <summary>출발 단계가 머물러야 하는 시간. 단계에 들어설 때 한 번 정한다.</summary>
        private float _departTotal;

        public LoadingStage Stage => _stage;

        public float Progress => _progress;

        public string Status => LoadingStageText.For(_stage);

        public bool IsActive => _stage != LoadingStage.Idle;

        public float Alpha
        {
            get
            {
                if (_stage == LoadingStage.Idle || _stage == LoadingStage.Done)
                {
                    return 0f;
                }

                float now = Time.unscaledTime;
                return LoadingFadeMath.Alpha(
                    now - _visibleSince,
                    now - _stageEnteredAt,
                    _departTotal,
                    _stage == LoadingStage.Depart);
            }
        }

        public int PeerCapacity => RosterOrdering.Capacity;

        /// <summary>로딩의 네트워크 면. 아직 안 섰으면 <c>null</c> — 그때는 혼자 가는 경로다.</summary>
        private static SessionLoadState Net => SessionLoadState.Current;

        private static bool IsServer
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                return manager != null && manager.IsServer;
            }
        }

        public bool IsPeerPresent(int slot)
        {
            LobbyRoomState room = LobbyRoomState.Current;
            return room != null && room.TryGetMember(slot, out _);
        }

        public bool IsPeerReady(int slot)
        {
            LobbyRoomState room = LobbyRoomState.Current;
            SessionLoadState net = Net;
            return room != null
                && net != null
                && room.TryGetMember(slot, out ulong clientId)
                && net.HasReported(clientId);
        }

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

            // 전원에게 "준비하라" — 게스트는 이 지시로 화면을 올린다(§3.3).
            Net?.BeginStage(LoadingStage.Prepare);

            Enter(LoadingStage.Prepare);
            return true;
        }

        // ── 흐름 ─────────────────────────────────────────────────────────

        private void Update()
        {
            SyncSceneEventSubscription();
            FollowDirective();

            switch (_stage)
            {
                case LoadingStage.Prepare:
                    TickPhase(LoadingStage.WaitPrepare);
                    break;

                case LoadingStage.WaitPrepare:
                    TickWaitPrepare();
                    break;

                case LoadingStage.LoadScene:
                    TickSceneLoad();
                    break;

                case LoadingStage.Settle:
                    TickPhase(LoadingStage.WaitSettle);
                    break;

                case LoadingStage.WaitSettle:
                    TickWaitSettle();
                    break;

                case LoadingStage.Depart:
                    TickDepart();
                    break;

                case LoadingStage.Done:
                    Finish();
                    break;
            }
        }

        /// <summary>
        /// 서버 지시를 따라간다 — <b>게스트를 깨우는 자리이자, 전원이 같은 순간에 넘어가는 자리</b>다.
        /// 호스트도 같은 경로를 지난다(§7.3) — "서버니까 바로" 같은 지름길을 만들지 않는다.
        /// </summary>
        private void FollowDirective()
        {
            SessionLoadState net = Net;
            if (net == null)
            {
                return;
            }

            LoadingStage directive = net.Directive;

            // ① 예고 — 게스트가 화면을 올리는 가장 이른 시점이다(§3.3).
            if (_stage == LoadingStage.Idle && directive == LoadingStage.Prepare)
            {
                ResetRun();
                _progress = 0f;
                Enter(LoadingStage.Prepare);
                return;
            }

            // ③ 정착 — 전원이 씬에 선 뒤에만 서버가 이 지시를 준다(동기점 2).
            if (_stage == LoadingStage.LoadScene && directive == LoadingStage.Settle && _sceneLoadedLocally)
            {
                SetStageProgress(1f);
                Enter(LoadingStage.Settle);
                return;
            }

            // ④ 출발 — 진짜 게이트가 풀렸다(동기점 3).
            if (_stage == LoadingStage.WaitSettle && directive == LoadingStage.Depart)
            {
                SetStageProgress(1f);
                Enter(LoadingStage.Depart);
            }
        }

        /// <summary>단계를 넘긴다. 프리로드 묶음이 걸린 단계면 여기서 돌 목록을 붙잡는다.</summary>
        private void Enter(LoadingStage stage)
        {
            // 화면이 처음 올라오는 순간 — 최소 표시 시간의 기준점이다(§8.3).
            if (_stage == LoadingStage.Idle && stage != LoadingStage.Idle)
            {
                _visibleSince = Time.unscaledTime;
            }

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
            else if (stage == LoadingStage.Depart)
            {
                _departTotal = LoadingFadeMath.DepartSeconds(_stageEnteredAt - _visibleSince);
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

        // ── 대기 (동기점 1 · 3) ──────────────────────────────────────────

        /// <summary>동기점 1 — 느린 PC가 타일 프리웜을 마치기 전에 씬 전환이 시작되지 않게 한다.</summary>
        private void TickWaitPrepare()
        {
            ReportOnce(LoadingStage.Prepare);

            SessionLoadState net = Net;
            if (net == null)
            {
                SetStageProgress(1f);
                Enter(LoadingStage.LoadScene);
                return;
            }

            SetStageProgress(LoadingReadiness.Progress(net.MemberCount, net.ReportedMemberCount));

            // 게스트는 씬 로드가 시작되는 것(Load 이벤트)으로 다음 단계를 안다.
            if (!IsServer)
            {
                return;
            }

            float elapsed = Time.unscaledTime - _stageEnteredAt;
            if (!LoadingReadiness.ShouldAdvance(
                    net.MemberCount, net.ReportedMemberCount, elapsed, LoadingReadiness.DefaultTimeoutSeconds))
            {
                return;
            }

            WarnIfForced(net, elapsed, "예고");
            SetStageProgress(1f);
            Enter(LoadingStage.LoadScene);
        }

        /// <summary>동기점 3 — <b>진짜 출발 게이트</b>. 여기를 지나면 전원이 플레이 가능하다.</summary>
        private void TickWaitSettle()
        {
            ReportOnce(LoadingStage.Settle);

            SessionLoadState net = Net;
            if (net == null)
            {
                SetStageProgress(1f);
                Enter(LoadingStage.Depart);
                return;
            }

            SetStageProgress(LoadingReadiness.Progress(net.MemberCount, net.ReportedMemberCount));

            float elapsed = Time.unscaledTime - _stageEnteredAt;

            if (IsServer)
            {
                if (LoadingReadiness.ShouldAdvance(
                        net.MemberCount, net.ReportedMemberCount, elapsed, LoadingReadiness.DefaultTimeoutSeconds))
                {
                    WarnIfForced(net, elapsed, "정착");
                    net.BeginStage(LoadingStage.Depart);
                }

                return;
            }

            // 게스트는 지시를 기다린다. 서버보다 늦게 포기해야 막 보낸 지시를 놓치지 않는다.
            if (LoadingReadiness.IsTimedOut(elapsed, LoadingReadiness.DefaultTimeoutSeconds + GuestGraceSeconds))
            {
                GameLog.Warn(LogCategory.Session, "출발 지시가 오지 않아 로딩 화면을 걷습니다.");
                SetStageProgress(1f);
                Enter(LoadingStage.Depart);
            }
        }

        /// <summary>
        /// ④ 출발 — 최소 표시 시간을 채우고 페이드 아웃이 끝날 때까지 머문다(§8.3).
        /// <b>여기서 서두르면 화면이 깜빡이기만 한다</b> — 빨라 보이는 게 아니라 고장으로 읽힌다.
        /// </summary>
        private void TickDepart()
        {
            float elapsed = Time.unscaledTime - _stageEnteredAt;

            SetStageProgress(_departTotal <= 0f ? 1f : Mathf.Clamp01(elapsed / _departTotal));

            if (elapsed >= _departTotal)
            {
                Enter(LoadingStage.Done);
            }
        }

        /// <summary>같은 단계를 두 번 보고하지 않는다. 호스트도 게스트와 같은 경로를 지난다(§7.3).</summary>
        private void ReportOnce(LoadingStage stage)
        {
            if (_reportedStage == stage)
            {
                return;
            }

            _reportedStage = stage;
            Net?.Report(stage);
        }

        private static void WarnIfForced(SessionLoadState net, float elapsed, string label)
        {
            if (LoadingReadiness.IsSatisfied(net.MemberCount, net.ReportedMemberCount))
            {
                return;
            }

            GameLog.Warn(
                LogCategory.Session,
                $"{label} 대기가 {elapsed:0.0}초를 넘어 강제로 진행합니다 " +
                $"({net.ReportedMemberCount}/{net.MemberCount}명 보고).");
        }

        // ── 씬 로드 (동기점 2) ───────────────────────────────────────────

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

            float elapsed = Time.unscaledTime - _stageEnteredAt;
            bool timedOut = elapsed > SceneLoadTimeoutSeconds;
            SessionLoadState net = Net;

            if (_sceneLoadedLocally)
            {
                SetStageProgress(1f);

                if (net == null)
                {
                    Enter(LoadingStage.Settle);
                    return;
                }

                // 동기점 2 — 서버는 전원이 씬에 선 뒤에만 정착을 지시한다.
                if (IsServer && net.Directive != LoadingStage.Settle && (_allPeersLoaded || timedOut))
                {
                    if (!_allPeersLoaded)
                    {
                        GameLog.Warn(LogCategory.Session, "전원의 씬 로드 완료를 기다리다 상한을 넘어 진행합니다.");
                    }

                    net.BeginStage(LoadingStage.Settle);
                }
                else if (!IsServer && timedOut && net.Directive != LoadingStage.Settle)
                {
                    GameLog.Warn(LogCategory.Session, "정착 지시가 오지 않아 혼자 진행합니다.");
                    Enter(LoadingStage.Settle);
                }

                // 실제 전이는 FollowDirective가 한다 — 전원이 같은 지시로 넘어간다.
                return;
            }

            // AsyncOperation.progress는 활성화 직전 0.9에서 멈춘다 — 0~0.9를 0~1로 편다.
            // NGO는 allowSceneActivation을 열어 주지 않으므로 0.9에 오래 머무르지는 않는다.
            SetStageProgress(_sceneOp == null ? 0f : Mathf.Clamp01(_sceneOp.progress / 0.9f));

            if (timedOut)
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
            Net?.BeginStage(LoadingStage.Idle);
            ResetRun();
            _stage = LoadingStage.Idle;
            _progress = 0f;
            aborted?.Invoke(reason);
        }

        private void Finish()
        {
            // 지시를 거둬야 다음 여정이 같은 신호로 다시 시작할 수 있다.
            if (IsServer)
            {
                Net?.BeginStage(LoadingStage.Idle);
            }

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
            _allPeersLoaded = false;
            _reportedStage = LoadingStage.Idle;
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
                    // 예고 신호를 못 받은 채(네트워크 상태 없이) 끌려온 경우 — 여기서라도 화면을 올린다.
                    if (_stage == LoadingStage.Idle)
                    {
                        ResetRun();
                        _sceneLoadRequested = true;
                        _progress = 0f;
                        Enter(LoadingStage.LoadScene);
                    }
                    else if (_stage < LoadingStage.LoadScene)
                    {
                        // 게스트가 ①에서 기다리는 중이었다 — 씬 로드가 시작됐으니 ②로 간다.
                        SetStageProgress(1f);
                        _sceneLoadRequested = true;
                        Enter(LoadingStage.LoadScene);
                    }

                    _sceneOp = sceneEvent.AsyncOperation;
                    break;
                }

                case SceneEventType.LoadComplete:
                {
                    // 서버는 클라이언트마다 하나씩 받는다 — 내 것만 센다.
                    NetworkManager manager = NetworkManager.Singleton;
                    if (manager != null && sceneEvent.ClientId != manager.LocalClientId)
                    {
                        return;
                    }

                    _sceneLoadedLocally = true;
                    break;
                }

                case SceneEventType.LoadEventCompleted:
                {
                    // 동기점 2 — NGO가 공짜로 준다.
                    _allPeersLoaded = true;
                    break;
                }
            }
        }
    }
}
