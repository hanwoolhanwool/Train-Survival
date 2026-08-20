using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 승강장 오른쪽에 선 운행 공고대 —
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.4.
    ///
    /// <para>[비주얼 가이드](../../design/Train-Survival-비주얼-UIUX-가이드.md) §11의 "패치 노트를
    /// 벽에 붙은 공고문 종이로" 치환을 그대로 구현한다. 메뉴 항목으로 "패치 노트"를 만들지 않고
    /// <b>세계 안의 물건</b>으로 둔다.</para>
    ///
    /// <para><b>제목은 그림이고 요약만 TMP다.</b> 6차까지는 종이 한 장이 허공에 떠 있었고 제목까지
    /// TMP였다. 7차에 기둥·경첩·받침이 있는 공고대로 바꾸면서 제목 "운행 공고"는 원화에 구워진
    /// 것을 그대로 쓴다 — 씬의 TMP 문자열과 글자가 같았으므로 잃는 것이 없고, 종이의 원근과
    /// 잉크 번짐까지 살아 있어 TMP보다 낫다. 대신 <b>버전이 들어가는 요약 3줄은 반드시 TMP</b>다.</para>
    ///
    /// <para>스프라이트는 공고대 <b>전체</b>이고 종이는 그 안의 부분 영역이다. 받침은 화면 아래로
    /// 잘려 나가도록 배치한다 — 받침이 다 보이면 "이 물건이 어디에 서 있는가"를 묻게 되는데,
    /// 배경 오른쪽에는 선로밖에 없어서 답할 수가 없다.</para>
    ///
    /// <para>버전 문자열은 <see cref="Application.version"/>에서 온다. 씬에 적어 두면 빌드 설정과
    /// 어긋나는 순간을 아무도 눈치채지 못한다.</para>
    /// </summary>
    [ExecuteAlways]
    public sealed class NoticeBoardView : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField]
        [Tooltip("종이에 보이는 요약 3줄. 제목은 그림에 있으므로 TMP가 아니다.")]
        private TMP_Text _summary;

        [SerializeField]
        [TextArea(3, 6)]
        [Tooltip("종이에 보일 요약. {version} 자리에 빌드 버전이 들어간다.")]
        private string _summaryFormat = "v{version} 운행 개시\n· 전방 구간 서리 경보\n· 정차역 보급 정상";

        /// <summary>공고문을 눌렀다 — 패치 노트 전문을 열라는 신호.</summary>
        public event Action Clicked;

        /// <summary>지금 표시 중인 빌드 버전.</summary>
        public static string Version => string.IsNullOrEmpty(Application.version) ? "0.0.0" : Application.version;

        // ── 공고대 배치 ──────────────────────────────────────────────────
        //
        // 배너와 같은 이유로 **높이로 폭을 정한다** — 폭 기준으로 잡으면 21:9에서 공고대가
        // 같이 커져 화면을 파고든다. 높이가 1을 넘는 것은 의도다: 받침이 화면 아래로 나간다.

        /// <summary>공고대 높이 ÷ 화면 높이. <b>1을 넘는다</b> — 받침을 화면 밖으로 밀어낸다.</summary>
        public const float HeightScale = 1.0455f;

        /// <summary>공고대 스프라이트의 가로÷세로 (764:1664).</summary>
        public const float Aspect = 764f / 1664f;

        /// <summary>오른쪽 가장자리에서 띄우는 거리 — <b>화면 폭이 아니라 공고대 폭</b>에 비례한다.</summary>
        public const float RightMarginInWidths = 0.1636f;

        /// <summary>공고대 중심의 세로 위치 (화면 높이 대비, 아래 원점).</summary>
        public const float CenterY = 0.472f;

        // ── 스프라이트 안의 종이 ─────────────────────────────────────────
        //
        // 아래 값은 전부 **공고대 크기에 대한 비율**이고, 원점은 공고대의 중심이다.
        // 종이만 따로 알아야 하는 이유: 화면 밖으로 나가도 되는 것은 기둥과 받침뿐이고,
        // 글자가 실린 종이는 어느 종횡비에서도 전부 보여야 한다.

        /// <summary>종이 중심의 가로 오프셋.</summary>
        public const float PaperOffsetX = -0.09882f;

        /// <summary>종이 중심의 세로 오프셋 (위가 +).</summary>
        public const float PaperOffsetY = 0.04657f;

        /// <summary>종이 폭 ÷ 공고대 폭.</summary>
        public const float PaperWidth = 0.71335f;

        /// <summary>종이 높이 ÷ 공고대 높이.</summary>
        public const float PaperHeight = 0.47175f;

        // ── 요약 문구가 앉는 자리 ────────────────────────────────────────
        //
        // 원화의 장식 두 줄 사이 — 위 장식이 끝나는 곳과 아래 장식이 시작하는 곳 사이다.
        // 원화의 글줄이 오른쪽으로 2.4도 내려가므로 TMP도 같은 각으로 눕힌다.

        /// <summary>요약 사각형 중심의 가로 오프셋.</summary>
        public const float SummaryOffsetX = -0.09031f;

        /// <summary>요약 사각형 중심의 세로 오프셋 (위가 +).</summary>
        public const float SummaryOffsetY = 0.00541f;

        /// <summary>요약 사각형 폭 ÷ 공고대 폭.</summary>
        public const float SummaryWidth = 0.52356f;

        /// <summary>요약 사각형 높이 ÷ 공고대 높이.</summary>
        public const float SummaryHeight = 0.16827f;

        /// <summary>원화 글줄의 기울기. 화면 좌표는 위가 +이므로 음수다.</summary>
        public const float SummaryTiltDegrees = -2.4f;

        /// <summary>캔버스 크기에서 공고대 RectTransform 크기를 낸다.</summary>
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

        /// <summary>공고대 앵커 (우변 고정 · 세로는 화면 비율).</summary>
        public static Vector2 BoardAnchor()
        {
            return new Vector2(1f, CenterY);
        }

        /// <summary>공고대 중심의 캔버스 좌표 (좌하 원점).</summary>
        public static Vector2 BoardCenter(Vector2 canvasSize)
        {
            return new Vector2(canvasSize.x + BoardPosition(canvasSize).x, canvasSize.y * CenterY);
        }

        /// <summary>종이가 실제로 덮는 캔버스 사각형 (좌하 원점).</summary>
        public static Rect PaperRect(Vector2 canvasSize)
        {
            return SubRect(canvasSize, PaperOffsetX, PaperOffsetY, PaperWidth, PaperHeight);
        }

        /// <summary>요약 문구가 앉는 캔버스 사각형 (좌하 원점).</summary>
        public static Rect SummaryRect(Vector2 canvasSize)
        {
            return SubRect(canvasSize, SummaryOffsetX, SummaryOffsetY, SummaryWidth, SummaryHeight);
        }

        private static Rect SubRect(Vector2 canvasSize, float offsetX, float offsetY, float width, float height)
        {
            Vector2 board = BoardSize(canvasSize);
            if (board.y <= 0f)
            {
                return Rect.zero;
            }

            Vector2 center = BoardCenter(canvasSize) + new Vector2(offsetX * board.x, offsetY * board.y);
            Vector2 size = new Vector2(width * board.x, height * board.y);
            return new Rect(center - size * 0.5f, size);
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

                ApplySummaryRect(size);
            }
            finally
            {
                _applying = false;
            }
        }

        /// <summary>요약 TMP를 종이 위 제자리에 앉힌다 — 공고대가 커지면 글자 자리도 같이 커진다.</summary>
        private void ApplySummaryRect(Vector2 boardSize)
        {
            if (_summary == null || !(_summary.transform is RectTransform summary))
            {
                return;
            }

            summary.anchorMin = new Vector2(0.5f, 0.5f);
            summary.anchorMax = new Vector2(0.5f, 0.5f);
            summary.pivot = new Vector2(0.5f, 0.5f);
            summary.sizeDelta = new Vector2(SummaryWidth * boardSize.x, SummaryHeight * boardSize.y);
            summary.anchoredPosition = new Vector2(SummaryOffsetX * boardSize.x, SummaryOffsetY * boardSize.y);
            summary.localRotation = Quaternion.Euler(0f, 0f, SummaryTiltDegrees);
            summary.localScale = Vector3.one;
        }

        private void OnEnable()
        {
            ApplyRect();
            Refresh();
        }

        /// <summary>요약을 다시 그린다. 버전이 바뀌면 여기만 다시 부르면 된다.</summary>
        public void Refresh()
        {
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
