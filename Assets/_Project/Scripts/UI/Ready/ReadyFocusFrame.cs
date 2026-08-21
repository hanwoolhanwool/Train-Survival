using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Ready
{
    /// <summary>
    /// 지금 선택된 버튼을 감싸는 테두리 한 장 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §5.2-3 · §9.2.
    ///
    /// <para><b>색각 대응이 이 부품의 존재 이유다.</b> 조작 패널의 버튼들은 상태를 밝기로 말하는데
    /// (§5.2-3 ①안 — Hover 원화가 없어 밝기 변조로 간다), 밝기 차이는 색을 못 보는 눈에도
    /// 남지만 <b>어느 것이 선택됐는지</b>를 가리기엔 약하다. 그래서 테두리가 따라붙는다 —
    /// 색이 아니라 <b>형태의 있고 없음</b>이라 색맹·저시력에서도 그대로 읽힌다.</para>
    ///
    /// <para><b>한 장뿐이고 옮겨 다닌다.</b> 버튼마다 테두리를 달아 두면 여섯 장이 되고,
    /// 두 장이 동시에 켜지는 사고가 난다(배너 명판에서 실제로 겪었다 — 마우스와 키보드가
    /// 각각 다른 명판을 켰다). 켜지는 것이 하나뿐이라는 사실을 <b>오브젝트가 하나</b>라는
    /// 구조로 못박는다. 배너의 화살표가 줄을 옮겨 다니는 것과 같은 방식이다.</para>
    ///
    /// <para><b>따라갈 때 부모를 바꾼다.</b> 그래야 대상의 앵커를 그대로 베껴 쓸 수 있고,
    /// 패널이 화면 비율에 따라 크기를 다시 잡아도(§4.2) 테두리가 저절로 같이 움직인다.
    /// 좌표를 계산해 옮기면 그 계산이 패널의 크기 재조정과 어긋나는 프레임이 생긴다.</para>
    /// </summary>
    public sealed class ReadyFocusFrame : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("테두리 그림. 9분할이라 어떤 크기의 버튼에도 모서리가 뭉개지지 않는다.")]
        private Image _frame;

        [SerializeField]
        [Tooltip("버튼 바깥으로 물러나는 여백(px, 패널 기준). 0이면 테두리가 버튼에 딱 붙는다.")]
        private float _padding = 4f;

        private RectTransform _rect;
        private RectTransform _target;

        /// <summary>지금 감싸고 있는 대상. 아무것도 없으면 <c>null</c>.</summary>
        public RectTransform Target => _target;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Follow(null);
        }

        /// <summary>
        /// 대상을 감싼다. <c>null</c>을 주면 사라진다.
        ///
        /// <para>테두리는 <b>대상의 형제</b>가 되어 맨 뒤에 선다 — 같은 부모 안에서 마지막 자식이
        /// 가장 위에 그려지므로, 버튼 위에 테두리가 얹힌다.</para>
        /// </summary>
        public void Follow(RectTransform target)
        {
            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            _target = target;

            if (_frame != null)
            {
                _frame.enabled = target != null;
            }

            if (target == null || target.parent == null)
            {
                return;
            }

            if (_rect.parent != target.parent)
            {
                _rect.SetParent(target.parent, false);
            }

            _rect.SetAsLastSibling();
            _rect.anchorMin = target.anchorMin;
            _rect.anchorMax = target.anchorMax;
            _rect.pivot = target.pivot;
            _rect.offsetMin = target.offsetMin - new Vector2(_padding, _padding);
            _rect.offsetMax = target.offsetMax + new Vector2(_padding, _padding);
            _rect.localScale = Vector3.one;
        }

        /// <summary>대상이 사라졌으면 (숨겨졌거나 파괴됐으면) 스스로 꺼진다.</summary>
        public void Prune()
        {
            if (_target != null && !_target.gameObject.activeInHierarchy)
            {
                Follow(null);
            }
        }
    }
}
