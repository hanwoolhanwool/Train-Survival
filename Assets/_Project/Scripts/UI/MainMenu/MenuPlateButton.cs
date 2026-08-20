using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 명판 한 장 = 버튼 한 개 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.1.
    ///
    /// <para><b>선택 상태를 그림 두 장으로 만든다.</b> 황금 명판을 어두운 명판 위에 겹쳐 두고
    /// 투명도만 올린다. 색을 칠하는 것이 아니라 원화를 그대로 쓰는 것이라 톤이 어긋나지 않는다.</para>
    ///
    /// <para><b>그림은 슬롯마다 따로 있다</b>(7차). 6차까지는 명판 한 장을 크롭해 네 줄에
    /// 늘여 썼는데, 배너 원화의 네 명판은 <b>높이도 리벳 자리도 서로 다르다</b>(117·113·113·111 px).
    /// 한 장을 늘이면 아래로 갈수록 밑에 구워진 명판과 어긋나 테두리가 겹쳐 보인다 —
    /// 플레이 검증 C 구역에서 "아래로 갈수록 이질감"으로 올라온 것이 이것이다.
    /// 지금은 <c>T_Menu_Plate{0..3}_Normal/Hover</c> 여덟 장이 각자 제 줄의 픽셀이다.</para>
    ///
    /// <para><b>어느 명판이 켜지는지는 이 컴포넌트가 정하지 않는다.</b> 표지판 전체에서 켜지는 것은
    /// 언제나 한 장뿐이라 그 판단은 <see cref="MenuBannerView"/>가 갖고, 여기서는 들어온 지시를
    /// 그리기만 한다. <see cref="Selectable"/>의 Highlighted/Selected 상태를 그대로 쓰면
    /// 마우스와 키보드가 각각 다른 명판을 켜 두 장이 동시에 빛난다.</para>
    ///
    /// <para><b>색만으로 구분하지 않는다</b>(§8.2 색각 대응) — 황금빛과 함께 <b>화살표가 옮겨 붙고</b>
    /// 명판이 살짝 커진다. 색을 못 봐도 어느 줄이 선택됐는지 읽힌다.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuPlateButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        /// <summary>강조 시 명판이 커지는 배율. 아래 원본 명판을 항상 덮도록 <b>커지는 방향으로만</b> 움직인다(§4.2-3).</summary>
        public const float HighlightScale = 1.02f;

        /// <summary>황금 명판이 떠오르는 데 걸리는 시간. 즉시 바뀌면 어느 줄로 옮겨왔는지 눈이 못 따라간다.</summary>
        public const float FadeSeconds = 0.12f;

        [SerializeField]
        [Tooltip("황금 명판 이미지. 평소 알파 0, 강조 시 1이 된다.")]
        private Image _hover;

        [SerializeField]
        [Tooltip("문구. 이미지에 구워진 글자가 아니라 TMP다 — 로컬라이징이 열려 있다.")]
        private TMP_Text _label;

        [SerializeField]
        [Tooltip("위에서부터 0. 화살표 위치와 내비게이션 순서가 이 값을 쓴다.")]
        private int _slot;

        [SerializeField]
        private UnityEvent _clicked = new UnityEvent();

        private RectTransform _rect;
        private float _weight;
        private bool _highlighted;

        /// <summary>위에서부터 센 슬롯 번호 (0~3).</summary>
        public int Slot => _slot;

        /// <summary>지금 이 명판이 강조 상태인가.</summary>
        public bool IsHighlighted => _highlighted;

        /// <summary>마우스가 올라왔거나 포커스가 들어왔다 — 표지판에 "여기로 옮기라"고 알린다.</summary>
        public event Action<MenuPlateButton> Focused;

        /// <summary>눌렸다. 무엇을 할지는 듣는 쪽이 정한다(4차 <c>MenuSessionActions</c>).</summary>
        public event Action<MenuPlateButton> Clicked;

        protected override void Awake()
        {
            base.Awake();
            _rect = (RectTransform)transform;
            transition = Transition.None;   // 색 틴트 대신 스프라이트 두 장으로 표현한다
            ApplyWeight(0f);
        }

        /// <summary>강조를 켜거나 끈다. 표지판만 호출한다.</summary>
        public void SetHighlighted(bool on, bool instant = false)
        {
            _highlighted = on;
            if (instant || !Application.isPlaying)
            {
                ApplyWeight(on ? 1f : 0f);
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            RaiseFocus();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            RaiseFocus();
        }

        private void RaiseFocus()
        {
            if (!IsInteractable())
            {
                return;
            }

            Focused?.Invoke(this);
        }

        private void Update()
        {
            float target = _highlighted ? 1f : 0f;
            if (Mathf.Approximately(_weight, target))
            {
                return;
            }

            float step = FadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / FadeSeconds;
            ApplyWeight(Mathf.MoveTowards(_weight, target, step));
        }

        private void ApplyWeight(float weight)
        {
            _weight = Mathf.Clamp01(weight);

            if (_hover != null)
            {
                Color c = _hover.color;
                c.a = _weight;
                _hover.color = c;
            }

            if (_rect == null)
            {
                _rect = (RectTransform)transform;
            }

            _rect.localScale = Vector3.one * Mathf.LerpUnclamped(1f, HighlightScale, _weight);

            if (_label != null)
            {
                // 문구 색은 상태에 따라 바꾸지 않는다. 어두운 명판과 황금 명판 위 모두에서 읽혀야 해서
                // 검은 외곽선을 두른 크림색 한 가지로 두고, 상태는 명판 그림·화살표·크기로 말한다.
                _label.color = UiPalette.TextSteam;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Fire();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Fire();
        }

        private void Fire()
        {
            if (!IsInteractable())
            {
                return;
            }

            _clicked.Invoke();
            Clicked?.Invoke(this);
        }
    }
}
