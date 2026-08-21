using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Ready
{
    /// <summary>
    /// 준비 화면 버튼 한 개의 상태 표현 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §5.2-3.
    ///
    /// <para><b>왜 이 부품이 필요했는가 — uGUI의 색 틴트가 밝게는 못 만든다.</b>
    /// 계획 §5.2-3 ①안은 "Normal 스프라이트에 1.0 → 1.18 틴트"였고 실제로 <c>ColorBlock</c>에
    /// 1.18을 넣어 두었지만, <b>화면에서는 아무 일도 일어나지 않았다.</b> uGUI는 상태색을
    /// 정점 색으로 실어 보내는데 그 채널이 <see cref="Color32"/>(0~255)라 <b>1을 넘는 값이
    /// 그대로 잘린다.</b> 1.18도 1.0도 같은 흰색이고, 즉 <b>호버가 없는 것과 똑같았다.</b></para>
    ///
    /// <para>그래서 <b>쉬는 상태를 조금 어둡게 두고 강조에서 원래 밝기로 올린다.</b>
    /// 곱셈으로 밝히는 대신 어둠에서 놓아주는 방향이라 1.0 상한에 걸리지 않는다.
    /// 원화 밝기는 <see cref="HoverTint"/>일 때 그대로다 — 강조된 버튼이 곧 원본이다.</para>
    ///
    /// <para><b>밝기만으로 말하지 않는다.</b> 색을 못 보는 눈에도 읽히도록 크기가 함께 움직이고
    /// (§9.2), 그 위에 테두리(<see cref="ReadyFocusFrame"/>)가 따라붙는다.
    /// <b>커지는 방향으로만</b> 움직이는 이유는 칸·명판과 같다 — 이 버튼들은 패널 그림에
    /// 구워진 버튼 위에 겹쳐 있어서, 1보다 작아지면 밑그림 테두리가 삐져나온다.</para>
    ///
    /// <para><b>마우스가 올라오면 선택도 옮긴다.</b> 그러지 않으면 마우스가 가리키는 버튼과
    /// 키보드가 선택한 버튼이 달라져 <b>두 곳이 동시에 빛난다</b> — 배너 명판에서 실제로 겪었다.
    /// 강조를 "선택" 하나에만 매달아 두면 그 사고가 구조적으로 불가능해진다.</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class ReadyButtonAccent : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        /// <summary>쉬는 상태의 밝기 — 원화보다 살짝 어둡다. 이 차이가 곧 호버의 폭이다.</summary>
        public const float RestTint = 0.82f;

        /// <summary>강조 상태의 밝기 — <b>원화 그대로</b>.</summary>
        public const float HoverTint = 1f;

        /// <summary>누르는 동안의 밝기 — 손끝이 눌렀다는 것을 알게 하는 만큼만 내려간다.</summary>
        public const float PressTint = 0.68f;

        /// <summary>강조에서의 배율. <b>1보다 크다</b> — 밑에 깔린 그림을 덮은 채로만 움직인다.</summary>
        public const float HoverScale = 1.03f;

        /// <summary>강조가 차오르는 데 걸리는 시간(초). 즉시 바뀌면 어디로 옮겨왔는지 눈이 못 따라간다.</summary>
        public const float FadeSeconds = 0.10f;

        [SerializeField]
        [Tooltip("밝기를 적용할 그림. 비면 Button의 targetGraphic을 쓴다.")]
        private Graphic _graphic;

        [SerializeField]
        [Tooltip("이 버튼의 바탕색. 흰색이면 원화 색을 그대로 쓴다는 뜻이다.")]
        private Color _baseColor = Color.white;

        private Button _button;
        private RectTransform _rect;
        private float _weight;
        private bool _pointerInside;
        private bool _selected;
        private bool _pressed;

        /// <summary>마우스가 올라왔거나 포커스가 들어왔다 — 어디를 비출지는 듣는 쪽이 정한다.</summary>
        public event Action<ReadyButtonAccent> Focused;

        /// <summary>이 버튼의 사각형 — 테두리가 따라올 때 쓴다.</summary>
        public RectTransform Rect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = (RectTransform)transform;
                }

                return _rect;
            }
        }

        /// <summary>바탕색을 바꾼다 — 확인 창처럼 버튼마다 색이 다른 곳에서 쓴다.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            Apply(_weight);
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            _rect = (RectTransform)transform;

            if (_graphic == null)
            {
                _graphic = _button.targetGraphic;
            }

            // uGUI의 색 틴트는 이 부품과 같은 그림을 두고 다툰다 — 표현은 한 곳이 소유한다.
            _button.transition = Selectable.Transition.None;
            Apply(0f);
        }

        private void OnDisable()
        {
            _pointerInside = false;
            _selected = false;
            _pressed = false;
            Apply(0f);
        }

        private void Update()
        {
            float target = _button != null && _button.interactable && (_pointerInside || _selected) ? 1f : 0f;
            if (Mathf.Approximately(_weight, target))
            {
                return;
            }

            float step = FadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / FadeSeconds;
            Apply(Mathf.MoveTowards(_weight, target, step));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            RaiseFocus();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            Apply(_weight);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            Apply(_weight);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
        }

        private void RaiseFocus()
        {
            if (_button == null || !_button.interactable)
            {
                return;
            }

            Focused?.Invoke(this);
        }

        private void Apply(float weight)
        {
            _weight = Mathf.Clamp01(weight);

            bool usable = _button == null || _button.interactable;
            float tint = _pressed && usable
                ? PressTint
                : Mathf.LerpUnclamped(RestTint, HoverTint, _weight);

            if (_graphic != null)
            {
                // 못 누르는 버튼은 채도를 빼고 가라앉힌다(§5.2-4) — 게스트의 "게임 시작"이 이 경우다.
                _graphic.color = usable
                    ? new Color(_baseColor.r * tint, _baseColor.g * tint, _baseColor.b * tint, _baseColor.a)
                    : new Color(UiPalette.IronGray.r, UiPalette.IronGray.g, UiPalette.IronGray.b, _baseColor.a * 0.85f);
            }

            float scale = usable ? Mathf.LerpUnclamped(1f, HoverScale, _weight) : 1f;
            Rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
