using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 표지판 위로 열리는 하위 패널 한 장 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.3.
    ///
    /// <para><b>열릴 때 포커스를 안으로 가져온다.</b> 그러지 않으면 패널이 떠 있는데 키보드는
    /// 뒤쪽 명판을 오가고, 취소를 눌러도 패널이 아니라 배너가 받는다.</para>
    ///
    /// <para>취소(Esc·게임패드 B)는 <see cref="ICancelHandler"/>로 받는다. 취소 이벤트는 선택된
    /// 오브젝트에서 위로 전파되는데 버튼은 이를 처리하지 않으므로 패널이 받게 된다 —
    /// 입력 액션을 따로 배선하지 않아도 표준 입력 모듈이 그대로 실어 나른다.</para>
    /// </summary>
    public sealed class MenuPanel : MonoBehaviour, ICancelHandler
    {
        [SerializeField]
        [Tooltip("열릴 때 포커스를 받을 항목. 비면 자식에서 첫 번째를 찾는다.")]
        private Selectable _firstSelected;

        /// <summary>취소를 눌렀다 — 닫을지는 듣는 쪽(<see cref="MainMenuRoot"/>)이 정한다.</summary>
        public event Action Cancelled;

        /// <summary>지금 열려 있는가.</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>패널을 열고 포커스를 안으로 가져온다.</summary>
        public void Open()
        {
            gameObject.SetActive(true);

            Selectable first = _firstSelected != null ? _firstSelected : GetComponentInChildren<Selectable>(false);
            if (first != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        /// <summary>패널을 닫는다. 포커스를 어디로 돌려줄지는 부르는 쪽이 정한다.</summary>
        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void OnCancel(BaseEventData eventData)
        {
            Cancelled?.Invoke();
        }
    }
}
