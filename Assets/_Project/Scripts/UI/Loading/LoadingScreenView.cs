using Game.Core.Services;
using Game.Systems.Loading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Loading
{
    /// <summary>
    /// 인게임 진입 로딩 화면 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §2 · §6.3 · §8.
    ///
    /// <para><b>Boot 씬의 <c>DontDestroyOnLoad</c> 오버레이다</b>(§2). 대기실 씬은 로딩 한복판에
    /// 통째로 사라지고 인게임 씬은 아직 없다 — 그 사이를 덮을 수 있는 자리는 여기뿐이다.</para>
    ///
    /// <para><b>화면이 흐름을 밀지 않는다</b>(§6.3). 여기서 하는 일은
    /// <see cref="ISessionLoadFlow"/>가 내놓는 (단계, 진행률, 문구)를 읽어 그리는 것뿐이고,
    /// 단계를 넘기는 판단은 하나도 하지 않는다.</para>
    ///
    /// <para><b>계층을 코드로 짓는다.</b> 프리팹으로 두지 않은 이유는 이 화면에 <b>배치할 것이
    /// 거의 없기</b> 때문이다 — 상호작용이 없고, 진행바는 값으로 움직이며, 색은 전부
    /// <see cref="UiPalette"/>가 정한다. 그림이 붙는 것은 5차(배경·팁)이고, 그때 필요한 것도
    /// 자산 참조 한둘이라 직렬화 필드로 충분하다. 대신 씬 YAML에 남기는 흔적이 한 줄로 줄어든다.</para>
    ///
    /// <para><b>1차는 검정 바탕이다</b>(§9 1차). 배경 이미지·어둡게·팁·페이드는 5차 몫이다.</para>
    ///
    /// <para><b>알려진 함정</b>(§8.4): ③ 정착 단계에서는 인게임 씬의 IMGUI HUD가 이미 그려진다.
    /// <b>IMGUI는 uGUI 캔버스보다 항상 위에 그려지므로 <see cref="SortingOrder"/>로는 막을 수
    /// 없다.</b> 가리는 일은 3차가 맡는다 — 1차에는 정착 단계가 한 프레임이라 드러나지 않는다.</para>
    /// </summary>
    public sealed class LoadingScreenView : MonoBehaviour
    {
        /// <summary>
        /// 무엇보다 위에 있어야 한다(§8.4). 준비 화면 캔버스가 25이므로 여유를 크게 둔다 —
        /// <b>uGUI 안에서만</b> 유효한 값이라는 점은 위 함정 항목을 참고.
        /// </summary>
        public const int SortingOrder = 1000;

        /// <summary>디자인 해상도 — 준비 화면과 같은 기준을 쓴다(§8.4).</summary>
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private const float BarWidth = 720f;
        private const float BarHeight = 10f;

        [SerializeField]
        [Tooltip("문구에 쓸 폰트. 비어 있으면 TMP 기본 폰트로 떨어진다.")]
        private TMP_FontAsset _font;

        private ISessionLoadFlow _flow;

        private GameObject _root;
        private RectTransform _fill;
        private TMP_Text _status;
        private TMP_Text _percent;

        private string _shownStatus;
        private int _shownPercent = -1;

        private void Awake()
        {
            Build();
            Show(false);
        }

        private void LateUpdate()
        {
            // 코디네이터보다 뒤에 읽는다 — 같은 프레임의 단계 전이가 화면에 그대로 반영된다.
            if (_flow == null && !ServiceLocator.TryGet(out _flow))
            {
                Show(false);
                return;
            }

            if (!_flow.IsActive)
            {
                Show(false);
                return;
            }

            Show(true);
            Draw(_flow.Progress, _flow.Status);
        }

        private void Show(bool on)
        {
            if (_root != null && _root.activeSelf != on)
            {
                _root.SetActive(on);
            }
        }

        private void Draw(float progress, string status)
        {
            float clamped = Mathf.Clamp01(progress);

            if (_fill != null)
            {
                // 채움은 폭이 아니라 앵커로 민다 — 해상도가 바뀌어도 비율이 그대로 따라온다.
                _fill.anchorMax = new Vector2(clamped, 1f);
            }

            if (_status != null && _shownStatus != status)
            {
                _shownStatus = status;
                _status.text = status;
            }

            int percent = Mathf.RoundToInt(clamped * 100f);
            if (_percent != null && _shownPercent != percent)
            {
                _shownPercent = percent;
                _percent.text = percent + "%";
            }
        }

        // ── 계층 ─────────────────────────────────────────────────────────

        private void Build()
        {
            _root = new GameObject("LoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root.transform.SetParent(transform, false);
            _root.layer = LayerMask.NameToLayer("UI");

            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // 바탕 — 대기실이 비쳐서는 안 되므로 불투명이다.
            Image backdrop = AddImage(_root.transform, "Backdrop", UiPalette.PanelSoot);
            Stretch(backdrop.rectTransform);

            _status = AddText(_root.transform, "Status", 48, UiPalette.TextSteam);
            Center(_status.rectTransform, new Vector2(1200f, 70f), 90f);

            Image track = AddImage(_root.transform, "BarTrack", UiPalette.PanelLine);
            Center(track.rectTransform, new Vector2(BarWidth, BarHeight), 0f);

            // 채움은 트랙 안에서 왼쪽에 붙어 늘어난다 — 앵커를 먼저 잡고 여백을 0으로 붙인다.
            _fill = AddImage(track.transform, "BarFill", UiPalette.FocusBrass).rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;

            _percent = AddText(_root.transform, "Percent", 32, UiPalette.TextMuted);
            Center(_percent.rectTransform, new Vector2(400f, 44f), -50f);
        }

        private static Image AddImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            var image = go.GetComponent<Image>();
            image.color = color;

            // 이 화면에는 누를 것이 없다. 레이캐스트를 켜 두면 아래 화면의 클릭만 애매하게 먹는다.
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text AddText(Transform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }

            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>화면 한가운데를 기준으로 <paramref name="offsetY"/>만큼 띄워 앉힌다.</summary>
        private static void Center(RectTransform rect, Vector2 size, float offsetY)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, offsetY);
        }
    }
}
