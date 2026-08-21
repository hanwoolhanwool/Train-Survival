using System;
using Game.Core.Services;
using Game.Systems.Networking.Lobby;
using Game.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// "방을 떠나기"이기 때문이다(§6.5). 그래서 <b>호스트든 게스트든 한 번 묻는다</b>
    /// (§12 미결 3번 — <see cref="ReadyConfirmDialog"/>).</para>
    ///
    /// <para><b>시안은 호스트 화면이다.</b> 게스트에게는 시안에 없는 상태가 둘 생긴다(§6.3) —
    /// 게임 시작이 눌리지 않고, 난이도 화살표가 사라진다. <b>버튼을 숨기지는 않는다</b>:
    /// 그림에 자리가 파여 있어 숨기면 패널이 비고, 무엇을 기다리는지도 알 수 없다.
    /// 그래서 비활성 + 라벨 교체로 남긴다.</para>
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
        [Tooltip("게임 시작 버튼의 라벨 — 게스트에게는 \"호스트를 기다리는 중\"으로 바뀐다(§6.3).")]
        private TMP_Text _startLabel;

        [Header("난이도")]
        [SerializeField]
        [Tooltip("난이도 감소 (◀) — 게스트에게는 보이지 않는다.")]
        private Button _difficultyPrev;

        [SerializeField]
        [Tooltip("난이도 증가 (▶) — 게스트에게는 보이지 않는다.")]
        private Button _difficultyNext;

        [SerializeField]
        [Tooltip("현재 단계 이름. 게스트에게도 그대로 보인다.")]
        private TMP_Text _difficultyValue;

        [Header("문구")]
        [SerializeField] private TMP_Text _roomStatus;

        [Header("연출 · 대응")]
        [SerializeField]
        [Tooltip("나가기 전에 한 번 묻는 창(§12 미결 3번). 없으면 묻지 않고 곧바로 나간다.")]
        private ReadyConfirmDialog _leaveConfirm;

        [SerializeField]
        [Tooltip("선택된 버튼을 감싸는 테두리 — 색 없이도 어디에 있는지 읽히게 한다(§9.2).")]
        private ReadyFocusFrame _focusFrame;

        [SerializeField]
        [Tooltip("로스터 패널의 등장 연출 — 위에서 내려온다.")]
        private ReadyPanelSlide _rosterSlide;

        [SerializeField]
        [Tooltip("조작 패널의 등장 연출 — 오른쪽에서 당겨져 온다.")]
        private ReadyPanelSlide _controlsSlide;

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

        /// <summary>지금 보여 주고 있는 난이도 단계. 방이 서기 전에도 시안대로 "보통"이 떠 있어야 한다.</summary>
        private int _difficulty = DifficultyStepper.DefaultIndex;

        /// <summary>마지막으로 화면에 반영한 권한. <c>-1</c>은 "아직 한 번도 안 그렸다".</summary>
        private int _shownAuthority = -1;

        /// <summary>토스트가 사라질 시각. 0이면 지금 문구는 토스트가 아니다.</summary>
        private float _statusClearAt;

        /// <summary>출발·이탈로 버튼을 잠갔는가 — 잠근 뒤에는 권한 계산이 이걸 덮어쓰면 안 된다.</summary>
        private bool _locked;

        /// <summary>토스트가 떠 있는 시간(초).</summary>
        private const float ToastSeconds = 3f;

        /// <summary>
        /// 위에서 아래로 본 조작 줄 — 키보드·게임패드가 이 순서로 오르내린다.
        /// 난이도는 화살표 둘이 <b>한 줄</b>이고, 그 안에서만 좌우로 움직인다.
        /// </summary>
        private const int RowDifficulty = 0;
        private const int RowStart = 1;
        private const int RowInvite = 2;
        private const int RowLeave = 3;
        private const int RowDev = 4;
        private const int RowCount = 5;

        private readonly bool[] _rowOpen = new bool[RowCount];

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
            Bind(_difficultyPrev, OnDifficultyPrev);
            Bind(_difficultyNext, OnDifficultyNext);
            BindAccents();

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
                _room.Changed -= OnRoomChanged;
            }

            UnbindAccents();
        }

        /// <summary>
        /// 대기실 상태 서비스를 붙인다. <b>UI가 NGO를 직접 보지 않는 자리</b>이고,
        /// 서비스는 Boot에서 등록되므로 화면이 켜질 때 찾는다.
        /// </summary>
        private void BindRoom()
        {
            if (_room != null)
            {
                _room.Changed -= OnRoomChanged;
                _room = null;
            }

            if (ServiceLocator.TryGet(out ILobbyRoomService room))
            {
                _room = room;
                _room.Changed += OnRoomChanged;
            }
        }

        /// <summary>
        /// 버튼들의 강조 표현을 이 화면에 잇는다.
        ///
        /// <para><b>마우스가 올라오면 선택도 그리로 옮긴다.</b> 그러지 않으면 마우스가 가리키는
        /// 버튼과 키보드가 선택한 버튼이 달라 <b>두 곳이 동시에 빛난다</b> — 배너 명판에서 실제로
        /// 겪은 사고다. 강조를 "선택" 하나에만 매달아 두면 그 일이 구조적으로 불가능해진다.</para>
        /// </summary>
        private void BindAccents()
        {
            ReadyButtonAccent[] accents = GetComponentsInChildren<ReadyButtonAccent>(true);
            for (int i = 0; i < accents.Length; i++)
            {
                if (accents[i] == null)
                {
                    continue;
                }

                accents[i].Focused -= OnAccentFocused;
                accents[i].Focused += OnAccentFocused;
            }
        }

        private void UnbindAccents()
        {
            ReadyButtonAccent[] accents = GetComponentsInChildren<ReadyButtonAccent>(true);
            for (int i = 0; i < accents.Length; i++)
            {
                if (accents[i] != null)
                {
                    accents[i].Focused -= OnAccentFocused;
                }
            }
        }

        private void OnAccentFocused(ReadyButtonAccent accent)
        {
            if (accent == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(accent.gameObject);
        }

        /// <summary>대기실 상태가 바뀌었다 — 멤버든 난이도든 한 신호로 온다(§7.1).</summary>
        private void OnRoomChanged()
        {
            RefreshRoster();
            RefreshDifficulty();
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
            _locked = false;
            gameObject.SetActive(true);

            _shownAuthority = -1;

            // 새 방은 언제나 "보통"에서 시작한다 — 상태가 서 있으면 곧 그쪽 값이 덮는다.
            _difficulty = DifficultyStepper.DefaultIndex;

            BindRoom();
            ApplyDevGroup();
            RefreshSceneLabel();
            RefreshRoster();
            RefreshDifficulty();
            RefreshAuthority();
            SetStatus(string.Empty);

            if (_leaveConfirm != null)
            {
                _leaveConfirm.Dismiss();
            }

            if (_panel != null)
            {
                _panel.Open();
            }

            ApplyNavigation();
            FocusFirst();
            PlayIntro();
        }

        /// <summary>
        /// 두 패널을 제자리로 들여온다 — <b>간판을 내려 걸고 옆의 것을 당겨오는</b> 그림이다.
        /// 시차를 두는 이유는 동시에 나타나면 "창이 떴다"가 되고, 하나씩 들어오면
        /// <b>손이 하나씩 놓은 것</b>으로 읽히기 때문이다.
        /// </summary>
        private void PlayIntro()
        {
            if (_rosterSlide != null)
            {
                _rosterSlide.Play();
            }

            if (_controlsSlide != null)
            {
                _controlsSlide.Play();
            }
        }

        /// <summary>대기실을 닫는다. <b>세션은 건드리지 않는다</b> — 그건 부르는 쪽이 정한다.</summary>
        public void Close()
        {
            _open = false;

            if (_leaveConfirm != null)
            {
                _leaveConfirm.Dismiss();
            }

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
            ExpireToast();
            KeepFocus();
            RefreshFocusFrame();
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

        /// <summary>
        /// 친구를 부른다 — 모드에 따라 <b>하는 일이 다르다</b>(§6.2).
        ///
        /// <para>Steam 모드에서는 오버레이 친구 목록을 연다. 직결 모드에는 부를 창이 없으므로
        /// <b>접속 주소를 클립보드에 넣는다</b>(§12 미결 8번) — 버튼이 살아 있어 패널이 비지 않고,
        /// 같은 PC 두 벌 테스트에서 실제로 쓸모가 있다.</para>
        ///
        /// <para>직결 모드의 복사는 <b>호스트만</b>이다. 게스트는 호스트 주소의 출처가 아니라
        /// 자기가 접속한 주소밖에 모르고, 그걸 남에게 건네면 맞을 수도 틀릴 수도 있다.</para>
        /// </summary>
        private void OnInvite()
        {
            if (_actions == null || _leaving)
            {
                return;
            }

            if (_actions.IsSteamMode)
            {
                ShowToast(_actions.InviteFriends()
                    ? "친구 목록을 열었습니다."
                    : "친구 목록을 열지 못했습니다.");
                return;
            }

            if (!_actions.IsHost)
            {
                return;
            }

            string address = _actions.RoomAddress;
            GUIUtility.systemCopyBuffer = address;
            ShowToast("접속 주소를 복사했습니다 — " + address);
        }

        private void OnDifficultyPrev()
        {
            StepDifficulty(DifficultyStepper.Prev(_difficulty, DifficultyStepper.Count));
        }

        private void OnDifficultyNext()
        {
            StepDifficulty(DifficultyStepper.Next(_difficulty, DifficultyStepper.Count));
        }

        /// <summary>
        /// 난이도를 옮긴다 — <b>호스트만</b>. 값은 대기실 상태에 실려 전원에게 같이 간다.
        ///
        /// <para>상태 객체가 아직 서기 전(방을 연 직후 한두 프레임)에도 화면은 움직여야 하므로
        /// 로컬 값을 먼저 옮기고, 서비스가 살아 있으면 그쪽 값이 다시 덮는다.</para>
        /// </summary>
        private void StepDifficulty(int index)
        {
            if (_leaving || _actions == null || !_actions.IsHost)
            {
                return;
            }

            _difficulty = index;
            if (_room != null)
            {
                _room.SetDifficulty(DifficultyStepper.ToLevel(index));
            }

            RefreshDifficulty();
        }

        /// <summary>
        /// 나가기 — <b>한 번 묻고 나간다</b>(§12 미결 3번, 4차 결정).
        ///
        /// <para>호스트든 게스트든 묻는다. 지금은 리스크 9번 때문에 <b>한 번 나가면 그 방으로
        /// 못 돌아오므로</b>, 되돌릴 수 없기는 양쪽이 같다.</para>
        ///
        /// <para>물을 창이 없으면(구버전 프리팹) 곧바로 나간다 — 없다고 나가지 못하게 되면
        /// 방에 갇힌다.</para>
        /// </summary>
        private void OnLeave()
        {
            if (_leaving)
            {
                return;
            }

            if (_leaveConfirm == null)
            {
                Leave();
                return;
            }

            if (_leaveConfirm.IsOpen)
            {
                return;
            }

            bool host = _actions != null && _actions.IsHost;
            _leaveConfirm.Ask(
                host ? "방을 닫고 나가시겠습니까?\n함께 있는 사람들의 연결이 끊깁니다." : "방에서 나가시겠습니까?",
                Leave);
        }

        private void Leave()
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

            if (_focusFrame != null)
            {
                _focusFrame.Follow(null);
            }

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

        /// <summary>현재 난이도를 화면에 옮긴다 — 값은 게스트에게도 보인다(§6.3).</summary>
        private void RefreshDifficulty()
        {
            if (_room != null && _room.IsActive)
            {
                _difficulty = DifficultyStepper.ToIndex(_room.Difficulty);
            }

            if (_difficultyValue != null)
            {
                _difficultyValue.text = DifficultyStepper.Name(_difficulty);
            }
        }

        /// <summary>
        /// 호스트와 게스트의 화면을 가른다(§6.2 · §6.3).
        ///
        /// <para>매 프레임 불리므로 <b>권한이 바뀔 때만</b> 실제로 그린다 — 세션이 서기까지
        /// 몇 프레임이 걸려 처음에는 게스트로 보였다가 호스트가 되는 경우가 있고,
        /// 그때 한 번만 다시 그리면 된다.</para>
        /// </summary>
        private void RefreshAuthority()
        {
            bool host = _actions != null && _actions.IsHost;
            bool steam = _actions != null && _actions.IsSteamMode;

            // 호스트 여부·모드·잠금이 함께 화면을 정한다 — 셋을 한 값으로 묶어 변화만 잡는다.
            int authority = (host ? 1 : 0) | (steam ? 2 : 0) | (_locked ? 4 : 0);
            if (authority == _shownAuthority)
            {
                return;
            }

            _shownAuthority = authority;

            if (_start != null)
            {
                _start.interactable = host && !_locked;
            }

            if (_startLabel != null)
            {
                // 숨기지 않고 문구를 바꾼다 — 무엇을 기다리는지 알 수 있어야 한다(§6.3).
                _startLabel.text = host ? "게임 시작" : "호스트를 기다리는 중";
            }

            // 화살표는 게스트에게서 사라지되 값은 남는다.
            SetArrow(_difficultyPrev, host && !_locked);
            SetArrow(_difficultyNext, host && !_locked);

            // Steam 모드에서는 로비 멤버도 초대할 수 있지만, 직결 모드의 주소 복사는 호스트만이다.
            if (_invite != null)
            {
                _invite.interactable = (steam || host) && !_locked;
            }

            // 잠긴 줄이 달라졌으니 오르내림 경로도 다시 잇는다.
            ApplyNavigation();
        }

        /// <summary>
        /// 오르내림 순서를 다시 잇는다 — <b>못 누르는 줄은 건너뛴다.</b>
        ///
        /// <para>유니티의 명시 내비게이션은 <b>비활성 이웃을 건너뛰지 않는다</b>. 게스트 화면에서는
        /// 난이도 줄과 게임 시작이 잠기는데, 그대로 두면 아래로 두 번 눌러야 초대에 닿고
        /// 그 사이 두 번은 눌러도 반응이 없어 보인다. 그래서 어디로 갈지는
        /// <see cref="MenuNavigation"/>이 정하고, 결과만 이웃 링크로 써넣는다 — 배너와 같은 방식이다.</para>
        /// </summary>
        private void ApplyNavigation()
        {
            _rowOpen[RowDifficulty] = IsUsable(_difficultyNext) || IsUsable(_difficultyPrev);
            _rowOpen[RowStart] = IsUsable(_start);
            _rowOpen[RowInvite] = IsUsable(_invite);
            _rowOpen[RowLeave] = IsUsable(_leave);
            _rowOpen[RowDev] = IsUsable(_sceneToggle);

            for (int row = 0; row < RowCount; row++)
            {
                Selectable up = Representative(MenuNavigation.Move(row, _rowOpen, -1));
                Selectable down = Representative(MenuNavigation.Move(row, _rowOpen, 1));

                if (row == RowDifficulty)
                {
                    // 한 줄 안의 좌우 — 화살표 둘 사이에서만 오간다.
                    Link(_difficultyPrev, up, down, null, _difficultyNext);
                    Link(_difficultyNext, up, down, _difficultyPrev, null);
                    continue;
                }

                Link(Representative(row), up, down, null, null);
            }
        }

        /// <summary>줄을 대표하는 항목 — 다른 줄에서 넘어올 때 여기에 내려앉는다.</summary>
        private Selectable Representative(int row)
        {
            switch (row)
            {
                // 난이도 줄에는 왼쪽 화살표로 들어온다 — 읽는 순서와 같다.
                case RowDifficulty: return IsUsable(_difficultyPrev) ? _difficultyPrev : _difficultyNext;
                case RowStart: return _start;
                case RowInvite: return _invite;
                case RowLeave: return _leave;
                case RowDev: return _sceneToggle;
                default: return null;
            }
        }

        private static bool IsUsable(Selectable target)
        {
            return target != null && target.gameObject.activeInHierarchy && target.interactable;
        }

        private static void Link(Selectable target, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            if (target == null)
            {
                return;
            }

            Navigation nav = target.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = up;
            nav.selectOnDown = down;
            nav.selectOnLeft = left;
            nav.selectOnRight = right;
            target.navigation = nav;
        }

        /// <summary>
        /// 포커스를 화면 안 첫 항목으로 데려온다.
        ///
        /// <para><see cref="MenuPanel"/>도 열 때 첫 항목을 고르지만, 그건 <b>직렬화된 한 곳</b>이라
        /// 게스트 화면에서 잠겨 있는 "게임 시작"을 고를 수 있다. 잠긴 것이 선택되면 눌러도 반응이 없고,
        /// 무엇보다 <b>거기서 방향키를 눌러도 이동 링크가 잠긴 항목 기준</b>이 된다.</para>
        /// </summary>
        private void FocusFirst()
        {
            Selectable first = Representative(MenuNavigation.First(_rowOpen));
            if (first != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        /// <summary>
        /// 선택이 비었으면 되찾아 온다 — <b>키보드·게임패드가 죽지 않게 하는 최후의 보루</b>다.
        /// 배너 쪽 <c>MainMenuRoot.KeepFocusInsideMenu</c>는 대기실이 떠 있으면 물러나므로
        /// (그쪽 화면은 지금 꺼져 있다) 여기서 스스로 지킨다.
        /// </summary>
        private void KeepFocus()
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return;
            }

            // 묻는 창이 떠 있으면 포커스는 그쪽 것이다.
            if (_leaveConfirm != null && _leaveConfirm.IsOpen)
            {
                return;
            }

            GameObject selected = events.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy)
            {
                FocusFirst();
            }
        }

        /// <summary>선택된 버튼을 테두리가 따라간다 — 색 없이도 어디에 있는지 읽힌다(§9.2).</summary>
        private void RefreshFocusFrame()
        {
            if (_focusFrame == null)
            {
                return;
            }

            EventSystem events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;

            // 이 화면 밖(묻는 창 안 포함)이 선택돼 있으면 테두리는 물러난다.
            Selectable target = selected != null ? selected.GetComponent<Selectable>() : null;
            bool mine = target != null && Owns(target);

            _focusFrame.Follow(mine ? (RectTransform)target.transform : null);
        }

        private bool Owns(Selectable target)
        {
            return target == _start || target == _invite || target == _leave
                || target == _difficultyPrev || target == _difficultyNext || target == _sceneToggle;
        }

        /// <summary>
        /// 화살표를 보이거나 감춘다 — <b>비활성이 아니라 투명</b>이다(§6.3).
        ///
        /// <para><c>SetActive(false)</c>로 지우지 않는 이유는 5차의 내비게이션 때문이다.
        /// 꺼진 오브젝트는 포커스 이동 경로에서 사라지고, 그러면 게스트와 호스트의 이동 순서가
        /// 달라진다. 투명하게 두고 <see cref="Selectable.interactable"/>만 끄면 경로가 같다.</para>
        /// </summary>
        private static void SetArrow(Button button, bool on)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = on;

            Graphic graphic = button.targetGraphic;
            if (graphic != null)
            {
                Color color = graphic.color;
                color.a = on ? 1f : 0f;
                graphic.color = color;
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
            _statusClearAt = 0f;
            WriteStatus(text);
        }

        /// <summary>
        /// 잠깐 떴다 사라지는 알림 — 주소를 복사했다는 것 같은, <b>남아 있을 이유가 없는 문구</b>다.
        /// 상태 줄을 그대로 쓴다: 이 화면에 뜨는 글자는 한 자리로 모여 있는 편이 읽힌다.
        /// </summary>
        private void ShowToast(string text)
        {
            WriteStatus(text);
            _statusClearAt = Time.unscaledTime + ToastSeconds;
        }

        private void ExpireToast()
        {
            if (_statusClearAt > 0f && Time.unscaledTime >= _statusClearAt)
            {
                SetStatus(string.Empty);
            }
        }

        private void WriteStatus(string text)
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
            _locked = !on;
            _shownAuthority = -1;

            SetButton(_start, on);
            SetButton(_invite, on);
            SetButton(_leave, on);
            SetButton(_sceneToggle, on);
            SetArrow(_difficultyPrev, on && _actions != null && _actions.IsHost);
            SetArrow(_difficultyNext, on && _actions != null && _actions.IsHost);
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
