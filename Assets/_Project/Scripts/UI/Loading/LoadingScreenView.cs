using System.Text;
using Game.Core.Services;
using Game.Systems.Loading;
using Game.UI.MainMenu;
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
    /// <see cref="UiPalette"/>가 정한다. 붙는 자산도 배경 한 장과 팁 목록뿐이라 직렬화 필드로
    /// 충분하다. 대신 씬 YAML에 남기는 흔적이 오브젝트 하나로 줄어든다.</para>
    ///
    /// <para><b>같은 장소를 더 어둡게 깐다</b>(§8.1). 로비·준비 화면과 같은 배경을 쓰고 검정을
    /// 55 % 겹치므로, <b>밝기 차이만으로 "화면이 넘어갔다"가 읽힌다</b> — 새 그림을 그리지 않고도
    /// 세 화면이 한 장소에서 이어진다. 흰 글자 대비도 그 오버레이가 벌어 준다.</para>
    ///
    /// <para><b>시간은 흐름이 소유한다</b>(§8.3). 페이드와 최소 표시 시간은
    /// <see cref="ISessionLoadFlow.Alpha"/>가 이미 계산해 온 값이고, 여기서는 그것을
    /// <see cref="CanvasGroup"/>에 옮겨 적기만 한다.</para>
    ///
    /// <para><b>알려진 함정</b>(§8.4): ③ 정착 단계에서는 인게임 씬의 IMGUI HUD가 이미 그려진다.
    /// <b>IMGUI는 uGUI 캔버스보다 항상 위에 그려지므로 <see cref="SortingOrder"/>로는 막을 수
    /// 없다.</b> 가리는 일은 <see cref="HudLoadingCover"/>가 맡는다.</para>
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

        /// <summary>
        /// 배경 위에 겹치는 검정의 진하기.
        ///
        /// <para><b>계획 §8.1은 0.55였다.</b> 실측으로 올렸다 — 이 프로젝트는 선형 색공간이라
        /// 알파 블렌딩이 감마 기준의 직관보다 <b>덜 어둡게</b> 끝나고, 배경(<c>T_Menu_Background</c>)의
        /// 아래쪽 돌길이 밝아 팁 줄이 특히 불리하다. 글자 아래 배경만 골라 재면:</para>
        ///
        /// <list type="table">
        /// <item><description>0.55 — 상태 문구 10.6:1 · <b>팁 3.6:1</b> (4.5 미만)</description></item>
        /// <item><description>0.70 — 상태 문구 12.2:1 · 팁 4.2:1 (여전히 미만)</description></item>
        /// <item><description><b>0.80</b> — 상태 문구 13.6:1 · <b>팁 4.8:1</b></description></item>
        /// </list>
        ///
        /// <para><see cref="UiPalette.TextMuted"/>가 약속하는 5.5:1은 <b>바탕이 <c>PanelSoot</c>일 때</b>의
        /// 값이다(팔레트 주석). 사진 위에 그대로 얹으면 그 약속이 성립하지 않으므로,
        /// 오버레이를 그 약속이 다시 서는 지점까지 올리는 것이 색을 새로 만드는 것보다 낫다.</para>
        /// </summary>
        private const float DimAlpha = 0.80f;

        [SerializeField]
        [Tooltip("문구에 쓸 폰트. 비어 있으면 TMP 기본 폰트로 떨어진다.")]
        private TMP_FontAsset _font;

        [SerializeField]
        [Tooltip("로비·준비 화면과 같은 배경 한 장. 비워 두면 검정 바탕이 된다.")]
        private Sprite _background;

        [SerializeField]
        [Tooltip("로딩 중에 읽을 한 줄. 비워 두면 팁 자리가 비어 있는다.")]
        private LoadingTipCatalog _tips;

        private ISessionLoadFlow _flow;

        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _fill;
        private TMP_Text _status;
        private TMP_Text _percent;
        private TMP_Text _peers;
        private TMP_Text _tip;

        private string _shownStatus;
        private int _shownPercent = -1;
        private string _shownPeers;

        /// <summary>이번 로딩에 뽑은 팁. 로딩당 하나이고 도중에 바뀌지 않는다(§8.3).</summary>
        private int _tipIndex = -1;

        /// <summary>직전 로딩의 팁 — 같은 것이 연달아 나오지 않게 기억한다.</summary>
        private int _previousTipIndex = -1;

        /// <summary>참가자 점의 색 — <see cref="UiPalette"/> 토큰을 리치텍스트 표기로 한 번만 옮겨 둔다.</summary>
        private string _hexReady;
        private string _hexWaiting;
        private string _hexEmpty;

        /// <summary>점 문자열 조립용 — 매 프레임 문자열을 새로 만들면 로딩 중에 GC가 돈다.</summary>
        private readonly StringBuilder _peerBuilder = new StringBuilder(64);

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

            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01(_flow.Alpha);
            }

            Draw(_flow.Progress, _flow.Status);
            DrawPeers(_flow);
        }

        /// <summary>
        /// 참가자 준비 현황(§8.2) — <b>이게 전원 대기를 견딜 만한 것으로 만든다.</b>
        /// 그냥 멈춰 있으면 고장으로 보이지만, "셋 중 둘 준비됨"이 보이면 기다리는 이유가 화면에 있다.
        ///
        /// <para>방이 없으면(Boot만 열어 본 경우) 아무것도 그리지 않는다 — 빈 점 넷은 정보가 아니다.</para>
        /// </summary>
        private void DrawPeers(ISessionLoadFlow flow)
        {
            if (_peers == null)
            {
                return;
            }

            _peerBuilder.Length = 0;
            bool anyone = false;

            for (int slot = 0; slot < flow.PeerCapacity; slot++)
            {
                if (slot > 0)
                {
                    _peerBuilder.Append("   ");
                }

                if (!flow.IsPeerPresent(slot))
                {
                    _peerBuilder.Append("<color=").Append(_hexEmpty).Append(">·</color>");
                    continue;
                }

                anyone = true;
                bool ready = flow.IsPeerReady(slot);
                _peerBuilder.Append("<color=").Append(ready ? _hexReady : _hexWaiting).Append('>')
                    .Append(ready ? '●' : '○').Append("</color>");
            }

            string text = anyone ? _peerBuilder.ToString() : string.Empty;
            if (_shownPeers != text)
            {
                _shownPeers = text;
                _peers.text = text;
            }
        }

        /// <summary>
        /// 화면을 올리고 내린다. <b>올라오는 순간에만</b> 팁을 뽑는다 —
        /// 로딩 도중에 바뀌면 읽던 문장이 사라진다(§8.3).
        /// </summary>
        private void Show(bool on)
        {
            if (_root == null || _root.activeSelf == on)
            {
                return;
            }

            if (on)
            {
                PickTip();
            }

            _root.SetActive(on);
        }

        private void PickTip()
        {
            if (_tip == null)
            {
                return;
            }

            if (_tips == null || _tips.Count == 0)
            {
                _tip.text = string.Empty;
                return;
            }

            _tipIndex = LoadingTipCatalog.PickIndex(_tips.Count, _previousTipIndex, Random.value);
            _previousTipIndex = _tipIndex;
            _tip.text = _tips.Get(_tipIndex);
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
            _root = new GameObject(
                "LoadingScreen",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            _root.transform.SetParent(transform, false);
            _root.layer = LayerMask.NameToLayer("UI");

            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _group = _root.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            BuildBackdrop();

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

            _peers = AddText(_root.transform, "Peers", 40, UiPalette.TextSteam);
            _peers.richText = true;
            Center(_peers.rectTransform, new Vector2(600f, 56f), -120f);

            _tip = AddText(_root.transform, "Tip", 28, UiPalette.TextMuted);
            _tip.fontStyle = FontStyles.Italic;
            Center(_tip.rectTransform, new Vector2(1400f, 80f), -230f);

            _hexReady = "#" + ColorUtility.ToHtmlStringRGB(UiPalette.FocusBrass);
            _hexWaiting = "#" + ColorUtility.ToHtmlStringRGB(UiPalette.TextMuted);
            _hexEmpty = "#" + ColorUtility.ToHtmlStringRGB(UiPalette.PanelLine);
        }

        /// <summary>
        /// 바탕 — 배경 한 장 위에 검정을 겹친다(§8.1). 배경이 없으면 불투명한 무쇠색 한 장이다.
        ///
        /// <para>배경은 <see cref="BackgroundCoverFitter"/>로 <b>넘치게</b> 늘린다.
        /// 로비가 이미 쓰는 계산이라 21:9에서도 여백이 없다 — 새로 만들지 않는다.</para>
        /// </summary>
        private void BuildBackdrop()
        {
            if (_background == null)
            {
                Image plain = AddImage(_root.transform, "Backdrop", UiPalette.PanelSoot);
                Stretch(plain.rectTransform);
                return;
            }

            Image background = AddImage(_root.transform, "Background", Color.white);
            background.sprite = _background;
            Stretch(background.rectTransform);
            background.gameObject.AddComponent<BackgroundCoverFitter>();

            // 어둡게 — 화면이 넘어갔다는 신호이자 흰 글자의 대비 확보다.
            Image dim = AddImage(_root.transform, "Dim", new Color(0f, 0f, 0f, DimAlpha));
            Stretch(dim.rectTransform);
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
