using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 선택된 항목이 받은 취소를 위쪽 <see cref="MenuPanel"/>로 넘긴다.
    ///
    /// <para><b>왜 필요한가</b> — 취소는 계층을 타고 올라오지 않는다. 유니티 입력 모듈이
    /// <c>ExecuteEvents.ExecuteHierarchy</c>가 아니라 <b><c>ExecuteEvents.Execute</c></b>로
    /// 보내기 때문에(`InputSystemUIInputModule.ProcessNavigation`), 취소는 <b>선택된
    /// 오브젝트 단 하나</b>에만 도착한다. 패널 안에서 선택된 것은 언제나 버튼 같은
    /// 자식이므로, 패널 루트에 <see cref="ICancelHandler"/>를 달아 두는 것만으로는
    /// Esc·게임패드 B가 영영 오지 않는다.</para>
    ///
    /// <para><see cref="MenuPanel.Open"/>이 자기 밑의 모든 <c>Selectable</c>에 이 부품을
    /// 런타임에 붙인다 — 씬에 손으로 달아 두면 버튼을 새로 추가할 때마다 빠뜨린다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuCancelRelay : MonoBehaviour, ICancelHandler
    {
        private MenuPanel _panel;

        public void OnCancel(BaseEventData eventData)
        {
            if (_panel == null)
            {
                _panel = GetComponentInParent<MenuPanel>(true);
            }

            if (_panel != null)
            {
                _panel.OnCancel(eventData);
            }
        }
    }
}
