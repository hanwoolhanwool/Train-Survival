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
    /// <para><b>접속 표시(녹색 점 + "접속 중")는 지웠다</b>(2026-08-22 사용자 지시).
    /// 칸에 이름이 있다는 것이 곧 접속 중이라는 뜻이라 같은 사실을 두 번 말하고 있었고,
    /// 그 자리를 비우니 이름 칸이 오른쪽으로 넓어졌다.</para>
    ///
    /// <para><b>프레임 그림을 지우지 않는다.</b> 로스터 그림에는 빈 칸 넉 장이 이미 그려져 있고,
    /// 이 컴포넌트는 그 위 같은 자리에 칸을 겹친다 — 잘라낸 자리가 드러나지 않는 검증된 방식이다
    /// (로비 계획 §4.2-3). 그래서 <see cref="_background"/>는 <b>항상 켜져 있고</b>,
    /// 빈자리·참가자는 스프라이트 교체로만 갈린다.</para>
    ///
    /// <para>이름·역할·상태는 전부 TMP다. 그림에 구워진 문구는 칸 스프라이트를 만들 때 지웠다(§5.1-5).</para>
    ///
    /// <para><b>사람이 들고 날 때 짧게 밝아졌다 가라앉는다</b>(5차). 로스터는 스스로 움직이지 않는
    /// 화면이라, 누가 들어왔는지가 <b>한 프레임 만에 갈아치워지면</b> 보고 있지 않던 사람은
    /// 무엇이 바뀌었는지 모른다. 연출은 그 변화에 시간을 주는 일이다.</para>
    ///
    /// <para><b>작아지는 방향으로는 움직이지 않는다.</b> 이 칸은 프레임 그림에 구워진 빈 칸 위에
    /// 정확히 겹쳐 있어서, 1보다 작게 줄이면 <b>밑에 깔린 그림의 테두리가 삐져나온다</b> —
    /// 배너 명판이 강조에서 커지는 방향으로만 움직이는 것과 같은 이유다(로비 계획 §4.2-3).</para>
    /// </summary>
    [ExecuteAlways]
    public sealed class ReadySlotView : MonoBehaviour
    {
        /// <summary>들고 나는 연출에 걸리는 시간(초).</summary>
        public const float TransitionSeconds = 0.18f;

        /// <summary>연출 시작 배율 — <b>1보다 크다.</b> 밑에 깔린 그림을 덮은 채로만 움직인다.</summary>
        public const float TransitionScale = 1.035f;

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

        [Header("연출")]
        [SerializeField]
        [Tooltip("들고 나는 연출에 쓰는 CanvasGroup. 없으면 연출 없이 즉시 바뀐다.")]
        private CanvasGroup _group;

        private bool _occupied;
        private bool _known;
        private float _transition = 1f;

        /// <summary>이 칸에 사람이 들어와 있는가.</summary>
        public bool IsOccupied => _occupied;

        private void OnEnable()
        {
            ApplyLayout();
            if (!Application.isPlaying && !_occupied)
            {
                SetEmpty();
            }

            // 화면을 새로 열 때는 연출 없이 제자리에서 시작한다 — 처음 뜨는 것은 "변화"가 아니다.
            _known = false;
            ApplyTransition(1f);
        }

        private void Update()
        {
            if (_transition >= 1f)
            {
                return;
            }

            float step = TransitionSeconds <= 0f ? 1f : Time.unscaledDeltaTime / TransitionSeconds;
            ApplyTransition(Mathf.MoveTowards(_transition, 1f, step));
        }

        /// <summary>
        /// 상태가 <b>실제로 바뀌었을 때만</b> 연출을 시작한다.
        ///
        /// <para>로스터는 멤버 목록이 올 때마다 넉 장을 통째로 다시 그리므로(§7.1의 <c>Changed</c> 하나로
        /// 멤버도 난이도도 온다), 여기서 거르지 않으면 <b>난이도를 바꿀 때마다 칸 넉 장이 깜빡인다.</b></para>
        /// </summary>
        private void Begin(bool occupied)
        {
            // 처음 앉히는 것은 "변화"가 아니다 — 화면을 열 때 넉 장이 한꺼번에 깜빡이면 소란스럽다.
            bool changed = _known && _occupied != occupied;
            _known = true;
            _occupied = occupied;

            if (changed && Application.isPlaying)
            {
                ApplyTransition(0f);
            }
        }

        private void ApplyTransition(float weight)
        {
            _transition = Mathf.Clamp01(weight);

            if (_group != null)
            {
                _group.alpha = _transition;
            }

            transform.localScale = Vector3.one * Mathf.LerpUnclamped(TransitionScale, 1f, _transition);
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
            Begin(false);

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
            Begin(true);

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
