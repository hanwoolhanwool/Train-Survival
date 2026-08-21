using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Ready
{
    /// <summary>
    /// 로스터 슬롯 한 칸 — [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §4.1.
    ///
    /// <para>칸은 <b>빈자리 / 참가자</b> 두 모습만 갖는다. 준비(Ready) 토글은 시안에 없고,
    /// 출발은 호스트가 단독으로 정한다(§1.2).</para>
    ///
    /// <para><b>프레임 그림을 지우지 않는다.</b> 로스터 그림에는 빈 칸 넉 장이 이미 그려져 있고,
    /// 이 컴포넌트는 그 위 같은 자리에 칸을 겹친다 — 잘라낸 자리가 드러나지 않는 검증된 방식이다
    /// (로비 계획 §4.2-3). 그래서 <see cref="_background"/>는 <b>항상 켜져 있고</b>,
    /// 빈자리·참가자는 스프라이트 교체로만 갈린다.</para>
    ///
    /// <para>이름·역할·상태는 전부 TMP다. 그림에 구워진 문구는 칸 스프라이트를 만들 때 지웠다(§5.1-5).</para>
    /// </summary>
    [ExecuteAlways]
    public sealed class ReadySlotView : MonoBehaviour
    {
        [Header("조각")]
        [SerializeField]
        [Tooltip("칸 바탕. 빈자리·참가자에 따라 스프라이트가 갈린다.")]
        private Image _background;

        [SerializeField]
        [Tooltip("왼쪽 아이콘 — 왕관(호스트) 또는 사람+(빈자리).")]
        private Image _icon;

        [SerializeField]
        [Tooltip("\"HOST\" 줄. 참가자가 있을 때만 보인다.")]
        private TMP_Text _role;

        [SerializeField]
        [Tooltip("표시 이름.")]
        private TMP_Text _name;

        [SerializeField]
        [Tooltip("빈자리 안내 문구. 역할 줄이 없으므로 세로 중앙에 온다.")]
        private TMP_Text _emptyLabel;

        [SerializeField]
        [Tooltip("접속 표시 점. 색은 굽지 않고 UiPalette로 틴트한다.")]
        private Image _dot;

        [SerializeField]
        [Tooltip("접속 상태 문구.")]
        private TMP_Text _status;

        [Header("그림")]
        [SerializeField]
        private Sprite _emptySprite;

        [SerializeField]
        private Sprite _occupiedSprite;

        [SerializeField]
        private Sprite _crownIcon;

        [SerializeField]
        private Sprite _emptyIcon;

        [Header("문구")]
        [SerializeField]
        private string _hostRoleText = "HOST";

        [SerializeField]
        private string _emptyText = "플레이어 대기 중";

        [SerializeField]
        private string _connectedText = "접속 중";

        private bool _occupied;

        /// <summary>이 칸에 사람이 들어와 있는가.</summary>
        public bool IsOccupied => _occupied;

        private void OnEnable()
        {
            ApplyLayout();
            if (!Application.isPlaying && !_occupied)
            {
                SetEmpty();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyLayout();
            }
        }
#endif

        /// <summary>
        /// 칸 안 조각들의 자리를 실측표에서 다시 매긴다.
        /// <b>씬에 굳히지 않는 이유</b>는 배너 명판과 같다 — 그림을 다시 뽑아 좌표가 바뀌어도
        /// 고칠 곳이 <see cref="ReadyPanelLayout"/> 한 군데다.
        /// </summary>
        public void ApplyLayout()
        {
            Place(_icon, ReadyPanelLayout.SlotIcon);
            Place(_role, ReadyPanelLayout.SlotRole);
            Place(_name, ReadyPanelLayout.SlotName);
            Place(_emptyLabel, ReadyPanelLayout.SlotEmptyLabel);
            Place(_dot, ReadyPanelLayout.SlotDot);
            Place(_status, ReadyPanelLayout.SlotStatus);

            if (_background != null)
            {
                RectTransform rect = _background.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        /// <summary>빈자리로 만든다 — 사람+ 아이콘과 안내 문구만 남는다.</summary>
        public void SetEmpty()
        {
            _occupied = false;

            if (_background != null && _emptySprite != null)
            {
                _background.sprite = _emptySprite;
            }

            if (_icon != null)
            {
                _icon.sprite = _emptyIcon;
                _icon.enabled = _emptyIcon != null;
            }

            Show(_role, false);
            Show(_name, false);
            Show(_dot, false);
            Show(_status, false);
            Show(_emptyLabel, true);

            if (_emptyLabel != null)
            {
                _emptyLabel.text = _emptyText;
                _emptyLabel.color = UiPalette.TextMuted;
            }
        }

        /// <summary>참가자를 앉힌다. 호스트면 왕관과 역할 줄이 함께 온다.</summary>
        public void SetOccupied(string displayName, bool isHost)
        {
            _occupied = true;

            if (_background != null && _occupiedSprite != null)
            {
                _background.sprite = _occupiedSprite;
            }

            if (_icon != null)
            {
                _icon.sprite = isHost ? _crownIcon : _emptyIcon;
                _icon.enabled = _icon.sprite != null;
            }

            Show(_emptyLabel, false);
            Show(_name, true);
            Show(_dot, true);
            Show(_status, true);
            Show(_role, isHost);

            if (_role != null && isHost)
            {
                _role.text = _hostRoleText;
                _role.color = UiPalette.FocusBrass;
            }

            if (_name != null)
            {
                _name.text = displayName;
                _name.color = UiPalette.TextSteam;
            }

            if (_dot != null)
            {
                _dot.color = UiPalette.StatusFill(UiStatusLevel.Safe);
            }

            if (_status != null)
            {
                _status.text = _connectedText;
                _status.color = UiPalette.StatusTextColor(UiStatusLevel.Safe);
            }
        }

        private static void Show(Component target, bool visible)
        {
            if (target != null && target.gameObject.activeSelf != visible)
            {
                target.gameObject.SetActive(visible);
            }
        }

        private static void Place(Component target, Rect anchors)
        {
            if (target == null)
            {
                return;
            }

            RectTransform rect = target.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(anchors.xMin, anchors.yMin);
            rect.anchorMax = new Vector2(anchors.xMax, anchors.yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
