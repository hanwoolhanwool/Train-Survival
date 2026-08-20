using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 우측 벽에 붙은 운행 공고 종이 —
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.4.
    ///
    /// <para>[비주얼 가이드](../../design/Train-Survival-비주얼-UIUX-가이드.md) §11의 "패치 노트를
    /// 벽에 붙은 공고문 종이로" 치환을 그대로 구현한다. 메뉴 항목으로 "패치 노트"를 만들지 않고
    /// <b>세계 안의 물건</b>으로 둔다.</para>
    ///
    /// <para><b>종이에는 글자가 구워져 있지 않다.</b> 원화에서 제목과 본문을 지우고(6차) 장식만
    /// 남겼으므로, 제목·요약은 전부 TMP다 — 버전이 올라가도 그림을 다시 만들지 않는다.</para>
    ///
    /// <para>버전 문자열은 <see cref="Application.version"/>에서 온다. 씬에 적어 두면 빌드 설정과
    /// 어긋나는 순간을 아무도 눈치채지 못한다.</para>
    /// </summary>
    [ExecuteAlways]
    public sealed class NoticeBoardView : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField]
        [Tooltip("공고 제목. 이미지가 아니라 TMP다.")]
        private TMP_Text _title;

        [SerializeField]
        [Tooltip("종이에 보이는 요약 3줄.")]
        private TMP_Text _summary;

        [SerializeField]
        [Tooltip("제목 문구. 비우면 '운행 공고'.")]
        private string _titleText = "운행 공고";

        [SerializeField]
        [TextArea(3, 6)]
        [Tooltip("종이에 보일 요약. {version} 자리에 빌드 버전이 들어간다.")]
        private string _summaryFormat = "v{version} 운행 개시\n· 전방 구간 서리 경보\n· 정차역 보급 정상";

        /// <summary>공고문을 눌렀다 — 패치 노트 전문을 열라는 신호.</summary>
        public event Action Clicked;

        /// <summary>지금 표시 중인 빌드 버전.</summary>
        public static string Version => string.IsNullOrEmpty(Application.version) ? "0.0.0" : Application.version;

        // ── 배치 ────────────────────────────────────────────────────────
        //
        // 시안(1672×941)에서 종이가 차지한 자리를 정규화한 값이다. 배너와 같은 이유로
        // **높이로 폭을 정한다** — 화면이 넓어질 때 폭 기준이면 공고문이 같이 커진다.

        /// <summary>종이 높이 ÷ 화면 높이.</summary>
        public const float HeightScale = 0.3783f;

        /// <summary>종이 스프라이트의 가로÷세로 (244:356).</summary>
        public const float Aspect = 244f / 356f;

        /// <summary>오른쪽 가장자리에서 띄우는 거리 — <b>화면 폭이 아니라 종이 폭</b>에 비례한다.</summary>
        public const float RightMarginInWidths = 0.2255f;

        /// <summary>종이 중심의 세로 위치 (화면 높이 대비, 아래 원점).</summary>
        public const float CenterY = 0.41655f;

        /// <summary>캔버스 크기에서 종이 RectTransform 크기를 낸다.</summary>
        public static Vector2 BoardSize(Vector2 canvasSize)
        {
            if (canvasSize.y <= 0f)
            {
                return Vector2.zero;
            }

            float height = canvasSize.y * HeightScale;
            return new Vector2(height * Aspect, height);
        }

        /// <summary>앵커를 캔버스 <b>우변</b> <see cref="CenterY"/> 지점에 둔 전제의 <c>anchoredPosition</c>.</summary>
        public static Vector2 BoardPosition(Vector2 canvasSize)
        {
            return new Vector2(-BoardSize(canvasSize).x * (0.5f + RightMarginInWidths), 0f);
        }

        /// <summary>종이 앵커 (우변 고정 · 세로는 화면 비율).</summary>
        public static Vector2 BoardAnchor()
        {
            return new Vector2(1f, CenterY);
        }

        private bool _applying;

        private void OnRectTransformDimensionsChange()
        {
            if (!_applying)
            {
                ApplyRect();
            }
        }

        private void ApplyRect()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null || !(rect.parent is RectTransform parent))
            {
                return;
            }

            Vector2 size = BoardSize(parent.rect.size);
            if (size.y <= 0f)
            {
                return;
            }

            _applying = true;
            try
            {
                rect.anchorMin = BoardAnchor();
                rect.anchorMax = BoardAnchor();
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = BoardPosition(parent.rect.size);
                rect.localScale = Vector3.one;
            }
            finally
            {
                _applying = false;
            }
        }

        private void OnEnable()
        {
            ApplyRect();
            Refresh();
        }

        /// <summary>제목·요약을 다시 그린다. 버전이 바뀌면 여기만 다시 부르면 된다.</summary>
        public void Refresh()
        {
            if (_title != null)
            {
                _title.text = string.IsNullOrEmpty(_titleText) ? "운행 공고" : _titleText;
            }

            if (_summary != null)
            {
                _summary.text = _summaryFormat.Replace("{version}", Version);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Clicked?.Invoke();
        }
    }
}
