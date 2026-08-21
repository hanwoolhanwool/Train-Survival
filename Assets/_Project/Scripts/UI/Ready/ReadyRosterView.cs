using UnityEngine;

namespace Game.UI.Ready
{
    /// <summary>
    /// 왼쪽 로스터 패널 한 덩어리 — [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §4.1 · §4.2.
    ///
    /// <para>패널 자신의 크기·자리를 캔버스에서 다시 재고, 칸 넉 장을 실측표 위치로 옮긴다.
    /// <b>높이로 폭을 정하므로</b> 21:9에서도 그림이 늘어나지 않는다.</para>
    ///
    /// <para><b>여기는 네트워크를 모른다.</b> 누가 들어와 있는지는 3차의 <c>ILobbyRoomService</c>가
    /// 알려 주고, 이 컴포넌트는 <see cref="Show"/>로 받은 이름만 칸에 앉힌다.</para>
    /// </summary>
    [ExecuteAlways]
    public sealed class ReadyRosterView : MonoBehaviour, IReadyPanel
    {
        [SerializeField]
        [Tooltip("위에서부터 순서대로. 비어 있으면 자식에서 찾는다.")]
        private ReadySlotView[] _slots;

        [SerializeField]
        [Tooltip("그림에 구워진 \"게임 준비\" 각인 위에 겹치는 타이틀.")]
        private RectTransform _title;

        private bool _applying;
        private Vector2 _introOffset;

        /// <summary>칸 수 — 언제나 <see cref="ReadyPanelLayout.SlotCount"/>다.</summary>
        public int SlotCount => _slots != null ? _slots.Length : 0;

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
            Collect();
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Collect();
                ApplyPanelRect();
                ApplyLayout();
            }
        }
#endif

        /// <summary>
        /// 로스터를 다시 그린다. <paramref name="names"/>가 짧으면 나머지 칸은 빈자리가 된다.
        /// 호스트는 언제나 첫 칸이다 — 그 규칙은 3차의 <c>RosterOrdering</c>이 지킨다.
        /// </summary>
        public void Show(string[] names, int hostIndex)
        {
            Collect();
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    continue;
                }

                if (names != null && i < names.Length && !string.IsNullOrEmpty(names[i]))
                {
                    _slots[i].SetOccupied(names[i], i == hostIndex);
                }
                else
                {
                    _slots[i].SetEmpty();
                }
            }
        }

        /// <summary>타이틀과 칸 넉 장을 실측표 자리로 옮긴다.</summary>
        public void ApplyLayout()
        {
            if (_title != null)
            {
                Rect t = ReadyPanelLayout.RosterTitle;
                _title.anchorMin = new Vector2(t.xMin, t.yMin);
                _title.anchorMax = new Vector2(t.xMax, t.yMax);
                _title.offsetMin = Vector2.zero;
                _title.offsetMax = Vector2.zero;
                _title.localScale = Vector3.one;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    continue;
                }

                RectTransform rect = _slots[i].transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                rect.anchorMin = ReadyPanelLayout.SlotAnchorMin(i);
                rect.anchorMax = ReadyPanelLayout.SlotAnchorMax(i);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                _slots[i].ApplyLayout();
            }
        }

        /// <summary>패널 자신의 크기·자리를 캔버스에서 다시 잰다.</summary>
        public void ApplyPanelRect()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null || !(rect.parent is RectTransform parent))
            {
                return;
            }

            Vector2 size = ReadyPanelLayout.RosterSize(parent.rect.size);
            if (size.y <= 0f)
            {
                return;
            }

            _applying = true;
            try
            {
                rect.anchorMin = ReadyPanelLayout.RosterAnchor();
                rect.anchorMax = ReadyPanelLayout.RosterAnchor();
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = ReadyPanelLayout.RosterPosition(parent.rect.size) + _introOffset;
                rect.localScale = Vector3.one;
            }
            finally
            {
                _applying = false;
            }
        }

        private void Collect()
        {
            if (_slots == null || _slots.Length == 0)
            {
                _slots = GetComponentsInChildren<ReadySlotView>(true);
            }
        }
    }
}
