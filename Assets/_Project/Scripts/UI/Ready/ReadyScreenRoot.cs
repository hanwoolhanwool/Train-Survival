using System;
using Game.Core.Services;
using Game.Systems.Networking.Lobby;
using Game.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Ready
{
    /// <summary>
    /// 준비 화면의 상태 기계 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §6.1 · §6.4 · §6.5.
    ///
    /// <para><b>방이 열려 있는 동안만 떠 있는 화면이다.</b> 방을 여는 일은
    /// <see cref="MainMenuRoot"/>가 하고(명판 "방 만들기"), 이 화면은 그 뒤부터 —
    /// 누가 모였는지 보여 주고, 호스트가 출발 신호를 주고, 나가면 배너로 돌려보낸다.</para>
    ///
    /// <para><b>세션이 끊기면 스스로 닫는다.</b> 호스트가 방을 닫으면 게스트에게는 아무 신호도
    /// 오지 않고 세션만 조용히 죽는다 — 그대로 두면 게스트가 빈 대기실에 갇힌다(§10 리스크 7).
    /// 그래서 <see cref="Update"/>가 세션이 살아 있는지 계속 본다.</para>
    ///
    /// <para><b>취소(Esc·게임패드 B)는 나가기와 같은 뜻이다.</b> 이 화면에서 "닫기"는 곧
    /// "방을 떠나기"이기 때문이다(§6.5). 확인 대화상자는 §12 미결 3번으로 남아 있다.</para>
    ///
    /// <para>난이도 순환·초대·게스트 권한 분리는 4차 몫이라 여기 없다. 지금은
    /// <b>혼자 방을 열고 출발하는 경로</b>가 끝까지 이어지는 데까지다.</para>
    /// </summary>
    public sealed class ReadyScreenRoot : MonoBehaviour
    {
        [Header("세션")]
        [SerializeField]
        [Tooltip("씬의 MenuSessionActions. 이 화면은 프리팹이라 보통은 MainMenuRoot가 Bind로 넘겨준다.")]
        private MenuSessionActions _actions;

        [Header("화면")]
        [SerializeField]
        [Tooltip("취소 전파와 포커스 회수를 맡는다. 이 화면 전체를 감싼다.")]
        private MenuPanel _panel;

        [SerializeField] private ReadyRosterView _roster;
        [SerializeField] private ReadyControlsView _controls;

        [Header("버튼")]
        [SerializeField] private Button _start;
        [SerializeField] private Button _invite;
        [SerializeField] private Button _leave;

        [SerializeField]
        [Tooltip("게임 시작 버튼의 라벨 — 게스트에게는 문구가 바뀐다(4차).")]
        private TMP_Text _startLabel;

        [Header("문구")]
        [SerializeField] private TMP_Text _roomStatus;

        [Header("개발 빌드 전용")]
        [SerializeField]
        [Tooltip("인게임 씬 선택 줄. Panel_Host에서 이관해 왔다(§6.4).")]
        private GameObject _devGroup;

        [SerializeField] private Button _sceneToggle;
        [SerializeField] private TMP_Text _sceneToggleLabel;

        private readonly string[] _names = new string[Systems.Networking.Lobby.RosterOrdering.Capacity];

        private ILobbyRoomService _room;
        private bool _open;
        private bool _leaving;

        /// <summary>
        /// 방을 떠났다 — 배너로 돌아갈지는 듣는 쪽(<see cref="MainMenuRoot"/>)이 정한다.
        /// 인자는 <b>밖에서 끊긴 사유</b>이고, 스스로 나갔으면 비어 있다.
        /// </summary>
        public event Action<string> Left;

        /// <summary>지금 이 화면이 떠 있는가.</summary>
        public bool IsOpen => _open && gameObject.activeSelf;

        private void OnEnable()
        {
            Bind(_start, OnStart);
            Bind(_invite, OnInvite);
            Bind(_leave, OnLeave);
            Bind(_sceneToggle, OnToggleScene);

            if (_panel != null)
            {
                _panel.Cancelled -= OnLeave;
                _panel.Cancelled += OnLeave;
            }

            BindRoom();
        }

        private void OnDisable()
        {
            if (_panel != null)
            {
                _panel.Cancelled -= OnLeave;
            }

            if (_room != null)
            {
                _room.Changed -= RefreshRoster;
            }
        }

        /// <summary>
        /// 대기실 상태 서비스를 붙인다. <b>UI가 NGO를 직접 보지 않는 자리</b>이고,
        /// 서비스는 Boot에서 등록되므로 화면이 켜질 때 찾는다.
        /// </summary>
        private void BindRoom()
        {
            if (_room != null)
            {
                _room.Changed -= RefreshRoster;
                _room = null;
            }

            if (ServiceLocator.TryGet(out ILobbyRoomService room))
            {
                _room = room;
                _room.Changed += RefreshRoster;
            }
        }

        /// <summary>
        /// 세션 동작을 넘겨받는다.
        ///
        /// <para><b>프리팹은 씬 오브젝트를 직렬화로 물 수 없다.</b> 준비 화면은 프리팹으로 지었으므로
        /// (계획 §14 ③ ㉤) 씬에 사는 <see cref="MenuSessionActions"/>는 <see cref="MainMenuRoot"/>가
        /// 여기로 넘겨준다. 인스펙터로 직접 꽂아 둔 경우에는 이 호출이 없어도 동작한다.</para>
        /// </summary>
        public void Bind(MenuSessionActions actions)
        {
            if (actions != null)
            {
                _actions = actions;
            }
        }

        /// <summary>대기실을 연다 — 방이 이미 열려 있다는 전제다.</summary>
        public void Open()
        {
            _open = true;
            _leaving = false;
            gameObject.SetActive(true);

            BindRoom();
            ApplyDevGroup();
            RefreshSceneLabel();
            RefreshRoster();
            RefreshAuthority();
            SetStatus(string.Empty);

            if (_panel != null)
            {
                _panel.Open();
            }
        }

        /// <summary>대기실을 닫는다. <b>세션은 건드리지 않는다</b> — 그건 부르는 쪽이 정한다.</summary>
        public void Close()
        {
            _open = false;
            if (_panel != null)
            {
                _panel.Close();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsOpen || _leaving)
            {
                return;
            }

            // 호스트가 방을 닫으면 게스트에게는 세션이 조용히 죽는 것 말고 신호가 없다.
            if (_actions != null && !_actions.IsSessionActive)
            {
                Depart("방이 닫혔습니다.");
                return;
            }

            RefreshAuthority();
        }

        private void OnStart()
        {
            if (_actions == null || _leaving)
            {
                return;
            }

            if (!_actions.BeginJourney())
            {
                SetStatus("출발하지 못했습니다. 잠시 뒤 다시 시도해 주세요.");
                return;
            }

            // 씬 로드가 시작됐다 — 이 화면은 곧 사라지므로 더 누르지 못하게 잠근다.
            SetInteractable(false);
            SetStatus("여정을 준비하는 중...");
        }

        private void OnInvite()
        {
            // 4차 — Steam 오버레이 / 직결 모드 주소 복사.
        }

        private void OnLeave()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            SetInteractable(false);

            if (_actions != null)
            {
                _actions.CloseRoom();
            }

            Depart(string.Empty);
        }

        /// <summary>화면을 닫고 떠났음을 알린다. 세션 종료는 비동기라 여기서 기다리지 않는다.</summary>
        private void Depart(string reason)
        {
            _leaving = true;
            Close();
            SetInteractable(true);
            Left?.Invoke(reason);
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

        /// <summary>
        /// 로스터를 다시 그린다 — 방에 누가 있는지는 <see cref="ILobbyRoomService"/>만 안다.
        ///
        /// <para><b>호스트는 언제나 첫 칸</b>이고 빈자리는 뒤로 몰린다 — 그 규칙은 서비스 뒤의
        /// <c>RosterOrdering</c>이 세우고, 여기서는 받은 대로 앉힌다(§7.3).</para>
        /// </summary>
        private void RefreshRoster()
        {
            if (_roster == null)
            {
                return;
            }

            for (int i = 0; i < _names.Length; i++)
            {
                _names[i] = null;
            }

            int hostSlot = -1;
            if (_room != null && _room.IsActive)
            {
                for (int i = 0; i < _names.Length; i++)
                {
                    if (_room.TryGetSlot(i, out string name, out bool isHost))
                    {
                        _names[i] = name;
                        if (isHost)
                        {
                            hostSlot = i;
                        }
                    }
                }
            }
            else if (_actions != null && _actions.IsHost)
            {
                // 상태 객체가 아직 스폰되기 전 — 방을 연 사람은 이미 방에 있다.
                _names[0] = Systems.Networking.Lobby.RosterOrdering.DisplayName(0);
                hostSlot = 0;
            }

            _roster.Show(_names, hostSlot);
        }

        /// <summary>호스트만 출발할 수 있다. 게스트 라벨 교체와 난이도 잠금은 4차다(§6.3).</summary>
        private void RefreshAuthority()
        {
            bool host = _actions != null && _actions.IsHost;
            if (_start != null)
            {
                _start.interactable = host && !_leaving;
            }
        }

        private void ApplyDevGroup()
        {
            bool dev = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            dev = true;
#endif
            if (_devGroup != null)
            {
                _devGroup.SetActive(dev);
            }
        }

        private void RefreshSceneLabel()
        {
            if (_sceneToggleLabel != null && _actions != null)
            {
                _sceneToggleLabel.text = $"인게임 씬: {_actions.GameplayScene}  →  {_actions.OtherGameplayScene}";
            }
        }

        private void SetStatus(string text)
        {
            if (_roomStatus == null)
            {
                return;
            }

            _roomStatus.text = text;
            _roomStatus.color = UiPalette.TextMuted;
        }

        private void SetInteractable(bool on)
        {
            SetButton(_start, on);
            SetButton(_invite, on);
            SetButton(_leave, on);
            SetButton(_sceneToggle, on);
        }

        private static void SetButton(Button button, bool on)
        {
            if (button != null)
            {
                button.interactable = on;
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
