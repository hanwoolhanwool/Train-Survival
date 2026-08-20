using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 좌측 표지판 한 덩어리 — 명판 4장의 자리를 배너에 맞추고, 화살표를 선택된 줄로 옮긴다.
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §3.1 · §4.2.
    ///
    /// <para><b>자리를 씬에 굳히지 않고 여기서 매긴다.</b> 명판 좌표는
    /// <see cref="MenuPlateLayout"/>의 실측표가 유일한 출처이고, 이 컴포넌트가 그것을 앵커로 옮긴다.
    /// 배너 그림을 다시 뽑아 좌표가 바뀌어도 고칠 곳이 한 군데다.</para>
    ///
    /// <para>화살표는 <b>한 개뿐이다.</b> 원화에서는 첫 줄에 붙박이로 그려져 있던 것을 떼어내
    /// (§4.2-2) 선택된 줄로 옮겨 붙인다 — 색을 못 봐도 선택 위치가 읽히는 근거다(§8.2).</para>
    ///
    /// <para>무엇을 실행할지는 여기서 정하지 않는다. 화면 상태 기계와 세션 로직은 4차의
    /// <c>MainMenuRoot</c>·<c>MenuSessionActions</c> 몫이다.</para>
    /// </summary>
    [ExecuteAlways]
    public sealed class MenuBannerView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("위에서부터 순서대로. 비어 있으면 자식에서 찾는다.")]
        private MenuPlateButton[] _plates;

        [SerializeField]
        [Tooltip("선택된 줄로 옮겨 붙는 화살표.")]
        private RectTransform _arrow;

        [SerializeField]
        [Tooltip("시작 시 선택해 둘 줄 (위에서부터 0).")]
        private int _defaultSlot;

        private int _current = -1;
        private bool _applying;

        /// <summary>지금 화살표가 붙어 있는 줄.</summary>
        public int CurrentSlot => _current;

        private void OnRectTransformDimensionsChange()
        {
            if (_applying)
            {
                return;
            }

            ApplyBannerRect();
        }

        /// <summary>
        /// 배너 자신의 크기·자리를 캔버스에서 다시 잰다. <b>높이로 폭을 정하므로</b>
        /// 21:9에서도 그림이 늘어나지 않는다 — 잘리는 것은 왼쪽과 위아래뿐이다.
        /// </summary>
        private void ApplyBannerRect()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null || !(rect.parent is RectTransform parent))
            {
                return;
            }

            Vector2 canvas = parent.rect.size;
            Vector2 size = MenuPlateLayout.BannerSize(canvas);
            if (size.y <= 0f)
            {
                return;
            }

            _applying = true;
            try
            {
                rect.anchorMin = MenuPlateLayout.BannerAnchor();
                rect.anchorMax = MenuPlateLayout.BannerAnchor();
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = MenuPlateLayout.BannerPosition(canvas);
                rect.localScale = Vector3.one;
            }
            finally
            {
                _applying = false;
            }
        }

        private void OnEnable()
        {
            Collect();
            ApplyBannerRect();
            ApplyLayout();

            for (int i = 0; i < _plates.Length; i++)
            {
                if (_plates[i] == null)
                {
                    continue;
                }

                _plates[i].Focused -= OnPlateFocused;
                _plates[i].Focused += OnPlateFocused;
            }

            Highlight(_defaultSlot, true);

            if (Application.isPlaying)
            {
                SelectSlot(_defaultSlot);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; _plates != null && i < _plates.Length; i++)
            {
                if (_plates[i] != null)
                {
                    _plates[i].Focused -= OnPlateFocused;
                }
            }
        }

        /// <summary>해당 줄로 포커스를 옮긴다 — 키보드·게임패드 내비게이션(4차)이 부를 자리다.</summary>
        public void SelectSlot(int slot)
        {
            MenuPlateButton plate = Find(slot);
            if (plate == null)
            {
                return;
            }

            EventSystem current = EventSystem.current;
            if (current != null)
            {
                current.SetSelectedGameObject(plate.gameObject);
            }

            Highlight(slot, false);
        }

        private void OnPlateFocused(MenuPlateButton plate)
        {
            Highlight(plate.Slot, false);
        }

        /// <summary>
        /// 표지판에서 켜지는 명판은 <b>언제나 한 장뿐이다.</b> 마우스와 키보드가 각각 다른 줄을
        /// 켜 두 장이 동시에 빛나지 않도록, 켜고 끄는 판단을 여기 한 곳에 모은다.
        /// </summary>
        private void Highlight(int slot, bool instant)
        {
            MoveArrow(slot);

            for (int i = 0; _plates != null && i < _plates.Length; i++)
            {
                if (_plates[i] != null)
                {
                    _plates[i].SetHighlighted(_plates[i].Slot == _current, instant);
                }
            }
        }

        private void MoveArrow(int slot)
        {
            _current = Mathf.Clamp(slot, 0, MenuPlateLayout.SlotCount - 1);

            if (_arrow == null)
            {
                return;
            }

            _arrow.anchorMin = MenuPlateLayout.ArrowAnchorMin(_current);
            _arrow.anchorMax = MenuPlateLayout.ArrowAnchorMax(_current);
            _arrow.offsetMin = Vector2.zero;
            _arrow.offsetMax = Vector2.zero;
        }

        private void ApplyLayout()
        {
            for (int i = 0; i < _plates.Length; i++)
            {
                MenuPlateButton plate = _plates[i];
                if (plate == null)
                {
                    continue;
                }

                RectTransform rect = (RectTransform)plate.transform;
                rect.anchorMin = MenuPlateLayout.ToAnchorMin(plate.Slot);
                rect.anchorMax = MenuPlateLayout.ToAnchorMax(plate.Slot);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private void Collect()
        {
            if (_plates == null || _plates.Length == 0)
            {
                _plates = GetComponentsInChildren<MenuPlateButton>(true);
            }
        }

        private MenuPlateButton Find(int slot)
        {
            for (int i = 0; _plates != null && i < _plates.Length; i++)
            {
                if (_plates[i] != null && _plates[i].Slot == slot)
                {
                    return _plates[i];
                }
            }

            return null;
        }
    }
}
