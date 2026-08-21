using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 로비 화면의 상태 기계 — 표지판과 하위 패널 중 무엇이 떠 있는지, 명판을 누르면 무엇이 열리는지.
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.1 · §5.3.
    ///
    /// <para><b>메뉴는 방 만들기 · 참가하기 · 설정 · 종료다</b>(7차 개편). 시안의 원안은
    /// "게임 시작 · 업적 · 설정 · 종료"였지만 이 게임은 1~4인 협동이라, 메뉴의 첫 갈림길이
    /// <b>"내가 방을 여는가, 남의 방에 드는가"</b>여야 한다. 표지판 원화의 명판이 <b>정확히 4장</b>이라
    /// 업적은 자리를 내줬다 — 되살릴 때는 새 진입점이 필요하다.</para>
    ///
    /// <para><b>방 만들기는 이제 패널을 열지 않는다</b>(게임 준비 화면 계획 §6.4).
    /// 누르는 즉시 호스트 세션이 서고 <b>대기실</b>(<see cref="Ready.ReadyScreenRoot"/>)이 뜬다 —
    /// 예전의 <c>Panel_Host</c>가 하던 새 여정·초대·씬 선택은 전부 그쪽으로 옮겨 갔다.
    /// <b>참가하기</b>는 그대로 패널을 연다(주소 접속). Steam 모드에서는 주소 칸 대신
    /// 오버레이 친구 목록이 참가를 맡으므로 안내 문구만 남는다.</para>
    ///
    /// <para><b>세션 서비스가 준비되기 전에는 방 만들기와 참가하기를 둘 다 잠근다</b>(§5.2).
    /// Boot 씬을 거치지 않고 Main만 열어 본 경우가 여기 걸리는데, 눌러도 아무 일이 없는 것보다
    /// 잠겨 있는 편이 정직하다.</para>
    ///
    /// <para>무엇을 실행할지는 <see cref="MenuSessionActions"/>가 알고, 어떻게 보일지는
    /// <see cref="MenuBannerView"/>와 <see cref="MenuPanel"/>이 안다. 이 클래스는 <b>둘을 잇는
    /// 순서</b>만 갖는다.</para>
    /// </summary>
    public sealed class MainMenuRoot : MonoBehaviour
    {
        /// <summary>
        /// 명판 순서 — 표지판 위에서부터.
        ///
        /// <para><b>7차에 1·2번이 바뀌었다.</b> "게임 시작 → 업적"이 <b>"방 만들기 → 참가하기"</b>가 됐다.
        /// 1~4인 협동이라 메뉴의 첫 갈림길이 "혼자 시작/업적"이 아니라 <b>"내가 방을 여는가,
        /// 남의 방에 드는가"</b>이기 때문이다. 표지판 원화의 명판이 <b>정확히 4장</b>이라
        /// 업적은 메뉴에서 빠졌다 — 되살릴 때는 자리를 새로 마련해야 한다.</para>
        /// </summary>
        private const int SlotHost = 0;
        private const int SlotJoin = 1;
        private const int SlotSettings = 2;
        private const int SlotQuit = 3;

        [Header("화면")]
        [SerializeField] private MenuBannerView _banner;
        [SerializeField] private MenuSessionActions _actions;

        [SerializeField]
        [Tooltip("방 만들기 뒤에 도착하는 대기실. 명판을 누르면 곧바로 여기로 온다.")]
        private Ready.ReadyScreenRoot _readyScreen;

        [SerializeField]
        [FormerlySerializedAs("_panelAchievements")]
        [Tooltip("참가하기 — 주소 접속. Steam 모드에서는 오버레이 친구 목록이 대신한다.")]
        private MenuPanel _panelJoin;

        [SerializeField] private MenuPanel _panelSettings;

        [Header("주변 UI")]
        [SerializeField] private NoticeBoardView _noticeBoard;
        [SerializeField] private MenuPanel _panelNotice;
        [SerializeField] private TMP_Text _versionLabel;

        [Header("참가 패널")]
        [SerializeField] private Button _joinDirect;
        [SerializeField] private TMP_InputField _address;
        [SerializeField] private GameObject _directGroup;

        [SerializeField]
        [Tooltip("배너 아래 상태 줄 — 세션 대기와 방 열기 실패 사유가 여기 뜬다.")]
        private TMP_Text _status;

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

            Subscribe(_panelJoin);
            Subscribe(_panelSettings);
            Subscribe(_panelNotice);

            if (_readyScreen != null)
            {
                // 대기실은 프리팹이라 씬의 MenuSessionActions를 직렬화로 물 수 없다.
                _readyScreen.Bind(_actions);
                _readyScreen.Left -= OnLeftRoom;
                _readyScreen.Left += OnLeftRoom;
            }

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

            Bind(_joinDirect, OnJoinDirect);
            for (int i = 0; _backButtons != null && i < _backButtons.Length; i++)
            {
                Bind(_backButtons[i], ShowBanner);
            }

            ApplyTransportMode();
            ShowBanner();
            RefreshReadiness(true);
        }

        private void OnDisable()
        {
            if (_banner != null)
            {
                _banner.PlateClicked -= OnPlateClicked;
            }

            Unsubscribe(_panelJoin);
            Unsubscribe(_panelSettings);
            Unsubscribe(_panelNotice);

            if (_readyScreen != null)
            {
                _readyScreen.Left -= OnLeftRoom;
            }

            if (_noticeBoard != null)
            {
                _noticeBoard.Clicked -= OnNoticeClicked;
            }
        }

        /// <summary>
        /// 대기실에서 나왔다. <paramref name="reason"/>이 있으면 <b>내가 나간 게 아니라 끊긴 것</b>이라
        /// 사유를 배너 아래에 남긴다 — 호스트가 방을 닫으면 게스트에게는 그것 말고 설명이 없다(§6.5).
        /// </summary>
        private void OnLeftRoom(string reason)
        {
            ShowBanner();
            if (!string.IsNullOrEmpty(reason))
            {
                ShowStatus(reason);
            }
        }

        private void OnNoticeClicked()
        {
            Open(_panelNotice);
        }

        private void Update()
        {
            RefreshReadiness(false);
            CheckRoomArrival();
            KeepFocusInsideMenu();
        }

        /// <summary>
        /// 방에 들어갔으면 대기실을 연다 — <b>게스트가 도착하는 유일한 경로</b>다(§3.3).
        ///
        /// <para>주소 접속·Steam 초대 수락·<c>+connect_lobby</c> 부팅이 전부 여기로 모인다.
        /// 셋 다 "접속이 완료됐다"는 같은 신호로 끝나므로 경로별 분기가 필요 없다.
        /// <b>초대로 게임을 켠 사람은 메뉴 배너를 볼 이유가 없다</b> — 그래서 도착 즉시 넘긴다.</para>
        ///
        /// <para>호스트는 <see cref="OnCreateRoom"/>에서 이미 열었으므로 여기 걸리지 않는다.</para>
        /// </summary>
        private void CheckRoomArrival()
        {
            if (_readyScreen == null || _readyScreen.IsOpen || _actions == null || !_actions.IsConnected)
            {
                return;
            }

            Close(_panelJoin);
            Close(_panelSettings);
            Close(_panelNotice);
            ShowStatus(string.Empty);
            ShowMenuScenery(false);
            _readyScreen.Open();
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

            // 대기실이 떠 있으면 포커스는 그쪽 것이다 — 배너는 지금 꺼져 있다.
            if (_readyScreen != null && _readyScreen.IsOpen)
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
            if (_panelJoin != null && _panelJoin.IsOpen) { return _panelJoin; }
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
            // 대기실이 떠 있는 동안은 세션이 열려 있어 IsReady가 거짓이다 — 그때 명판을 잠그면
            // 돌아왔을 때 잠긴 채로 남는다. 화면이 배너일 때만 본다.
            if (_readyScreen != null && _readyScreen.IsOpen)
            {
                return;
            }

            bool ready = _actions != null && _actions.IsReady;
            if (!force && ready == _lastReady)
            {
                return;
            }

            _lastReady = ready;
            _interactable[SlotHost] = ready;
            _interactable[SlotJoin] = ready;

            if (_banner != null)
            {
                _banner.SetInteractable(_interactable);
            }

            ShowStatus(ready ? string.Empty : "세션 서비스 초기화 대기 중...");

            if (_joinDirect != null)
            {
                _joinDirect.interactable = ready;
            }
        }

        /// <summary>배너 아래 상태 줄. 빈 문구를 주면 줄 자체가 사라진다.</summary>
        private void ShowStatus(string text)
        {
            if (_status == null)
            {
                return;
            }

            bool has = !string.IsNullOrEmpty(text);
            _status.gameObject.SetActive(has);
            if (has)
            {
                _status.text = text;
                _status.color = UiPalette.TextMuted;
            }
        }

        private void ApplyTransportMode()
        {
            bool steam = _actions != null && _actions.IsSteamMode;

            if (_directGroup != null)
            {
                _directGroup.SetActive(!steam);
            }

            if (_address != null && string.IsNullOrEmpty(_address.text))
            {
                _address.text = MenuSessionActions.DefaultAddress;
            }
        }

        private void OnPlateClicked(MenuPlateButton plate)
        {
            switch (plate.Slot)
            {
                case SlotHost:
                    OnCreateRoom();
                    break;
                case SlotJoin:
                    Open(_panelJoin);
                    break;
                case SlotSettings:
                    Open(_panelSettings);
                    break;
                case SlotQuit:
                    Quit();
                    break;
            }
        }

        /// <summary>
        /// 방 만들기 — <b>중간 패널 없이 곧바로 대기실이 열린다</b>(§6.4).
        ///
        /// <para>실패하면(포트 점유·Steam 초기화 실패) 대기실을 열지 않고 배너에 머문 채 사유만
        /// 보여 준다 — <b>반쯤 열린 방이 남지 않게</b> 한다.</para>
        /// </summary>
        private void OnCreateRoom()
        {
            if (_actions == null || _readyScreen == null)
            {
                return;
            }

            if (!_actions.OpenRoom())
            {
                ShowStatus("방을 열지 못했습니다. 포트가 이미 쓰이고 있는지 확인해 주세요.");
                return;
            }

            ShowStatus(string.Empty);
            ShowMenuScenery(false);
            _readyScreen.Open();
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

        private void Open(MenuPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            Close(_panelJoin);
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
            Close(_panelJoin);
            Close(_panelSettings);
            Close(_panelNotice);

            if (_readyScreen != null && _readyScreen.IsOpen)
            {
                _readyScreen.Close();
            }

            ShowMenuScenery(true);
            RefreshReadiness(true);

            if (_banner != null)
            {
                _banner.SetPlatesInteractable(true);
                _banner.SetInteractable(_interactable);
                _banner.FocusCurrent();
            }
        }

        /// <summary>
        /// 표지판과 공고대를 함께 여닫는다.
        ///
        /// <para><b>대기실에서는 둘 다 없다.</b> 시안에 없기도 하지만, 표지판은 로스터 패널보다
        /// 좌우로 넓어 그 위에 덮어도 가장자리가 삐져나온다. 배경과 평면 열차는 그대로 남아
        /// "같은 정차역에서 사람을 기다린다"는 감각이 이어진다(§2 · §5.3).</para>
        /// </summary>
        private void ShowMenuScenery(bool on)
        {
            if (_banner != null)
            {
                _banner.gameObject.SetActive(on);
            }

            if (_noticeBoard != null)
            {
                _noticeBoard.gameObject.SetActive(on);
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
            if (_joinDirect != null)
            {
                _joinDirect.interactable = on;
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
