using UnityEngine;

namespace Game.UI.Ready
{
    /// <summary>
    /// 오른쪽 조작 패널 한 덩어리 — [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §4.1 · §4.2.
    ///
    /// <para>패널 크기를 화면 높이에서 정하고, 버튼 다섯 자리를 실측표로 옮긴다.
    /// 로스터가 화면 <b>좌변</b>에 붙는 것과 달리 이쪽은 <b>우변</b>에 붙는다 — 두 패널은
    /// 서로 독립 배치라 한쪽이 커져도 다른 쪽을 밀지 않는다.</para>
    ///
    /// <para><b>무엇을 실행할지는 여기서 정하지 않는다.</b> 세션을 열고 닫는 일은 2차의
    /// <c>ReadyScreenRoot</c>·<c>MenuSessionActions</c> 몫이고, 난이도 순환은 4차의
    /// <c>DifficultyStepper</c> 몫이다. 이 컴포넌트는 자리와 그림만 책임진다.</para>
    /// </summary>
    [ExecuteAlways]
    public sealed class ReadyControlsView : MonoBehaviour, IReadyPanel
    {
        [SerializeField]
        [Tooltip("\"난이도\" 각인 위에 겹치는 라벨.")]
        private RectTransform _difficultyLabel;

        [SerializeField]
        [Tooltip("난이도 감소 (◀).")]
        private RectTransform _difficultyPrev;

        [SerializeField]
        [Tooltip("난이도 값 박스.")]
        private RectTransform _difficultyValue;

        [SerializeField]
        [Tooltip("난이도 증가 (▶).")]
        private RectTransform _difficultyNext;

        [SerializeField]
        [Tooltip("게임 시작 — 호스트 전용.")]
        private RectTransform _startButton;

        [SerializeField]
        [Tooltip("초대 하기.")]
        private RectTransform _inviteButton;

        [SerializeField]
        [Tooltip("나가기.")]
        private RectTransform _leaveButton;

        private bool _applying;
        private Vector2 _introOffset;
        private CanvasRectWatch _watch;

        /// <summary>등장 연출용 오프셋 — 실측 자리에 마지막으로 더한다.</summary>
        public Vector2 IntroOffset
        {
            get { return _introOffset; }
            set
            {
                _introOffset = value;
                ApplyPanelRect();
            }
        }

        private void OnEnable()
        {
            ApplyPanelRect();
            ApplyLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_applying)
            {
                return;
            }

            ApplyPanelRect();
        }

        /// <summary>
        /// 캔버스가 달라졌으면 다시 잰다 — 이 패널도 점 앵커라 부모가 커져도
        /// 통지가 오지 않는다(<see cref="CanvasRectWatch"/>).
        /// </summary>
        private void LateUpdate()
        {
            if (transform.parent is RectTransform parent && _watch.NeedsApply(parent.rect.size))
            {
                ApplyPanelRect();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyPanelRect();
                ApplyLayout();
            }
        }
#endif

        /// <summary>버튼 자리를 실측표에서 다시 매긴다.</summary>
        public void ApplyLayout()
        {
            Place(_difficultyLabel, ReadyPanelLayout.DifficultyLabel);
            Place(_difficultyPrev, ReadyPanelLayout.DifficultyPrev);
            Place(_difficultyValue, ReadyPanelLayout.DifficultyValue);
            Place(_difficultyNext, ReadyPanelLayout.DifficultyNext);
            Place(_startButton, ReadyPanelLayout.StartButton);
            Place(_inviteButton, ReadyPanelLayout.InviteButton);
            Place(_leaveButton, ReadyPanelLayout.LeaveButton);
        }

        /// <summary>패널 자신의 크기·자리를 캔버스에서 다시 잰다.</summary>
        public void ApplyPanelRect()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null || !(rect.parent is RectTransform parent))
            {
                return;
            }

            Vector2 size = ReadyPanelLayout.ControlsSize(parent.rect.size);
            if (size.y <= 0f)
            {
                return;
            }

            _applying = true;
            try
            {
                rect.anchorMin = ReadyPanelLayout.ControlsAnchor();
                rect.anchorMax = ReadyPanelLayout.ControlsAnchor();
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = ReadyPanelLayout.ControlsPosition(parent.rect.size) + _introOffset;
                rect.localScale = Vector3.one;
                _watch.MarkApplied(parent.rect.size);
            }
            finally
            {
                _applying = false;
            }
        }

        private static void Place(RectTransform rect, Rect anchors)
        {
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
