using System;
using Game.UI.MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Ready
{
    /// <summary>
    /// 되돌릴 수 없는 조작 앞에 한 번 묻는 창 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §12 미결 3번.
    ///
    /// <para><b>호스트든 게스트든 묻는다</b>(4차 사용자 결정). 권한에 따라 묻고 안 묻고를 가르면
    /// 같은 버튼이 사람마다 다르게 굴어 설명할 수 없고, 지금은 리스크 9번(세션 재시작 불가) 때문에
    /// <b>게스트도 한 번 나가면 그 방으로 못 돌아온다</b> — 되돌릴 수 없기는 양쪽이 같다.</para>
    ///
    /// <para><b>취소 전파는 <see cref="MenuPanel"/>에 맡긴다.</b> 이 창이 떠 있는 동안 Esc는
    /// "나가기"가 아니라 "묻는 창 닫기"여야 하는데, <see cref="MenuCancelRelay"/>가
    /// <b>가장 가까운</b> <see cref="MenuPanel"/>을 찾으므로 이 창이 자기 패널을 가지면
    /// 그 구분이 저절로 선다 — 준비 화면 쪽 패널까지 올라가지 않는다.</para>
    ///
    /// <para><b>답을 기억하지 않는다.</b> 물을 때마다 무엇을 할지 받고, 답이 나오면 잊는다.
    /// "확인을 눌렀다"는 사실을 들고 있으면 창을 두 번 여는 경로에서 지난 답이 새어 나온다.</para>
    /// </summary>
    public sealed class ReadyConfirmDialog : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("이 창 전체. 포커스 회수와 취소 전파를 맡는다.")]
        private MenuPanel _panel;

        [SerializeField]
        [Tooltip("무엇을 묻는지.")]
        private TMP_Text _message;

        [SerializeField]
        [Tooltip("그렇게 한다.")]
        private Button _confirm;

        [SerializeField]
        [Tooltip("아니다 — 창만 닫는다.")]
        private Button _cancel;

        [Header("면")]
        [SerializeField]
        [Tooltip("뒤 화면을 덮는 막. 색은 UiPalette가 정한다.")]
        private Image _scrim;

        [SerializeField]
        [Tooltip("창 바탕. 색은 UiPalette가 정한다.")]
        private Image _box;

        private Action _confirmed;

        /// <summary>지금 묻고 있는가.</summary>
        public bool IsOpen => gameObject.activeSelf;

        private void OnEnable()
        {
            Bind(_confirm, OnConfirm);
            Bind(_cancel, Dismiss);
            ApplyPalette();

            if (_panel != null)
            {
                _panel.Cancelled -= Dismiss;
                _panel.Cancelled += Dismiss;
            }
        }

        /// <summary>
        /// 색은 <see cref="UiPalette"/>에서만 온다 — 프리팹에 굳어 있는 값은 편집기 미리보기일 뿐이다.
        ///
        /// <para><b>"확인"이 위험색인 이유</b>: 이 창에서 되돌릴 수 없는 쪽은 확인이다.
        /// 위험 적색은 <b>면 전용</b>이라(가이드 §7.2 — 텍스트로 쓰면 2.9:1) 버튼 바탕에만 쓰고
        /// 글자는 크림색 그대로 둔다.</para>
        /// </summary>
        private void ApplyPalette()
        {
            if (_scrim != null)
            {
                _scrim.color = UiPalette.SettingsOverlay;
            }

            if (_box != null)
            {
                _box.color = UiPalette.PanelBackdrop;
            }

            Tint(_confirm, UiPalette.CriticalFill);
            Tint(_cancel, UiPalette.IronGray);
        }

        private static void Tint(Button button, Color color)
        {
            if (button != null && button.targetGraphic != null)
            {
                button.targetGraphic.color = color;
            }
        }

        private void OnDisable()
        {
            if (_panel != null)
            {
                _panel.Cancelled -= Dismiss;
            }
        }

        /// <summary>
        /// 묻는다. <paramref name="confirmed"/>는 "확인"을 눌렀을 때만 불린다.
        ///
        /// <para><b>포커스는 "취소"에서 시작한다.</b> 되돌릴 수 없는 쪽에 손가락을 얹어 두면
        /// 확인 창이 오조작을 막기는커녕 한 번 더 눌러 통과시키는 장치가 된다.</para>
        /// </summary>
        public void Ask(string message, Action confirmed)
        {
            _confirmed = confirmed;

            if (_message != null)
            {
                _message.text = message;
                _message.color = UiPalette.TextSteam;
            }

            gameObject.SetActive(true);

            if (_panel != null)
            {
                _panel.Open();
            }
        }

        /// <summary>묻기를 그만둔다 — 아무 일도 일어나지 않는다.</summary>
        public void Dismiss()
        {
            _confirmed = null;

            if (_panel != null)
            {
                _panel.Close();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnConfirm()
        {
            Action confirmed = _confirmed;

            // 먼저 잊고 닫는다 — 콜백이 이 창을 다시 여는 경로가 있어도 지난 답이 남지 않는다.
            _confirmed = null;
            if (_panel != null)
            {
                _panel.Close();
            }
            else
            {
                gameObject.SetActive(false);
            }

            if (confirmed != null)
            {
                confirmed();
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
