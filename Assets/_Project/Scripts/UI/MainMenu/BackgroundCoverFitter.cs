using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 배경 이미지를 부모 rect보다 <b>넘치게</b> 늘려 어떤 종횡비에서도 여백이 남지 않게 한다 —
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §4.1-3.
    ///
    /// <para>계산은 전부 <see cref="BackgroundCoverMath"/>에 있다. 이 컴포넌트가 하는 일은
    /// <b>언제 다시 재는가</b>와 <b>어디에 써넣는가</b>뿐이다 — 창 크기가 바뀌면 RectTransform의
    /// 치수 변경 통지가 오고, 그때 <c>sizeDelta</c>만 갱신한다.</para>
    ///
    /// <para><b>앵커는 전체 스트레치를 전제로 한다.</b> 그래야 부모(캔버스)가 커질 때 이 rect도
    /// 함께 변해 통지가 오고, <c>sizeDelta</c>가 "부모보다 얼마나 넘치는가"라는 한 가지 뜻만 갖는다.
    /// <see cref="Apply"/>가 매번 앵커·피벗을 다시 맞추므로 인스펙터에서 잘못 건드려도 복구된다.</para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BackgroundCoverFitter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("덮을 배경 이미지. 비워 두면 같은 오브젝트의 Image를 쓴다.")]
        private Image _image;

        private RectTransform _rect;

        /// <summary>재귀 방지 — <c>sizeDelta</c> 대입이 다시 치수 변경 통지를 부른다.</summary>
        private bool _applying;

        private void OnEnable()
        {
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_applying)
            {
                return;
            }

            Apply();
        }

        /// <summary>스프라이트를 바꾼 뒤처럼 외부 사정으로 다시 재야 할 때 호출한다.</summary>
        public void Refit()
        {
            Apply();
        }

        private void Apply()
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            Sprite sprite = _image != null ? _image.sprite : null;
            if (sprite == null)
            {
                return;
            }

            if (!(_rect.parent is RectTransform parent))
            {
                return;
            }

            _applying = true;
            try
            {
                _rect.anchorMin = Vector2.zero;
                _rect.anchorMax = Vector2.one;
                _rect.pivot = new Vector2(0.5f, 0.5f);
                _rect.anchoredPosition = Vector2.zero;
                _rect.localScale = Vector3.one;
                _rect.sizeDelta = BackgroundCoverMath.CoverSizeDelta(parent.rect.size, sprite.rect.size);
            }
            finally
            {
                _applying = false;
            }
        }
    }
}
