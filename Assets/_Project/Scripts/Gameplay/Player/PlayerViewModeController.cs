using Game.Core.Events;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 시점 모드의 단일 출처 (1인칭 통합 시점 전환 계획 §3.1) — <b>상태만 소유하고 아무것도
    /// 직접 만지지 않는다</b> (SRP). 몸 렌더·카메라 파라미터·파지 프로파일·뷰모델 가시성은
    /// 각 표현 컴포넌트가 이 값을 읽어 자기 몫을 적용한다 (OCP — 소비자가 늘어도 이 클래스는 무변경).
    ///
    /// <para><b>조작 권한이 없는 인스턴스에서는 값이 절대 바뀌지 않는다</b> (<see cref="CanDrive"/>).
    /// 원격 프록시의 컨트롤러는 기본 모드에 머물러 있으므로, 원격 표현은 모드와 무관하게
    /// 현행 그대로 동작한다 (§4.2 — 모드가 원격에 새지 않는다는 보증의 코드 근거).</para>
    ///
    /// <para>네트워크 복제 없음 (기술 확정 ⑥) — 호스트와 클라이언트가 서로 다른 모드로 붙을 수 있고,
    /// 그것이 QA 비교 매트릭스 B·C의 전제다 (§4.3).</para>
    /// </summary>
    public sealed class PlayerViewModeController : MonoBehaviour
    {
        [SerializeField] private PlayerViewSettings _settings;

        private NetworkObject _networkObject;
        private bool _publishedInitial;

        /// <summary>현재 시점 모드 — 표현 컴포넌트가 매 프레임 읽어도 되는 값이다.</summary>
        public PlayerViewMode Mode { get; private set; }

        /// <summary>표현 컴포넌트가 함께 참조하는 설정 에셋 (카메라 파라미터·머리 은닉 계수).</summary>
        public PlayerViewSettings Settings => _settings;

        /// <summary>
        /// 이 인스턴스가 이 피어의 조작 대상인가. 네트워크 오브젝트가 없으면(뷰랩·단독 씬) 참으로 본다.
        /// 스폰 전에는 거짓이다 — 소유권이 확정되기 전에 발행하면 원격 프록시도 한 번 발행해 버린다.
        /// </summary>
        public bool CanDrive => _networkObject == null || (_networkObject.IsSpawned && _networkObject.IsOwner);

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            Mode = _settings != null ? _settings.DefaultMode : PlayerViewMode.SplitFpTp;
        }

        private void Update()
        {
            if (!CanDrive)
            {
                return;
            }

            // 스폰 직후 1회 — HUD처럼 컨트롤러를 참조하지 않는 구독자에게 초기 모드를 알린다.
            if (!_publishedInitial)
            {
                _publishedInitial = true;
                Publish();
            }

            if (_settings == null || !_settings.DebugToggleEnabled)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
            {
                SetMode(Mode == PlayerViewMode.SplitFpTp
                    ? PlayerViewMode.UnifiedFirstPerson
                    : PlayerViewMode.SplitFpTp);
            }
        }

        /// <summary>
        /// 모드 지정 — 같은 값이면 아무 일도 하지 않는다. 뷰랩·테스트에서도 이 경로로 들어온다
        /// (전환 처리를 한 곳에 모아 두어야 §4.1의 멱등성을 한 자리에서 보증할 수 있다).
        /// </summary>
        public void SetMode(PlayerViewMode mode)
        {
            if (Mode == mode && _publishedInitial)
            {
                return;
            }

            Mode = mode;
            _publishedInitial = true;
            Publish();
        }

        private void Publish()
        {
            Debug.Log($"[PlayerViewModeController] 시점 모드 = {Mode}");
            EventBus<PlayerViewModeChangedLocalEvent>.Publish(new PlayerViewModeChangedLocalEvent(Mode));
        }
    }
}
