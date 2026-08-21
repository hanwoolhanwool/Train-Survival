using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 로비 화면의 상태 기계 — 표지판과 하위 패널 중 무엇이 떠 있는지, 명판을 누르면 무엇이 열리는지.
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.1 · §5.3.
    ///
    /// <para><b>명판 4장으로는 부족하다.</b> 시안의 메뉴는 게임 시작·업적·설정·종료뿐인데 이 게임은
    /// 1~4인 협동이라 "참가"가 없다. 그래서 <b>게임 시작이 하위 패널을 연다</b> — 새 여정 / 친구 참가 /
    /// (개발 빌드) 인게임 씬 선택.</para>
    ///
    /// <para><b>세션 서비스가 준비되기 전에는 게임 시작을 잠근다</b>(§5.2). Boot 씬을 거치지 않고
    /// Main만 열어 본 경우가 여기 걸리는데, 눌러도 아무 일이 없는 것보다 잠겨 있는 편이 정직하다.</para>
    ///
    /// <para>무엇을 실행할지는 <see cref="MenuSessionActions"/>가 알고, 어떻게 보일지는
    /// <see cref="MenuBannerView"/>와 <see cref="MenuPanel"/>이 안다. 이 클래스는 <b>둘을 잇는
    /// 순서</b>만 갖는다.</para>
    /// </summary>
    public sealed class MainMenuRoot : MonoBehaviour
    {
        /// <summary>명판 순서 — 시안의 위에서부터.</summary>
        private const int SlotStart = 0;
        private const int SlotAchievements = 1;
        private const int SlotSettings = 2;
        private const int SlotQuit = 3;

        [Header("화면")]
        [SerializeField] private MenuBannerView _banner;
        [SerializeField] private MenuSessionActions _actions;
        [SerializeField] private MenuPanel _panelPlay;
        [SerializeField] private MenuPanel _panelAchievements;
        [SerializeField] private MenuPanel _panelSettings;

        [Header("주변 UI")]
        [SerializeField] private NoticeBoardView _noticeBoard;
        [SerializeField] private MenuPanel _panelNotice;
        [SerializeField] private TMP_Text _versionLabel;

        [Header("여정 시작 패널")]
        [SerializeField] private Button _newJourney;
        [SerializeField] private Button _invite;
        [SerializeField] private Button _joinDirect;
        [SerializeField] private TMP_InputField _address;
        [SerializeField] private GameObject _steamGroup;
        [SerializeField] private GameObject _directGroup;
        [SerializeField] private TMP_Text _status;

        [Header("개발 빌드 전용")]
        [SerializeField] private GameObject _devGroup;
        [SerializeField] private Button _sceneToggle;
        [SerializeField] private TMP_Text _sceneToggleLabel;

        [Header("공통")]
        [SerializeField] private Button[] _backButtons;

        private readonly bool[] _interactable = new bool[MenuPlateLayout.SlotCount];
        private bool _lastReady;

        private void Awake()
        {
            for (int i = 0; i < _interactable.Length; i++)
            {
                _interactable[i] = true;
            }
        }

        private void OnEnable()
        {
            if (_banner != null)
            {
                _banner.PlateClicked += OnPlateClicked;
            }

            Subscribe(_panelPlay);
            Subscribe(_panelAchievements);
            Subscribe(_panelSettings);
            Subscribe(_panelNotice);

            if (_noticeBoard != null)
            {
                _noticeBoard.Clicked -= OnNoticeClicked;
                _noticeBoard.Clicked += OnNoticeClicked;
            }

            if (_versionLabel != null)
            {
                _versionLabel.text = "v" + NoticeBoardView.Version;
                _versionLabel.color = UiPalette.TextMuted;
            }

            Bind(_newJourney, OnNewJourney);
            Bind(_invite, OnInvite);
            Bind(_joinDirect, OnJoinDirect);
            Bind(_sceneToggle, OnToggleScene);
            for (int i = 0; _backButtons != null && i < _backButtons.Length; i++)
            {
                Bind(_backButtons[i], ShowBanner);
            }

            ApplyTransportMode();
            RefreshSceneLabel();
            ShowBanner();
            RefreshReadiness(true);
        }

        private void OnDisable()
        {
            if (_banner != null)
            {
                _banner.PlateClicked -= OnPlateClicked;
            }

            Unsubscribe(_panelPlay);
            Unsubscribe(_panelAchievements);
            Unsubscribe(_panelSettings);
            Unsubscribe(_panelNotice);

            if (_noticeBoard != null)
            {
                _noticeBoard.Clicked -= OnNoticeClicked;
            }
        }

        private void OnNoticeClicked()
        {
            Open(_panelNotice);
        }

        private void Update()
        {
            RefreshReadiness(false);
            KeepFocusInsideMenu();
        }

        /// <summary>
        /// 선택된 항목이 없으면 되찾아 온다 — <b>키보드·게임패드가 죽지 않게 하는 최후의 보루</b>다.
        ///
        /// <para><b>왜 필요한가</b>(7차 실측). <see cref="OnEnable"/>이 이미 시작 슬롯을 선택하지만,
        /// <b>Boot → Main 전환에서는 그 선택이 살아남지 못한다</b> — Main의 컴포넌트가 깨어나는
        /// 시점에 <see cref="EventSystem.current"/>가 아직 곧 파괴될 Boot 쪽이거나 비어 있어서다.
        /// 실제로 Main 단독 실행은 선택이 잡혀 있었고 Boot를 거치면 <c>null</c>이었다.
        /// 선택이 비면 유니티 입력 모듈은 이동·확인·취소를 <b>한 줄도 보내지 않는다</b> —
        /// "UI를 한 번 클릭해야 방향키가 듣는다"는 증상의 정체가 이것이다.</para>
        ///
        /// <para>배경을 클릭해 선택이 풀리는 경우(입력 모듈의 기본 동작)도 같이 막힌다.
        /// 메뉴 화면에서는 <b>언제나 무언가가 선택돼 있어야 한다.</b></para>
        /// </summary>
        private void KeepFocusInsideMenu()
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return;
            }

            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                return;
            }

            MenuPanel open = OpenPanel();
            if (open != null)
            {
                open.Open();
            }
            else if (_banner != null)
            {
                _banner.FocusCurrent();
            }
        }

        /// <summary>지금 열려 있는 패널. 없으면 <c>null</c>.</summary>
        private MenuPanel OpenPanel()
        {
            if (_panelPlay != null && _panelPlay.IsOpen) { return _panelPlay; }
            if (_panelAchievements != null && _panelAchievements.IsOpen) { return _panelAchievements; }
            if (_panelSettings != null && _panelSettings.IsOpen) { return _panelSettings; }
            if (_panelNotice != null && _panelNotice.IsOpen) { return _panelNotice; }
            return null;
        }

        /// <summary>
        /// 세션 서비스는 Boot 씬에서 등록된다 — Main을 단독으로 열면 영영 오지 않을 수도 있고,
        /// 정상 흐름에서도 한 프레임 뒤에 온다. 그래서 매 프레임 값을 보고 바뀔 때만 반영한다.
        /// </summary>
        private void RefreshReadiness(bool force)
        {
            bool ready = _actions != null && _actions.IsReady;
            if (!force && ready == _lastReady)
            {
                return;
            }

            _lastReady = ready;
            _interactable[SlotStart] = ready;

            if (_banner != null)
            {
                _banner.SetInteractable(_interactable);
            }

            if (_status != null)
            {
                _status.gameObject.SetActive(!ready);
                _status.text = "세션 서비스 초기화 대기 중...";
                _status.color = UiPalette.TextMuted;
            }

            if (_newJourney != null)
            {
                _newJourney.interactable = ready;
            }

            if (_joinDirect != null)
            {
                _joinDirect.interactable = ready;
            }
        }

        private void ApplyTransportMode()
        {
            bool steam = _actions != null && _actions.IsSteamMode;

            if (_steamGroup != null)
            {
                _steamGroup.SetActive(steam);
            }

            if (_directGroup != null)
            {
                _directGroup.SetActive(!steam);
            }

            if (_invite != null)
            {
                _invite.interactable = steam && _actions.IsSteamReady;
            }

            if (_address != null && string.IsNullOrEmpty(_address.text))
            {
                _address.text = MenuSessionActions.DefaultAddress;
            }

            bool dev = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            dev = true;
#endif
            if (_devGroup != null)
            {
                _devGroup.SetActive(dev);
            }
        }

        private void OnPlateClicked(MenuPlateButton plate)
        {
            switch (plate.Slot)
            {
                case SlotStart:
                    Open(_panelPlay);
                    break;
                case SlotAchievements:
                    Open(_panelAchievements);
                    break;
                case SlotSettings:
                    Open(_panelSettings);
                    break;
                case SlotQuit:
                    Quit();
                    break;
            }
        }

        private void OnNewJourney()
        {
            if (_actions == null || !_actions.StartNewJourney())
            {
                return;
            }

            // 씬 로드가 시작됐다 — 이 화면은 곧 사라지므로 더 누르지 못하게 잠근다.
            SetPanelsInteractable(false);
        }

        private void OnInvite()
        {
            if (_actions != null)
            {
                _actions.InviteFriends();
            }
        }

        private void OnJoinDirect()
        {
            if (_actions == null)
            {
                return;
            }

            string address = _address != null ? _address.text : MenuSessionActions.DefaultAddress;
            if (_actions.JoinDirect(address, MenuSessionActions.DefaultPort))
            {
                SetPanelsInteractable(false);
            }
        }

        private void OnToggleScene()
        {
            if (_actions == null)
            {
                return;
            }

            _actions.ToggleGameplayScene();
            RefreshSceneLabel();
        }

        private void RefreshSceneLabel()
        {
            if (_sceneToggleLabel != null && _actions != null)
            {
                _sceneToggleLabel.text = $"인게임 씬: {_actions.GameplayScene}  →  {_actions.OtherGameplayScene}";
            }
        }

        private void Open(MenuPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            Close(_panelPlay);
            Close(_panelAchievements);
            Close(_panelSettings);
            Close(_panelNotice);
            panel.Open();

            if (_banner != null)
            {
                _banner.SetPlatesInteractable(false);
            }
        }

        /// <summary>패널을 모두 닫고 표지판으로 돌아간다 — 포커스도 돌려준다.</summary>
        public void ShowBanner()
        {
            Close(_panelPlay);
            Close(_panelAchievements);
            Close(_panelSettings);
            Close(_panelNotice);

            if (_banner != null)
            {
                _banner.SetPlatesInteractable(true);
                _banner.SetInteractable(_interactable);
                _banner.FocusCurrent();
            }
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetPanelsInteractable(bool on)
        {
            SetGroup(_newJourney, on);
            SetGroup(_invite, on);
            SetGroup(_joinDirect, on);
            SetGroup(_sceneToggle, on);
        }

        private static void SetGroup(Button button, bool on)
        {
            if (button != null)
            {
                button.interactable = on;
            }
        }

        private static void Close(MenuPanel panel)
        {
            if (panel != null)
            {
                panel.Close();
            }
        }

        private void Subscribe(MenuPanel panel)
        {
            if (panel != null)
            {
                panel.Cancelled -= ShowBanner;
                panel.Cancelled += ShowBanner;
            }
        }

        private void Unsubscribe(MenuPanel panel)
        {
            if (panel != null)
            {
                panel.Cancelled -= ShowBanner;
            }
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }
}
